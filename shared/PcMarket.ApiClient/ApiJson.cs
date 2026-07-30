using System.Text.Json;
using System.Text.Json.Serialization;

namespace PcMarket.ApiClient;

/// <summary>Shared serializer options matching the API: web (camelCase) conventions with enums as names.</summary>
public static class ApiJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };
}
