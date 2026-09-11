using Microsoft.Extensions.Options;
using SysPitstops.Api.Domain;

namespace SysPitstops.Api.Storage;

public class MediaOptions
{
    public const string SectionName = "Media";

    /// <summary>Relative paths resolve from the content root, so the default
    /// keeps the files next to the application instead of wherever the process
    /// happened to be started from.</summary>
    public string RootPath { get; set; } = "media";
}

/// <summary>
/// The development implementation of D-25. On Render the disk is ephemeral and
/// would drop every photo at each deploy, which is exactly why D-26 sends
/// production to the Supabase bucket — this class is not meant to go there.
/// </summary>
public class LocalDiskMediaStorage : IMediaStorage
{
    private readonly string _root;

    public LocalDiskMediaStorage(IOptions<MediaOptions> options, IHostEnvironment environment)
    {
        var configured = options.Value.RootPath;

        _root = Path.IsPathRooted(configured)
            ? configured
            : Path.Combine(environment.ContentRootPath, configured);
    }

    public async Task<string> SaveAsync(Stream content, string contentType, CancellationToken ct)
    {
        // Foldered by month so that a year of photos does not land in a single
        // directory, and the key stays sortable by eye.
        var now = DateTimeOffset.UtcNow;
        var folder = $"{now:yyyy}/{now:MM}";
        var key = $"{folder}/{Guid.NewGuid():N}{MediaRules.ExtensionFor(contentType)}";

        var destination = Resolve(key);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);

        await using var file = File.Create(destination);
        await content.CopyToAsync(file, ct);

        return key;
    }

    public Task<Stream?> OpenAsync(string storageKey, CancellationToken ct)
    {
        var path = Resolve(storageKey);

        return Task.FromResult<Stream?>(
            File.Exists(path) ? File.OpenRead(path) : null);
    }

    public Task DeleteAsync(string storageKey, CancellationToken ct)
    {
        var path = Resolve(storageKey);

        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    /// <summary>The key comes from the database, and a row carrying "../" would
    /// otherwise read and delete files outside the folder. Resolving and then
    /// checking the prefix is what keeps the key logical.</summary>
    private string Resolve(string storageKey)
    {
        var full = Path.GetFullPath(Path.Combine(_root, storageKey));
        var root = Path.GetFullPath(_root) + Path.DirectorySeparatorChar;

        if (!full.StartsWith(root, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Chave de mídia inválida: {storageKey}");
        }

        return full;
    }
}
