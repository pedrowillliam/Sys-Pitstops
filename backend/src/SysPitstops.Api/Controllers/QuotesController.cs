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
