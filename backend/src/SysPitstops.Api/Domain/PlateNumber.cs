using System.Text.RegularExpressions;

namespace SysPitstops.Api.Domain;

// Plates are stored normalized so that "abc-1d23" and "ABC1D23" collide on
// uq_vehicles_plate instead of creating two records for the same car.
public static partial class PlateNumber
{
    public static string Normalize(string value) =>
        NonAlphanumeric().Replace(value ?? string.Empty, string.Empty).ToUpperInvariant();

    // Covers both the old layout (ABC1234) and Mercosul (ABC1D23): the fifth
    // character is the only one that may be either a letter or a digit.
    public static bool IsValid(string normalized) => Layout().IsMatch(normalized);

    [GeneratedRegex(@"[^A-Za-z0-9]")]
    private static partial Regex NonAlphanumeric();

    [GeneratedRegex(@"^[A-Z]{3}[0-9][0-9A-Z][0-9]{2}$")]
    private static partial Regex Layout();
}
