namespace SysPitstops.Api.Domain;

// ItemsSnapshot freezes what the customer actually saw and approved (D-10).
// TotalAmount is the one stored total in the system (D-09).
// PublicToken must come from a CSPRNG and the route is rate limited (D-21).
public class Quote
{
    public Guid Id { get; set; }
    public Guid ServiceOrderId { get; set; }
    public string PublicToken { get; set; } = null!;
    public QuoteStatus Status { get; set; } = QuoteStatus.Sent;
    public string ItemsSnapshot { get; set; } = null!;
    public decimal TotalAmount { get; set; }
    public string? RejectionReason { get; set; }
    public Guid SentBy { get; set; }
    public DateTimeOffset SentAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RespondedAt { get; set; }

    public ServiceOrder ServiceOrder { get; set; } = null!;
    public User SentByUser { get; set; } = null!;
}
