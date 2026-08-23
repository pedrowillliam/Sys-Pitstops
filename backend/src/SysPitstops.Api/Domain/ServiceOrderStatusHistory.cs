namespace SysPitstops.Api.Domain;

// Feeds the average execution time KPI (IN_PROGRESS -> READY). Without this
// table the indicator could not be computed retroactively.
public class ServiceOrderStatusHistory
{
    public Guid Id { get; set; }
    public Guid ServiceOrderId { get; set; }
    public ServiceOrderStatus? FromStatus { get; set; }
    public ServiceOrderStatus ToStatus { get; set; }
    public Guid ChangedBy { get; set; }
    public string? Note { get; set; }
    public DateTimeOffset ChangedAt { get; set; }

    public ServiceOrder ServiceOrder { get; set; } = null!;
    public User ChangedByUser { get; set; } = null!;
}
