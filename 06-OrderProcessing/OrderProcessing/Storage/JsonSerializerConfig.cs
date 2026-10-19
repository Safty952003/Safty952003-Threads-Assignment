
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OrderProcessing.Storage;

public static class JsonSerializerConfig
{
    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters =
        {
            new JsonStringEnumConverter()
        }
    };
}
