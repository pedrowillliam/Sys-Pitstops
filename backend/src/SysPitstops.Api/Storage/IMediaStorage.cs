namespace SysPitstops.Api.Storage;

/// <summary>
/// Where the photos of the laudo live. D-25 put this interface between the
/// upload and the destination so that changing the destination is a new class,
/// not a rewrite: development writes to disk, and D-26 sends production to the
/// Supabase bucket.
/// <para>
/// The key returned is logical and goes into
/// <c>service_order_media.storage_key</c> — never an absolute path, or the rows
/// would stop resolving the moment the destination changed.
/// </para>
/// </summary>
public interface IMediaStorage
{
    Task<string> SaveAsync(Stream content, string contentType, CancellationToken ct);

    /// <summary>Null when the key resolves to nothing — a file removed outside
    /// the application, or a row left behind by a storage that was swapped.</summary>
    Task<Stream?> OpenAsync(string storageKey, CancellationToken ct);

    Task DeleteAsync(string storageKey, CancellationToken ct);
}
