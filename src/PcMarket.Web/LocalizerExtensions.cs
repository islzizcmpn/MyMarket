using Microsoft.Extensions.Localization;

namespace PcMarket.Web;

public static class LocalizerExtensions
{
    /// <summary>Looks up the display name for an enum value under the key <c>Enum.&lt;Type&gt;.&lt;Value&gt;</c>, so
    /// order and payment states read as words rather than as C# identifiers. A value with no entry in the resource
    /// files falls back to its identifier, which is what would have been rendered anyway.</summary>
    public static string EnumName<TEnum>(this IStringLocalizer localizer, TEnum value)
        where TEnum : struct, Enum
    {
        var localized = localizer[$"Enum.{typeof(TEnum).Name}.{value}"];
        return localized.ResourceNotFound ? value.ToString()! : localized.Value;
    }
}
