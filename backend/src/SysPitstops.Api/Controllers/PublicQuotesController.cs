using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using SysPitstops.Api.Contracts;
using SysPitstops.Api.Data;
using SysPitstops.Api.Domain;

namespace SysPitstops.Api.Controllers;

[ApiController]
[Route("api/public/quotes/{token}")]
[AllowAnonymous]
[EnableRateLimiting(PublicQuotesController.RateLimitPolicy)]
public class PublicQuotesController(AppDbContext db) : ControllerBase
{
    public const string RateLimitPolicy = "public-quote";

    [HttpGet]
    [ProducesResponseType(typeof(PublicQuoteResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PublicQuoteResponse>> Get(string token, CancellationToken ct)
    {
        var quote = await Find(token, ct);
        if (quote is null)
        {
            return LinkNotFound();
        }

        if (QuoteLifecycle.ApplyExpiry(quote))
        {
            await db.SaveChangesAsync(ct);
        }

        return Ok(Describe(quote));
    }

    [HttpPost("approve")]
    [ProducesResponseType(typeof(PublicQuoteResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PublicQuoteResponse>> Approve(
        string token, CancellationToken ct)
    {
        var quote = await Find(token, ct);
        if (quote is null)
        {
            return LinkNotFound();
        }

        if (QuoteLifecycle.ApplyExpiry(quote))
        {
            await db.SaveChangesAsync(ct);
        }

        if (!QuoteLifecycle.AwaitsAnswer(quote))
        {
            return AlreadyAnswered(quote);
        }

        var now = DateTimeOffset.UtcNow;
        quote.Status = QuoteStatus.Approved;
        quote.RespondedAt = now;

        var order = quote.ServiceOrder;

        if (order.Status == ServiceOrderStatus.AwaitingApproval)
        {
            db.ServiceOrderStatusHistory.Add(new ServiceOrderStatusHistory
            {
                ServiceOrderId = order.Id,
                FromStatus = order.Status,
                ToStatus = ServiceOrderStatus.InProgress,
                ChangedBy = quote.SentBy,
                Note = "Orçamento aprovado pelo cliente no link público.",
                ChangedAt = now
            });

            order.Status = ServiceOrderStatus.InProgress;
            order.UpdatedAt = now;
        }

        await db.SaveChangesAsync(ct);

        return Ok(Describe(quote));
    }

    [HttpPost("reject")]
    [ProducesResponseType(typeof(PublicQuoteResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PublicQuoteResponse>> Reject(
        string token, RejectQuoteRequest request, CancellationToken ct)
    {
        var quote = await Find(token, ct);
        if (quote is null)
        {
            return LinkNotFound();
        }

        if (QuoteLifecycle.ApplyExpiry(quote))
        {
            await db.SaveChangesAsync(ct);
        }

        if (!QuoteLifecycle.AwaitsAnswer(quote))
        {
            return AlreadyAnswered(quote);
        }

        quote.Status = QuoteStatus.Rejected;
        quote.RejectionReason = string.IsNullOrWhiteSpace(request.Reason)
            ? null
            : request.Reason.Trim();
        quote.RespondedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(ct);

        return Ok(Describe(quote));
    }

    private Task<Quote?> Find(string token, CancellationToken ct) =>
        db.Quotes
            .Include(q => q.ServiceOrder).ThenInclude(o => o.Vehicle)
            .Include(q => q.ServiceOrder).ThenInclude(o => o.Customer)
            .Include(q => q.ServiceOrder).ThenInclude(o => o.Workshop)
            .SingleOrDefaultAsync(q => q.PublicToken == token, ct);

    private static PublicQuoteResponse Describe(Quote quote)
    {
        var order = quote.ServiceOrder;
        var vehicle = order.Vehicle;
        var description = vehicle.ModelYear is null
            ? $"{vehicle.Brand} {vehicle.Model}"
            : $"{vehicle.Brand} {vehicle.Model} {vehicle.ModelYear}";

        return new PublicQuoteResponse(
            order.Number,
            order.Workshop.Name,
            order.Customer.Name,
            description,
            vehicle.Plate,
            quote.Status,
            quote.TotalAmount,
            QuoteLifecycle.ReadItems(quote),
            quote.SentAt,
            quote.ExpiresAt);
    }

    private NotFoundObjectResult LinkNotFound() => NotFound(new ProblemDetails
    {
        Status = StatusCodes.Status404NotFound,
        Title = "Orçamento não encontrado.",
        Detail = "O link pode ter expirado. Peça um novo à oficina."
    });

    private ConflictObjectResult AlreadyAnswered(Quote quote) => Conflict(new ProblemDetails
    {
        Status = StatusCodes.Status409Conflict,
        Title = quote.Status == QuoteStatus.Expired
            ? "Este orçamento expirou. Peça um novo à oficina."
            : "Este orçamento já foi respondido."
    });
}
