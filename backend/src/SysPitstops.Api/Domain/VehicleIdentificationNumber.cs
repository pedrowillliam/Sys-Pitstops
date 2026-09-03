using System.Text.RegularExpressions;

namespace SysPitstops.Api.Domain;

// The chassis number. Optional on the vehicle, but when informed it has to
// look like a VIN: 17 characters with I, O and Q excluded by the standard so
// they are never confused with 1 and 0.
public static partial class VehicleIdentificationNumber
{
    public static string Normalize(string value) =>
        (value ?? string.Empty).Trim().ToUpperInvariant();

    public static bool IsValid(string normalized) => Layout().IsMatch(normalized);

    [GeneratedRegex(@"^[A-HJ-NPR-Z0-9]{17}$")]
    private static partial Regex Layout();
}
