namespace SysPitstops.Api.Domain;

// What the server accepts as a photo of the laudo. Pure on purpose: the rule
// is the same wherever the file ends up (D-25 keeps the destination behind
// IMediaStorage), and the browser compresses before sending.
public static class MediaRules
{
    public const long MaxBytes = 5 * 1024 * 1024;

    private static readonly Dictionary<string, string> Extensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/jpeg"] = ".jpg",
        ["image/png"] = ".png",
        ["image/webp"] = ".webp"
    };

    public static IReadOnlyCollection<string> AllowedTypes => Extensions.Keys;

    public static bool IsAllowedType(string? contentType) =>
        contentType is not null && Extensions.ContainsKey(Normalize(contentType));

    /// <summary>The extension the key carries. It is cosmetic — what the
    /// browser is told on the way back is the stored content type — but it
    /// keeps the folder readable for whoever opens it looking for a file.</summary>
    public static string ExtensionFor(string contentType) =>
        Extensions.TryGetValue(Normalize(contentType), out var extension) ? extension : ".bin";

    /// <summary>Browsers send "image/jpeg; charset=..." now and then, and the
    /// parameters are not part of the type.</summary>
    public static string Normalize(string contentType) =>
        contentType.Split(';')[0].Trim();
}
