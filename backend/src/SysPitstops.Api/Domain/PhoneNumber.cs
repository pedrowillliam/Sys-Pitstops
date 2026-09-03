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

    [GeneratedRegex(@"\D")]
    private static partial Regex NonDigit();
}
