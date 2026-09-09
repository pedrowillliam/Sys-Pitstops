using System.Text.Json;
using System.Text.Json.Serialization;

namespace SysPitstops.Api.Domain;

public record QuoteItemSnapshot(
    ItemType ItemType,
    string Description,
    decimal Quantity,
    decimal UnitPrice,
    decimal Total);

public static class QuoteLifecycle
{

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper) }
    };

    public static bool ApplyExpiry(Quote quote, DateTimeOffset? now = null)
    {
        if (quote.Status != QuoteStatus.Sent || (now ?? DateTimeOffset.UtcNow) < quote.ExpiresAt)
        {
            return false;
        }

        quote.Status = QuoteStatus.Expired;
        return true;
    }

    public static bool AwaitsAnswer(Quote quote) => quote.Status == QuoteStatus.Sent;

    public static IReadOnlyList<QuoteItemSnapshot> ReadItems(Quote quote) =>
        JsonSerializer.Deserialize<List<QuoteItemSnapshot>>(quote.ItemsSnapshot, Json) ?? [];
}
