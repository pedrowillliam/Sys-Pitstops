using System.Security.Cryptography;

namespace SysPitstops.Api.Domain;

public static class PublicToken
{
    public const int Bytes = 32;

    public static string Create() =>
        Base64UrlEncode(RandomNumberGenerator.GetBytes(Bytes));

    private static string Base64UrlEncode(byte[] value) =>
        Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
