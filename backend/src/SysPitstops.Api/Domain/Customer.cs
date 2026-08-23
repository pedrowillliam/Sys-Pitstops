namespace SysPitstops.Api.Domain;

public class Customer
{
    public Guid Id { get; set; }
    public int WorkshopId { get; set; }
    public string Name { get; set; } = null!;
    public string Phone { get; set; } = null!;
    public string? Document { get; set; }
    public string? Email { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Workshop Workshop { get; set; } = null!;
    public ICollection<Vehicle> Vehicles { get; set; } = [];
}
