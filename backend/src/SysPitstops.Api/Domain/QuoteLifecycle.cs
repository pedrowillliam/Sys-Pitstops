using System.Linq.Expressions;
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

    /// <summary>Expiry is decided on read (D-14), so the stored status lags:
    /// a quote past its date is still SENT in the table. Comparing the column
    /// alone would list those as awaiting an answer and would never find them
    /// under EXPIRED, so the clock goes into the predicate.</summary>
    public static Expression<Func<Quote, bool>> HasStatus(
        QuoteStatus status, DateTimeOffset now) => status switch
    {
        QuoteStatus.Sent => q => q.Status == QuoteStatus.Sent && q.ExpiresAt > now,
        QuoteStatus.Expired => q => q.Status == QuoteStatus.Expired
            || (q.Status == QuoteStatus.Sent && q.ExpiresAt <= now),
        _ => q => q.Status == status
    };

    public static bool AwaitsAnswer(Quote quote) => quote.Status == QuoteStatus.Sent;

    public static IReadOnlyList<QuoteItemSnapshot> ReadItems(Quote quote) =>
        JsonSerializer.Deserialize<List<QuoteItemSnapshot>>(quote.ItemsSnapshot, Json) ?? [];
}
