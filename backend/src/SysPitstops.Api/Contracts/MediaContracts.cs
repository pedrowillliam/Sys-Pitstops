using System.ComponentModel.DataAnnotations;

namespace SysPitstops.Api.Contracts;

public record ServiceOrderMediaResponse(
    Guid Id,
    Guid ServiceOrderId,
    string ContentType,
    long? SizeBytes,
    string? Caption,
    string UploadedByName,
    DateTimeOffset UploadedAt);

/// <summary>The file rides in the multipart body; only the caption is a field.
/// StorageKey never leaves the server — the browser reaches the image through
/// the content route, which is what keeps the destination swappable (D-25).</summary>
public record UploadMediaRequest
{
    [Required(ErrorMessage = "O arquivo é obrigatório.")]
    public IFormFile File { get; init; } = null!;

    [StringLength(200)]
    public string? Caption { get; init; }
}
