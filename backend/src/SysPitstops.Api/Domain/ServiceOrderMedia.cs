namespace SysPitstops.Api.Domain;

// StorageKey is a logical key resolved by IMediaStorage, never an absolute
// path, so the storage backend can change by configuration (D-25, D-26).
public class ServiceOrderMedia
{
    public Guid Id { get; set; }
    public Guid ServiceOrderId { get; set; }
    public string StorageKey { get; set; } = null!;
    public string ContentType { get; set; } = null!;
    public long? SizeBytes { get; set; }
    public string? Caption { get; set; }
    public Guid UploadedBy { get; set; }
    public DateTimeOffset UploadedAt { get; set; }

    public ServiceOrder ServiceOrder { get; set; } = null!;
    public User UploadedByUser { get; set; } = null!;
}
