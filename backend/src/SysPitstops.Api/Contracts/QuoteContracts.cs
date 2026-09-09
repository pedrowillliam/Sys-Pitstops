using System.ComponentModel.DataAnnotations;
using SysPitstops.Api.Domain;

namespace SysPitstops.Api.Contracts;

public record QuoteResponse(
    Guid Id,
    Guid ServiceOrderId,
    int ServiceOrderNumber,
    QuoteStatus Status,
    decimal TotalAmount,
    IReadOnlyList<QuoteItemSnapshot> Items,
    string PublicToken,
    string? RejectionReason,
    string SentByName,
    DateTimeOffset SentAt,
    DateTimeOffset ExpiresAt,
    DateTimeOffset? RespondedAt);

public record PublicQuoteResponse(
    int ServiceOrderNumber,
    string WorkshopName,
    string CustomerName,
    string VehicleDescription,
    string VehiclePlate,
    QuoteStatus Status,
    decimal TotalAmount,
    IReadOnlyList<QuoteItemSnapshot> Items,
    DateTimeOffset SentAt,
    DateTimeOffset ExpiresAt);

public record RejectQuoteRequest
{
    [StringLength(1000)]
    public string? Reason { get; init; }
}
