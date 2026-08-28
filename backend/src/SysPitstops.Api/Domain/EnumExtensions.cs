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
}
