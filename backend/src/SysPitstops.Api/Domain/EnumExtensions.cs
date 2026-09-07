using System.Reflection;
using NpgsqlTypes;

namespace SysPitstops.Api.Domain;

public static class EnumExtensions
{
    public static string ToPgName<TEnum>(this TEnum value) where TEnum : struct, Enum
    {
        var member = typeof(TEnum).GetMember(value.ToString()).FirstOrDefault();
        return member?.GetCustomAttribute<PgNameAttribute>()?.PgName
            ?? value.ToString().ToUpperInvariant();
    }

    /// <summary>Reads a label such as IN_YARD back into its member. The label is
    /// the C# name in upper snake case, so dropping the underscores makes the
    /// two comparable. Ordinals are rejected on purpose: the contract publishes
    /// labels, and "2" reaching an endpoint is a mistake worth surfacing.</summary>
    public static bool TryParseLabel(Type enumType, string? label, out object? value)
    {
        value = null;

        if (string.IsNullOrWhiteSpace(label))
        {
            return false;
        }

        var normalized = label.Replace("_", string.Empty);

        foreach (var name in Enum.GetNames(enumType))
        {
            if (string.Equals(name, normalized, StringComparison.OrdinalIgnoreCase))
            {
                value = Enum.Parse(enumType, name);
                return true;
            }
        }

        return false;
    }
}
