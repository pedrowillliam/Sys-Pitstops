namespace SysPitstops.Api.Domain;

// QuantityOnHand is a read cache maintained by the application. Every change
// must be paired with a StockMovement row in the same transaction
// (see docs/data-model.md, section 4).
public class Part
{
    public Guid Id { get; set; }
    public int WorkshopId { get; set; }
    public string Sku { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public string Unit { get; set; } = "UN";
    public decimal SalePrice { get; set; }
    public decimal CostPrice { get; set; }
    public decimal QuantityOnHand { get; set; }
    public decimal MinQuantity { get; set; }
    public string? Location { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Workshop Workshop { get; set; } = null!;
}
