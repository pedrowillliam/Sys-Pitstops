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
    string? MechanicName,
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

/// <summary>One row of the workshop-wide quote list. It carries the customer
/// phone and the token because the screen builds the wa.me link from them
/// (D-15) without a second round trip.</summary>
public record QuoteListItem(
    Guid Id,
    Guid ServiceOrderId,
    int ServiceOrderNumber,
    ServiceOrderStatus ServiceOrderStatus,
    QuoteStatus Status,
    decimal TotalAmount,
    string CustomerName,
    string CustomerPhone,
    string VehiclePlate,
    string VehicleDescription,
    string PublicToken,
    string SentByName,
    DateTimeOffset SentAt,
    DateTimeOffset ExpiresAt,
    DateTimeOffset? RespondedAt);
