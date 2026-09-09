using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SysPitstops.Api.Auth;
using SysPitstops.Api.Contracts;
using SysPitstops.Api.Data;
using SysPitstops.Api.Domain;

namespace SysPitstops.Api.Controllers;

[ApiController]
[Route("api/service-orders")]
[Authorize]
public class ServiceOrdersController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<ServiceOrderSummary>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<ServiceOrderSummary>>> List(
        [FromQuery] ServiceOrderStatus? status,
        [FromQuery] Guid? mechanicId,
        [FromQuery] Guid? vehicleId,
        [FromQuery] Guid? customerId,
        [FromQuery] DateTimeOffset? openedFrom,
        [FromQuery] DateTimeOffset? openedTo,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken ct)
    {
        var (currentPage, size) = PagedResult<ServiceOrderSummary>.Clamp(page, pageSize);
        var query = Scoped();

        if (status is not null)
        {
            query = query.Where(o => o.Status == status);
        }

        if (mechanicId is not null)
        {
            query = query.Where(o => o.MechanicId == mechanicId);
        }

        if (vehicleId is not null)
        {
            query = query.Where(o => o.VehicleId == vehicleId);
        }

        if (customerId is not null)
        {
            query = query.Where(o => o.CustomerId == customerId);
        }

        if (openedFrom is not null)
        {
            query = query.Where(o => o.OpenedAt >= openedFrom);
        }

        if (openedTo is not null)
        {
            query = query.Where(o => o.OpenedAt < openedTo);
        }

        var total = await query.CountAsync(ct);
        var items = await Summarize(
            query.OrderByDescending(o => o.OpenedAt)
                .Skip((currentPage - 1) * size)
                .Take(size),
            ct);

        return Ok(new PagedResult<ServiceOrderSummary>(items, currentPage, size, total));
    }

    [HttpGet("my-queue")]
    [ProducesResponseType(typeof(IReadOnlyList<ServiceOrderSummary>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ServiceOrderSummary>>> MyQueue(CancellationToken ct)
    {
        var me = User.Id();

        var items = await Summarize(
            Scoped()
                .Where(o => o.MechanicId == me)
                .Where(o => o.Status != ServiceOrderStatus.Delivered
                         && o.Status != ServiceOrderStatus.Canceled)
                .OrderBy(o => o.OpenedAt),
            ct);

        return Ok(items);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ServiceOrderDetail), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ServiceOrderDetail>> Get(Guid id, CancellationToken ct)
    {
        var order = await Describe(id, ct);
        return order is null ? OrderNotFound() : Ok(order);
    }

    [HttpPost]
    [Authorize(Roles = Roles.Desk)]
    [ProducesResponseType(typeof(ServiceOrderDetail), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ServiceOrderDetail>> Open(
        OpenServiceOrderRequest request, CancellationToken ct)
    {
        var workshopId = User.WorkshopId();

        var vehicle = await db.Vehicles
            .SingleOrDefaultAsync(v => v.Id == request.VehicleId && v.WorkshopId == workshopId, ct);

        if (vehicle is null)
        {
            ModelState.AddModelError(nameof(request.VehicleId), "Veículo não encontrado.");
            return ValidationProblem(ModelState);
        }

        var now = DateTimeOffset.UtcNow;
        var order = new ServiceOrder
        {
            WorkshopId = workshopId,
            VehicleId = vehicle.Id,
            CustomerId = vehicle.OwnerId,
            CreatedBy = User.Id(),
            Status = ServiceOrderStatus.Requested,
            Mileage = request.Mileage,
            ReportedIssue = Blank(request.ReportedIssue),
            ScheduledAt = request.ScheduledAt,
            OpenedAt = now,
            CreatedAt = now,
            UpdatedAt = now
        };

        db.ServiceOrders.Add(order);

        db.ServiceOrderStatusHistory.Add(new ServiceOrderStatusHistory
        {
            ServiceOrder = order,
            FromStatus = null,
            ToStatus = ServiceOrderStatus.Requested,
            ChangedBy = User.Id(),
            ChangedAt = now
        });

        await db.SaveChangesAsync(ct);

        var created = await Describe(order.Id, ct);
        return CreatedAtAction(nameof(Get), new { id = order.Id }, created);
    }

    /// <summary>Front desk fields. Assigning the mechanic matters beyond the
    /// KPI: without one, nobody but the admin can take the order to READY.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = Roles.Desk)]
    [ProducesResponseType(typeof(ServiceOrderDetail), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ServiceOrderDetail>> Update(
        Guid id, UpdateServiceOrderRequest request, CancellationToken ct)
    {
        var order = await Editable(id, ct);
        if (order is null)
        {
            return OrderNotFound();
        }

        if (ServiceOrderWorkflow.IsFinal(order.Status))
        {
            return OrderIsClosed(order.Status);
        }

        if (request.MechanicId is { } mechanicId)
        {
            var isMechanic = await db.Users.AnyAsync(
                u => u.Id == mechanicId
                  && u.WorkshopId == order.WorkshopId
                  && u.IsActive
                  && u.Role == UserRole.Mechanic, ct);

            if (!isMechanic)
            {
                ModelState.AddModelError(
                    nameof(request.MechanicId), "Mecânico não encontrado ou inativo.");
                return ValidationProblem(ModelState);
            }
        }

        order.MechanicId = request.MechanicId;
        order.Mileage = request.Mileage;
        order.ReportedIssue = Blank(request.ReportedIssue);
        order.DiscountAmount = request.DiscountAmount;
        order.ScheduledAt = request.ScheduledAt;
        order.UpdatedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(ct);

        return Ok(await Describe(order.Id, ct));
    }

    /// <summary>Separate from the update above because this one is the
    /// mechanic's, written from the yard — and the front desk fields are not.</summary>
    [HttpPut("{id:guid}/diagnosis")]
    [ProducesResponseType(typeof(ServiceOrderDetail), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ServiceOrderDetail>> UpdateDiagnosis(
        Guid id, UpdateDiagnosisRequest request, CancellationToken ct)
    {
        var order = await Editable(id, ct);
        if (order is null)
        {
            return OrderNotFound();
        }

        if (ServiceOrderWorkflow.IsFinal(order.Status))
        {
            return OrderIsClosed(order.Status);
        }

        var role = User.Role();
        var mayWrite = role is UserRole.Admin or UserRole.Attendant
            || (role == UserRole.Mechanic && order.MechanicId == User.Id());

        if (!mayWrite)
        {
            return Forbid();
        }

        order.Diagnosis = Blank(request.Diagnosis);
        order.UpdatedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(ct);

        return Ok(await Describe(order.Id, ct));
    }

    /// <summary>
    /// Runs the same workflow against every reachable status and returns only
    /// what this user may do. The board reads it to decide which buttons to
    /// show, instead of carrying a second copy of the rules in TypeScript.
    /// </summary>
    [HttpGet("{id:guid}/allowed-transitions")]
    [ProducesResponseType(typeof(IReadOnlyList<AllowedTransition>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<AllowedTransition>>> AllowedTransitions(
        Guid id, CancellationToken ct)
    {
        var workshopId = User.WorkshopId();

        var order = await db.ServiceOrders.AsNoTracking()
            .Where(o => o.Id == id && o.WorkshopId == workshopId)
            .Select(o => new { o.Id, o.Status, o.MechanicId })
            .SingleOrDefaultAsync(ct);

        if (order is null)
        {
            return OrderNotFound();
        }

        var role = User.Role();
        var isAssigned = order.MechanicId is not null && order.MechanicId == User.Id();
        var hasQuote = await HasApprovedQuote(order.Id, ct);

        List<ServiceOrderStatus> reachable = [];

        if (ServiceOrderWorkflow.Next(order.Status) is { } next)
        {
            reachable.Add(next);
        }

        if (ServiceOrderWorkflow.CanBeCanceled(order.Status))
        {
            reachable.Add(ServiceOrderStatus.Canceled);
        }

        List<AllowedTransition> allowed = [];

        foreach (var target in reachable)
        {
            var plain = new ServiceOrderTransition(
                order.Status, target, role, isAssigned, hasQuote, WaivesApproval: false);

            if (ServiceOrderWorkflow.Check(plain).Allowed)
            {
                allowed.Add(new AllowedTransition(target, RequiresNote: false));
                continue;
            }

            // Only reachable by waiving the quote (D-13), which the admin may do
            // as long as the reason is written down — hence RequiresNote.
            var waiving = plain with { WaivesApproval = true };

            if (ServiceOrderWorkflow.Check(waiving).Allowed)
            {
                allowed.Add(new AllowedTransition(target, RequiresNote: true));
            }
        }

        return Ok(allowed);
    }

    /// <summary>
    /// Resolves the facts the workflow asks for, applies its verdict and records
    /// the move in service_order_status_history. The rules themselves live in
    /// ServiceOrderWorkflow — this only gathers the evidence and writes down what
    /// happened.
    /// </summary>
    [HttpPost("{id:guid}/status")]
    [ProducesResponseType(typeof(ServiceOrderDetail), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ServiceOrderDetail>> ChangeStatus(
        Guid id, ChangeStatusRequest request, CancellationToken ct)
    {
        var order = await Editable(id, ct);
        if (order is null)
        {
            return OrderNotFound();
        }

        var note = Blank(request.Note);

        // D-13 only allows the waiver when the reason is recorded, so an empty
        // note makes the request invalid rather than silently unaudited.
        if (request.WaiveApproval && note is null)
        {
            ModelState.AddModelError(
                nameof(request.Note),
                "Informe o motivo da dispensa de aprovação do orçamento.");
            return ValidationProblem(ModelState);
        }

        var verdict = ServiceOrderWorkflow.Check(new ServiceOrderTransition(
            order.Status,
            request.ToStatus,
            User.Role(),
            IsAssignedMechanic: order.MechanicId is not null && order.MechanicId == User.Id(),
            HasApprovedQuote: await HasApprovedQuote(order.Id, ct),
            WaivesApproval: request.WaiveApproval));

        if (!verdict.Allowed)
        {
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = verdict.Reason
            });
        }

        var now = DateTimeOffset.UtcNow;

        db.ServiceOrderStatusHistory.Add(new ServiceOrderStatusHistory
        {
            ServiceOrderId = order.Id,
            FromStatus = order.Status,
            ToStatus = request.ToStatus,
            ChangedBy = User.Id(),
            Note = note,
            ChangedAt = now
        });

        if (request.WaiveApproval)
        {
            order.ApprovalWaivedNote = note;
        }

        // The revenue query of data-model.md, section 6, reads closed_at for
        // orders in READY and DELIVERED, so the work finishing is what closes
        // the order. Cancelling leaves it null: nothing was concluded.
        if (request.ToStatus == ServiceOrderStatus.Ready)
        {
            order.ClosedAt = now;

            // D-11 writes the stock off here, in this same transaction. That
            // lands in the stock pull request of week 3.
        }

        order.Status = request.ToStatus;
        order.UpdatedAt = now;

        await db.SaveChangesAsync(ct);

        return Ok(await Describe(order.Id, ct));
    }

    [HttpPost("{id:guid}/items")]
    [Authorize(Roles = Roles.Desk)]
    [ProducesResponseType(typeof(ServiceOrderItemResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ServiceOrderItemResponse>> AddItem(
        Guid id, ServiceOrderItemRequest request, CancellationToken ct)
    {
        var order = await Editable(id, ct);
        if (order is null)
        {
            return OrderNotFound();
        }

        if (!ServiceOrderWorkflow.AllowsItemChanges(order.Status))
        {
            return ItemsAreClosed(order.Status);
        }

        if (!await ValidatePart(request, ct))
        {
            return ValidationProblem(ModelState);
        }

        var now = DateTimeOffset.UtcNow;
        var item = new ServiceOrderItem
        {
            ServiceOrderId = order.Id,
            ItemType = request.ItemType,
            PartId = request.PartId,
            Description = request.Description.Trim(),
            Quantity = request.Quantity,
            UnitPrice = request.UnitPrice,
            CreatedBy = User.Id(),
            CreatedAt = now
        };

        db.ServiceOrderItems.Add(item);
        order.UpdatedAt = now;
        await db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(Get), new { id = order.Id }, Describe(item));
    }

    [HttpPut("{id:guid}/items/{itemId:guid}")]
    [Authorize(Roles = Roles.Desk)]
    [ProducesResponseType(typeof(ServiceOrderItemResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ServiceOrderItemResponse>> UpdateItem(
        Guid id, Guid itemId, ServiceOrderItemRequest request, CancellationToken ct)
    {
        var order = await Editable(id, ct);
        if (order is null)
        {
            return OrderNotFound();
        }

        if (!ServiceOrderWorkflow.AllowsItemChanges(order.Status))
        {
            return ItemsAreClosed(order.Status);
        }

        var item = await db.ServiceOrderItems
            .SingleOrDefaultAsync(i => i.Id == itemId && i.ServiceOrderId == order.Id, ct);

        if (item is null)
        {
            return ItemNotFound();
        }

        if (!await ValidatePart(request, ct))
        {
            return ValidationProblem(ModelState);
        }

        item.ItemType = request.ItemType;
        item.PartId = request.PartId;
        item.Description = request.Description.Trim();
        item.Quantity = request.Quantity;
        item.UnitPrice = request.UnitPrice;
        order.UpdatedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(ct);

        return Ok(Describe(item));
    }

    [HttpDelete("{id:guid}/items/{itemId:guid}")]
    [Authorize(Roles = Roles.Desk)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteItem(Guid id, Guid itemId, CancellationToken ct)
    {
        var order = await Editable(id, ct);
        if (order is null)
        {
            return OrderNotFound();
        }

        if (!ServiceOrderWorkflow.AllowsItemChanges(order.Status))
        {
            return ItemsAreClosed(order.Status);
        }

        var item = await db.ServiceOrderItems
            .SingleOrDefaultAsync(i => i.Id == itemId && i.ServiceOrderId == order.Id, ct);

        if (item is null)
        {
            return ItemNotFound();
        }

        db.ServiceOrderItems.Remove(item);
        order.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);

        return NoContent();
    }

    // -----------------------------------------------------------------------

    private IQueryable<ServiceOrder> Scoped()
    {
        var workshopId = User.WorkshopId();
        return db.ServiceOrders.AsNoTracking().Where(o => o.WorkshopId == workshopId);
    }

    private static async Task<IReadOnlyList<ServiceOrderSummary>> Summarize(
        IQueryable<ServiceOrder> query, CancellationToken ct)
    {
        var rows = await query
            .Select(o => new
            {
                o.Id,
                o.Number,
                o.Status,
                o.VehicleId,
                o.Vehicle.Plate,
                o.Vehicle.Brand,
                o.Vehicle.Model,
                o.Vehicle.ModelYear,
                o.CustomerId,
                CustomerName = o.Customer.Name,
                o.MechanicId,
                MechanicName = o.Mechanic != null ? o.Mechanic.Name : null,
                // D-09: the total is computed, never stored.
                ItemsTotal = o.Items.Sum(i => i.Quantity * i.UnitPrice),
                o.DiscountAmount,
                // D-35: the answer travels as a badge on the card, because
                // approving does not always move the order.
                HasApprovedQuote = o.Quotes.Any(q => q.Status == QuoteStatus.Approved),
                o.OpenedAt,
                o.ScheduledAt,
                o.ClosedAt
            })
            .ToListAsync(ct);

        return rows.Select(r => new ServiceOrderSummary(
            r.Id,
            r.Number,
            r.Status,
            r.VehicleId,
            r.Plate,
            VehicleLabel.Describe(r.Brand, r.Model, r.ModelYear),
            r.CustomerId,
            r.CustomerName,
            r.MechanicId,
            r.MechanicName,
            r.ItemsTotal - r.DiscountAmount,
            r.HasApprovedQuote,
            r.OpenedAt,
            r.ScheduledAt,
            r.ClosedAt)).ToList();
    }

    private async Task<ServiceOrderDetail?> Describe(Guid id, CancellationToken ct)
    {
        var workshopId = User.WorkshopId();

        var row = await db.ServiceOrders.AsNoTracking()
            .Where(o => o.Id == id && o.WorkshopId == workshopId)
            .Select(o => new
            {
                Order = o,
                o.Vehicle.Plate,
                o.Vehicle.Brand,
                o.Vehicle.Model,
                o.Vehicle.ModelYear,
                CustomerName = o.Customer.Name,
                CustomerPhone = o.Customer.Phone,
                MechanicName = o.Mechanic != null ? o.Mechanic.Name : null,
                Items = o.Items
                    .OrderBy(i => i.CreatedAt)
                    .Select(i => new ServiceOrderItemResponse(
                        i.Id, i.ItemType, i.PartId, i.Description,
                        i.Quantity, i.UnitPrice, i.Quantity * i.UnitPrice, i.CreatedAt))
                    .ToList(),
                History = o.StatusHistory
                    .OrderBy(h => h.ChangedAt)
                    .Select(h => new ServiceOrderStatusChange(
                        h.Id, h.FromStatus, h.ToStatus,
                        h.ChangedByUser.Name, h.Note, h.ChangedAt))
                    .ToList()
            })
            .SingleOrDefaultAsync(ct);

        if (row is null)
        {
            return null;
        }

        var itemsTotal = row.Items.Sum(i => i.Total);

        return new ServiceOrderDetail(
            row.Order.Id,
            row.Order.Number,
            row.Order.Status,
            row.Order.VehicleId,
            row.Plate,
            VehicleLabel.Describe(row.Brand, row.Model, row.ModelYear),
            row.Order.CustomerId,
            row.CustomerName,
            row.CustomerPhone,
            row.Order.MechanicId,
            row.MechanicName,
            row.Order.Mileage,
            row.Order.ReportedIssue,
            row.Order.Diagnosis,
            row.Order.ApprovalWaivedNote,
            itemsTotal,
            row.Order.DiscountAmount,
            itemsTotal - row.Order.DiscountAmount,
            row.Items,
            row.History,
            row.Order.OpenedAt,
            row.Order.ScheduledAt,
            row.Order.ClosedAt);
    }


    private Task<ServiceOrder?> Editable(Guid id, CancellationToken ct)
    {
        var workshopId = User.WorkshopId();
        return db.ServiceOrders
            .SingleOrDefaultAsync(o => o.Id == id && o.WorkshopId == workshopId, ct);
    }

    private async Task<bool> ValidatePart(ServiceOrderItemRequest request, CancellationToken ct)
    {
        if (request.ItemType == ItemType.Part)
        {
            if (request.PartId is null)
            {
                ModelState.AddModelError(
                    nameof(request.PartId), "Informe a peça para um item do tipo PART.");
            }
            else if (!await db.Parts.AnyAsync(
                p => p.Id == request.PartId && p.WorkshopId == User.WorkshopId(), ct))
            {
                ModelState.AddModelError(nameof(request.PartId), "Peça não encontrada.");
            }
        }
        else if (request.PartId is not null)
        {
            ModelState.AddModelError(
                nameof(request.PartId), "Um item do tipo SERVICE não aponta para peça.");
        }

        return ModelState.IsValid;
    }

    private static ServiceOrderItemResponse Describe(ServiceOrderItem item) =>
        new(item.Id, item.ItemType, item.PartId, item.Description,
            item.Quantity, item.UnitPrice, item.Quantity * item.UnitPrice, item.CreatedAt);

    private NotFoundObjectResult OrderNotFound() => NotFound(new ProblemDetails
    {
        Status = StatusCodes.Status404NotFound,
        Title = "Ordem de serviço não encontrada."
    });

    /// <summary>Always false during week 2: the quotes table exists but nothing
    /// writes to it yet. The transition rule is already in place waiting for it
    /// (D-13), so only the admin waiver can start the work for now.</summary>
    private Task<bool> HasApprovedQuote(Guid orderId, CancellationToken ct) =>
        db.Quotes.AnyAsync(
            q => q.ServiceOrderId == orderId && q.Status == QuoteStatus.Approved, ct);

    private NotFoundObjectResult ItemNotFound() => NotFound(new ProblemDetails
    {
        Status = StatusCodes.Status404NotFound,
        Title = "Item não encontrado nesta ordem de serviço."
    });

    private ConflictObjectResult OrderIsClosed(ServiceOrderStatus status) => Conflict(
        new ProblemDetails
        {
            Status = StatusCodes.Status409Conflict,
            Title = $"A ordem de serviço está em {status.ToPgName()} e não pode mais ser alterada."
        });

    private ConflictObjectResult ItemsAreClosed(ServiceOrderStatus status) => Conflict(
        new ProblemDetails
        {
            Status = StatusCodes.Status409Conflict,
            Title = $"Os itens não podem mais ser alterados: a ordem está em {status.ToPgName()}.",
            Detail = "A baixa de estoque ocorre na conclusão, então a lista de itens "
                + "é congelada a partir dela."
        });

    private static string? Blank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
