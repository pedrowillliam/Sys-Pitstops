using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SysPitstops.Api.Auth;
using SysPitstops.Api.Contracts;
using SysPitstops.Api.Data;
using SysPitstops.Api.Domain;

namespace SysPitstops.Api.Controllers;

[ApiController]
[Route("api/service-orders/{orderId:guid}/quotes")]
[Authorize(Roles = Roles.Desk)]
public class QuotesController(AppDbContext db) : ControllerBase
{
    public static readonly TimeSpan Validity = TimeSpan.FromDays(7);

    /// <summary>The quote screen lists the workshop, not one order: the
    /// attendant opens it to see who has not answered yet. It hangs off
    /// /api/quotes, outside this controller's per-order prefix.</summary>
    [HttpGet("~/api/quotes")]
    [ProducesResponseType(typeof(PagedResult<QuoteListItem>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<QuoteListItem>>> ListAll(
        [FromQuery] QuoteStatus? status,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken ct)
    {
        var workshopId = User.WorkshopId();
        var now = DateTimeOffset.UtcNow;
        var (currentPage, size) = PagedResult<QuoteListItem>.Clamp(page, pageSize);

        var query = db.Quotes.AsNoTracking()
            .Where(q => q.ServiceOrder.WorkshopId == workshopId);

        if (status is not null)
        {
            query = query.Where(QuoteLifecycle.HasStatus(status.Value, now));
        }

        var total = await query.CountAsync(ct);

        var rows = await query
            .OrderByDescending(q => q.SentAt)
            .Skip((currentPage - 1) * size)
            .Take(size)
            .Select(q => new
            {
                q.Id,
                q.ServiceOrderId,
                q.ServiceOrder.Number,
                OrderStatus = q.ServiceOrder.Status,
                q.Status,
                q.TotalAmount,
                CustomerName = q.ServiceOrder.Customer.Name,
                CustomerPhone = q.ServiceOrder.Customer.Phone,
                q.ServiceOrder.Vehicle.Plate,
                q.ServiceOrder.Vehicle.Brand,
                q.ServiceOrder.Vehicle.Model,
                q.ServiceOrder.Vehicle.ModelYear,
                q.PublicToken,
                SentByName = q.SentByUser.Name,
                q.SentAt,
                q.ExpiresAt,
                q.RespondedAt
            })
            .ToListAsync(ct);

        var items = rows.Select(r => new QuoteListItem(
            r.Id,
            r.ServiceOrderId,
            r.Number,
            r.OrderStatus,
            r.Status == QuoteStatus.Sent && r.ExpiresAt <= now ? QuoteStatus.Expired : r.Status,
            r.TotalAmount,
            r.CustomerName,
            r.CustomerPhone,
            r.Plate,
            VehicleLabel.Describe(r.Brand, r.Model, r.ModelYear),
            r.PublicToken,
            r.SentByName,
            r.SentAt,
            r.ExpiresAt,
            r.RespondedAt)).ToList();

        return Ok(new PagedResult<QuoteListItem>(items, currentPage, size, total));
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<QuoteResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<QuoteResponse>>> List(
        Guid orderId, CancellationToken ct)
    {
        if (!await OrderExists(orderId, ct))
        {
            return OrderNotFound();
        }

        var quotes = await db.Quotes.AsNoTracking()
            .Include(q => q.ServiceOrder)
            .Include(q => q.SentByUser)
            .Where(q => q.ServiceOrderId == orderId)
            .OrderByDescending(q => q.SentAt)
            .ToListAsync(ct);

        foreach (var quote in quotes)
        {
            QuoteLifecycle.ApplyExpiry(quote);
        }

        return Ok(quotes.Select(Describe).ToList());
    }

    [HttpPost]
    [ProducesResponseType(typeof(QuoteResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<QuoteResponse>> Send(Guid orderId, CancellationToken ct)
    {
        var workshopId = User.WorkshopId();

        var order = await db.ServiceOrders
            .Include(o => o.Items)
            .SingleOrDefaultAsync(o => o.Id == orderId && o.WorkshopId == workshopId, ct);

        if (order is null)
        {
            return OrderNotFound();
        }

        if (ServiceOrderWorkflow.IsFinal(order.Status))
        {
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = $"A ordem de serviço está em {order.Status.ToPgName()} "
                    + "e não recebe mais orçamento."
            });
        }

        if (order.Items.Count == 0)
        {
            ModelState.AddModelError(
                "items", "Lance ao menos um item antes de enviar o orçamento.");
            return ValidationProblem(ModelState);
        }

        var live = await db.Quotes
            .Where(q => q.ServiceOrderId == orderId && q.Status == QuoteStatus.Sent)
            .ToListAsync(ct);

        foreach (var previous in live)
        {
            previous.Status = QuoteStatus.Expired;
        }

        var now = DateTimeOffset.UtcNow;
        var items = order.Items
            .OrderBy(i => i.CreatedAt)
            .Select(i => new QuoteItemSnapshot(
                i.ItemType, i.Description, i.Quantity, i.UnitPrice, i.Quantity * i.UnitPrice))
            .ToList();

        var quote = new Quote
        {
            ServiceOrderId = order.Id,
            PublicToken = PublicToken.Create(),
            Status = QuoteStatus.Sent,
            ItemsSnapshot = JsonSerializer.Serialize(items, QuoteLifecycle.Json),
            TotalAmount = Math.Max(0, items.Sum(i => i.Total) - order.DiscountAmount),
            SentBy = User.Id(),
            SentAt = now,
            ExpiresAt = now.Add(Validity)
        };

        db.Quotes.Add(quote);
        await db.SaveChangesAsync(ct);

        quote.ServiceOrder = order;
        await db.Entry(quote).Reference(q => q.SentByUser).LoadAsync(ct);

        return CreatedAtAction(nameof(List), new { orderId }, Describe(quote));
    }

    private Task<bool> OrderExists(Guid orderId, CancellationToken ct)
    {
        var workshopId = User.WorkshopId();
        return db.ServiceOrders.AnyAsync(
            o => o.Id == orderId && o.WorkshopId == workshopId, ct);
    }

    private QuoteResponse Describe(Quote quote) => new(
        quote.Id,
        quote.ServiceOrderId,
        quote.ServiceOrder?.Number ?? 0,
        quote.Status,
        quote.TotalAmount,
        QuoteLifecycle.ReadItems(quote),
        quote.PublicToken,
        quote.RejectionReason,
        quote.SentByUser?.Name ?? string.Empty,
        quote.SentAt,
        quote.ExpiresAt,
        quote.RespondedAt);

    private NotFoundObjectResult OrderNotFound() => NotFound(new ProblemDetails
    {
        Status = StatusCodes.Status404NotFound,
        Title = "Ordem de serviço não encontrada."
    });
}
