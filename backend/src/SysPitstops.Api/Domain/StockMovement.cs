namespace SysPitstops.Api.Domain;

// The auditable truth for stock. Quantity is always positive; direction comes
// from MovementType (docs/data-model.md, section 4).
public class StockMovement
{
    public Guid Id { get; set; }
    public int WorkshopId { get; set; }
    public Guid PartId { get; set; }
    public MovementType MovementType { get; set; }
    public decimal Quantity { get; set; }
    public decimal? UnitCost { get; set; }
    public Guid? ServiceOrderId { get; set; }
    public Guid UserId { get; set; }
    public string? Note { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public Workshop Workshop { get; set; } = null!;
    public Part Part { get; set; } = null!;
    public ServiceOrder? ServiceOrder { get; set; }
    public User User { get; set; } = null!;
}
