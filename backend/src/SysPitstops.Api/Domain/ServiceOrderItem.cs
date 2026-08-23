namespace SysPitstops.Api.Domain;

// UnitPrice and Description are FROZEN at insert time. Changing a part price
// later must never alter historical revenue (D-07). PartId exists only for
// traceability and stock write-off.
public class ServiceOrderItem
{
    public Guid Id { get; set; }
    public Guid ServiceOrderId { get; set; }
    public ItemType ItemType { get; set; }
    public Guid? PartId { get; set; }
    public string Description { get; set; } = null!;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public ServiceOrder ServiceOrder { get; set; } = null!;
    public Part? Part { get; set; }
    public User CreatedByUser { get; set; } = null!;
}
