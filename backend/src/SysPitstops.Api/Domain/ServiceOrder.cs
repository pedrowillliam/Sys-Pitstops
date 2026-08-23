namespace SysPitstops.Api.Domain;

// CustomerId is a SNAPSHOT of who owned the vehicle when the order was opened.
// Never resolve the customer through Vehicle.OwnerId in reports (D-08).
// The total is calculated, never stored (D-09).
public class ServiceOrder
{
    public Guid Id { get; set; }
    public int WorkshopId { get; set; }
    public int Number { get; set; }
    public Guid VehicleId { get; set; }
    public Guid CustomerId { get; set; }
    public Guid? MechanicId { get; set; }
    public Guid CreatedBy { get; set; }
    public ServiceOrderStatus Status { get; set; } = ServiceOrderStatus.Requested;
    public int? Mileage { get; set; }
    public string? ReportedIssue { get; set; }
    public string? Diagnosis { get; set; }
    public decimal DiscountAmount { get; set; }
    public string? ApprovalWaivedNote { get; set; }
    public DateTimeOffset? ScheduledAt { get; set; }
    public DateTimeOffset OpenedAt { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Workshop Workshop { get; set; } = null!;
    public Vehicle Vehicle { get; set; } = null!;
    public Customer Customer { get; set; } = null!;
    public User? Mechanic { get; set; }
    public User CreatedByUser { get; set; } = null!;
    public ICollection<ServiceOrderItem> Items { get; set; } = [];
    public ICollection<ServiceOrderStatusHistory> StatusHistory { get; set; } = [];
    public ICollection<ServiceOrderMedia> Media { get; set; } = [];
    public ICollection<Quote> Quotes { get; set; } = [];
}
