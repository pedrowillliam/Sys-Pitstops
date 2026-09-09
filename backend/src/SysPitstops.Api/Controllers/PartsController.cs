using System.Linq.Expressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SysPitstops.Api.Auth;
using SysPitstops.Api.Contracts;
using SysPitstops.Api.Data;
using SysPitstops.Api.Domain;

namespace SysPitstops.Api.Controllers;

[ApiController]
[Route("api/parts")]
[Authorize]
public class PartsController(AppDbContext db) : ControllerBase
{
    private static readonly Expression<Func<Part, PartResponse>> ToResponse =
        p => new PartResponse(
            p.Id, p.Sku, p.Name, p.Description, p.Unit,
            p.SalePrice, p.CostPrice, p.QuantityOnHand, p.MinQuantity,
            p.Location, p.IsActive,
            p.QuantityOnHand <= p.MinQuantity,
            p.QuantityOnHand < 0,
            p.CreatedAt, p.UpdatedAt);

    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<PartResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<PartResponse>>> List(
        [FromQuery] string? search,
        [FromQuery] bool lowStock,
        [FromQuery] bool includeInactive,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken ct)
    {
        var (currentPage, size) = PagedResult<PartResponse>.Clamp(page, pageSize);
        var workshopId = User.WorkshopId();

        var query = db.Parts.AsNoTracking().Where(p => p.WorkshopId == workshopId);

        if (!includeInactive)
        {
            query = query.Where(p => p.IsActive);
        }

        // Matches the "estoque baixo" filter of the prototype, and the index
        // ix_parts_low_stock exists for exactly this predicate.
        if (lowStock)
        {
            query = query.Where(p => p.QuantityOnHand <= p.MinQuantity);
        }

        var term = search?.Trim().ToLowerInvariant();
        if (!string.IsNullOrEmpty(term))
        {
            query = query.Where(p => p.Name.ToLower().Contains(term) || p.Sku.ToLower().Contains(term));
        }

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(p => p.Name)
            .Skip((currentPage - 1) * size)
            .Take(size)
            .Select(ToResponse)
            .ToListAsync(ct);

        return Ok(new PagedResult<PartResponse>(items, currentPage, size, total));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(PartResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PartResponse>> Get(Guid id, CancellationToken ct)
    {
        var part = await Describe(id, ct);
        return part is null ? PartNotFound() : Ok(part);
    }

    // A new part starts at zero on purpose: the balance is a cache of
    // stock_movements, so the first units arrive through an IN movement and
    // leave a row behind. A form that seeded the balance would break section 4
    // on the very first record.
    [HttpPost]
    [Authorize(Roles = Roles.Desk)]
    [ProducesResponseType(typeof(PartResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PartResponse>> Create(PartRequest request, CancellationToken ct)
    {
        var workshopId = User.WorkshopId();
        var sku = request.Sku.Trim();

        if (await SkuTaken(workshopId, sku, null, ct))
        {
            return ValidationProblem(ModelState);
        }

        var now = DateTimeOffset.UtcNow;
        var part = new Part
        {
            WorkshopId = workshopId,
            Sku = sku,
            Name = request.Name.Trim(),
            Description = Blank(request.Description),
            Unit = request.Unit.Trim().ToUpperInvariant(),
            SalePrice = request.SalePrice,
            CostPrice = request.CostPrice,
            QuantityOnHand = 0m,
            MinQuantity = request.MinQuantity,
            Location = Blank(request.Location),
            CreatedAt = now,
            UpdatedAt = now
        };

        db.Parts.Add(part);
        await db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(Get), new { id = part.Id }, Describe(part));
    }

    // Price, name and minimum are editable; the balance is not. Changing the
    // sale price never touches what is already on a service order — D-07 froze
    // that at insert time.
    [HttpPut("{id:guid}")]
    [Authorize(Roles = Roles.Desk)]
    [ProducesResponseType(typeof(PartResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PartResponse>> Update(
        Guid id, PartRequest request, CancellationToken ct)
    {
        var workshopId = User.WorkshopId();
        var part = await db.Parts.SingleOrDefaultAsync(
            p => p.Id == id && p.WorkshopId == workshopId, ct);

        if (part is null)
        {
            return PartNotFound();
        }

        var sku = request.Sku.Trim();
        if (await SkuTaken(workshopId, sku, part.Id, ct))
        {
            return ValidationProblem(ModelState);
        }

        part.Sku = sku;
        part.Name = request.Name.Trim();
        part.Description = Blank(request.Description);
        part.Unit = request.Unit.Trim().ToUpperInvariant();
        part.SalePrice = request.SalePrice;
        part.CostPrice = request.CostPrice;
        part.MinQuantity = request.MinQuantity;
        part.Location = Blank(request.Location);
        part.UpdatedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(ct);

        return Ok(Describe(part));
    }

    // Same reasoning as the customer: a part quoted on a past order can never be
    // deleted, because service_order_items points at this row for traceability.
    // Deactivating takes it out of the pickers and keeps the history readable.
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = Roles.Desk)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> Deactivate(Guid id, CancellationToken ct) =>
        SetActive(id, false, ct);

    [HttpPost("{id:guid}/reactivate")]
    [Authorize(Roles = Roles.Desk)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> Reactivate(Guid id, CancellationToken ct) =>
        SetActive(id, true, ct);

    [HttpGet("{id:guid}/movements")]
    [ProducesResponseType(typeof(PagedResult<StockMovementResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PagedResult<StockMovementResponse>>> Movements(
        Guid id,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken ct)
    {
        var workshopId = User.WorkshopId();

        if (!await db.Parts.AnyAsync(p => p.Id == id && p.WorkshopId == workshopId, ct))
        {
            return PartNotFound();
        }

        var (currentPage, size) = PagedResult<StockMovementResponse>.Clamp(page, pageSize);
        var query = db.StockMovements.AsNoTracking()
            .Where(m => m.PartId == id && m.WorkshopId == workshopId);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(m => m.CreatedAt)
            .Skip((currentPage - 1) * size)
            .Take(size)
            .Select(m => new StockMovementResponse(
                m.Id, m.PartId, m.MovementType, m.Quantity, m.UnitCost,
                m.ServiceOrderId,
                m.ServiceOrder == null ? null : m.ServiceOrder.Number,
                m.UserId, m.User.Name, m.Note, m.CreatedAt))
            .ToListAsync(ct);

        return Ok(new PagedResult<StockMovementResponse>(items, currentPage, size, total));
    }

    /// <summary>
    /// The only way the balance changes by hand. The movement and the new
    /// balance are written by a single SaveChangesAsync, which is the single
    /// transaction section 4 of data-model.md requires.
    /// </summary>
    [HttpPost("{id:guid}/movements")]
    [Authorize(Roles = Roles.Desk)]
    [ProducesResponseType(typeof(PartResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PartResponse>> Move(
        Guid id, StockMovementRequest request, CancellationToken ct)
    {
        var workshopId = User.WorkshopId();
        var part = await db.Parts.SingleOrDefaultAsync(
            p => p.Id == id && p.WorkshopId == workshopId, ct);

        if (part is null)
        {
            return PartNotFound();
        }

        if (request.MovementType != MovementType.Adjustment && request.Quantity <= 0)
        {
            ModelState.AddModelError(
                nameof(request.Quantity), "A quantidade deve ser maior que zero.");
            return ValidationProblem(ModelState);
        }

        var movement = Apply(request.MovementType, request.Quantity, part.QuantityOnHand);

        // Counting zero on a balance that is already zero moves nothing. There
        // is no row to write — quantity > 0 is a check constraint — and no
        // balance to change.
        if (movement is null)
        {
            return Ok(Describe(part));
        }

        var (type, quantity, balance) = movement.Value;
        var now = DateTimeOffset.UtcNow;

        db.StockMovements.Add(new StockMovement
        {
            WorkshopId = workshopId,
            PartId = part.Id,
            MovementType = type,
            Quantity = quantity,
            UnitCost = request.UnitCost,
            UserId = User.Id(),
            Note = Blank(request.Note),
            CreatedAt = now
        });

        part.QuantityOnHand = balance;
        part.UpdatedAt = now;

        await db.SaveChangesAsync(ct);

        return Ok(Describe(part));
    }

    /// <summary>
    /// Decides which row records the request and where the balance lands.
    /// <para>
    /// IN adds and OUT subtracts, both carrying how much moved. An ADJUSTMENT
    /// carries the <b>counted balance</b> instead of a difference: the column is
    /// positive by check constraint, so a difference would have nowhere to put
    /// its sign. Replaying the movements therefore rebuilds the balance — IN
    /// adds, OUT subtracts, ADJUSTMENT sets (D-40).
    /// </para>
    /// <para>
    /// Counting zero is the one balance the constraint cannot express, so it is
    /// recorded as whichever movement reaches zero from where the part is.
    /// </para>
    /// </summary>
    private static (MovementType Type, decimal Quantity, decimal Balance)? Apply(
        MovementType requested, decimal quantity, decimal balance) => requested switch
    {
        MovementType.In => (MovementType.In, quantity, balance + quantity),
        MovementType.Out => (MovementType.Out, quantity, balance - quantity),
        _ when quantity > 0 => (MovementType.Adjustment, quantity, quantity),
        _ when balance > 0 => (MovementType.Out, balance, 0m),
        _ when balance < 0 => (MovementType.In, -balance, 0m),
        _ => null
    };

    private async Task<IActionResult> SetActive(Guid id, bool isActive, CancellationToken ct)
    {
        var workshopId = User.WorkshopId();
        var part = await db.Parts.SingleOrDefaultAsync(
            p => p.Id == id && p.WorkshopId == workshopId, ct);

        if (part is null)
        {
            return PartNotFound();
        }

        if (part.IsActive != isActive)
        {
            part.IsActive = isActive;
            part.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
        }

        return NoContent();
    }

    private async Task<bool> SkuTaken(int workshopId, string sku, Guid? exceptId, CancellationToken ct)
    {
        var taken = await db.Parts.AnyAsync(
            p => p.WorkshopId == workshopId
                 && p.Sku == sku
                 && (exceptId == null || p.Id != exceptId),
            ct);

        if (taken)
        {
            ModelState.AddModelError(nameof(PartRequest.Sku), "Já existe uma peça com este código.");
        }

        return taken;
    }

    private Task<PartResponse?> Describe(Guid id, CancellationToken ct)
    {
        var workshopId = User.WorkshopId();
        return db.Parts.AsNoTracking()
            .Where(p => p.Id == id && p.WorkshopId == workshopId)
            .Select(ToResponse)
            .SingleOrDefaultAsync(ct)!;
    }

    private static PartResponse Describe(Part p) => new(
        p.Id, p.Sku, p.Name, p.Description, p.Unit,
        p.SalePrice, p.CostPrice, p.QuantityOnHand, p.MinQuantity,
        p.Location, p.IsActive,
        p.QuantityOnHand <= p.MinQuantity,
        p.QuantityOnHand < 0,
        p.CreatedAt, p.UpdatedAt);

    private NotFoundObjectResult PartNotFound() => NotFound(new ProblemDetails
    {
        Status = StatusCodes.Status404NotFound,
        Title = "Peça não encontrada."
    });

    private static string? Blank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
