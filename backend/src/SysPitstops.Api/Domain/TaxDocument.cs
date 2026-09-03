using System.Text.RegularExpressions;

namespace SysPitstops.Api.Domain;

// CPF or CNPJ, stored as digits only. The document is optional on a customer,
// but when it is filled it goes on the invoice the workshop hands over, so a
// typo has to be caught at the door rather than three weeks later.
public static partial class TaxDocument
{
    public static string Normalize(string value) =>
        NonDigit().Replace(value ?? string.Empty, string.Empty);

    public static bool IsValid(string normalized) => normalized.Length switch
    {
        11 => IsValidCpf(normalized),
        14 => IsValidCnpj(normalized),
        _ => false
    };

    private static bool IsValidCpf(string digits)
    {
        if (digits.All(c => c == digits[0]))
        {
            return false;
        }

        var first = CpfCheckDigit(digits, 9, 10);
        var second = CpfCheckDigit(digits, 10, 11);

        return digits[9] == first && digits[10] == second;
    }

    private static char CpfCheckDigit(string digits, int length, int startWeight)
    {
        var sum = 0;
        for (var i = 0; i < length; i++)
        {
            sum += (digits[i] - '0') * (startWeight - i);
        }

        var remainder = sum * 10 % 11;
        return (char)('0' + (remainder == 10 ? 0 : remainder));
    }

    private static bool IsValidCnpj(string digits)
    {
        if (digits.All(c => c == digits[0]))
        {
            return false;
        }

        ReadOnlySpan<int> firstWeights = [5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];
        ReadOnlySpan<int> secondWeights = [6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];

        return digits[12] == CnpjCheckDigit(digits, firstWeights)
            && digits[13] == CnpjCheckDigit(digits, secondWeights);
    }

    private static char CnpjCheckDigit(string digits, ReadOnlySpan<int> weights)
    {
        var sum = 0;
        for (var i = 0; i < weights.Length; i++)
        {
            sum += (digits[i] - '0') * weights[i];
        }

        var remainder = sum % 11;
        return (char)('0' + (remainder < 2 ? 0 : 11 - remainder));
    }

    [GeneratedRegex(@"\D")]
    private static partial Regex NonDigit();
}
