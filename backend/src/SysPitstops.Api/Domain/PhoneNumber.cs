using System.Text.RegularExpressions;

namespace SysPitstops.Api.Domain;

// Only digits are stored. The phone is what the attendant uses to find the
// customer and what feeds the wa.me link of D-15, so formatting is a display
// concern and must not reach the database.
public static partial class PhoneNumber
{
    public static string Normalize(string value) =>
        NonDigit().Replace(value ?? string.Empty, string.Empty);

    // Landline with area code is 10 digits, mobile is 11.
    public static bool IsValid(string normalized) =>
        normalized.Length is 10 or 11 && normalized[0] != '0';

    /// <summary>A mobile is 11 digits with the ninth digit in front of the
    /// number. Only a mobile has WhatsApp, so this is what decides whether the
    /// quote of D-15 can be sent by message or only by a copied link — a
    /// customer with a landline is still a customer, and stays registered.</summary>
    public static bool IsMobile(string normalized) =>
        IsValid(normalized) && normalized.Length == 11 && normalized[2] == '9';

    /// <summary>Drops a pasted +55. Only applied on save: the search box takes
    /// partial input, where a leading 55 is a digit like any other.</summary>
    public static string StripCountryCode(string normalized) =>
        normalized.Length is 12 or 13 && normalized.StartsWith("55", StringComparison.Ordinal)
            ? normalized[2..]
            : normalized;

    [GeneratedRegex(@"\D")]
    private static partial Regex NonDigit();
}
