using System.Text.Json;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace PcMarket.Infrastructure.Persistence;

/// <summary>Shared EF converter/comparer for <c>Dictionary&lt;string,string&gt;</c> properties persisted as JSONB.</summary>
internal static class JsonbConversions
{
    private static readonly JsonSerializerOptions Options = new();

    public static readonly ValueConverter<Dictionary<string, string>, string> Dictionary = new(
        v => JsonSerializer.Serialize(v, Options),
        v => JsonSerializer.Deserialize<Dictionary<string, string>>(v, Options) ?? new Dictionary<string, string>());

    public static readonly ValueComparer<Dictionary<string, string>> DictionaryComparer = new(
        (a, b) => JsonSerializer.Serialize(a, Options) == JsonSerializer.Serialize(b, Options),
        v => JsonSerializer.Serialize(v, Options).GetHashCode(),
        v => JsonSerializer.Deserialize<Dictionary<string, string>>(JsonSerializer.Serialize(v, Options), Options)
             ?? new Dictionary<string, string>());
}
