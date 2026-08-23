namespace SysPitstops.Api.Domain;

// OwnerId is the CURRENT owner. Historical ownership lives in the customer
// snapshot on each service order (see docs/decisions.md, D-08).
public class Vehicle
{
    public Guid Id { get; set; }
    public int WorkshopId { get; set; }
    public Guid OwnerId { get; set; }
    public string Plate { get; set; } = null!;
    public string Brand { get; set; } = null!;
    public string Model { get; set; } = null!;
    public int? ModelYear { get; set; }
    public string? Color { get; set; }
    public string? Vin { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Workshop Workshop { get; set; } = null!;
    public Customer Owner { get; set; } = null!;
}
