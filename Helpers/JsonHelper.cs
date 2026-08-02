using System.Text.Json;

namespace Insgram_Manager.Helpers;

/// <summary>
/// Utility helper for JSON serialization and deserialization with consistent options.
/// </summary>
public static class JsonHelper
{
    private static readonly JsonSerializerOptions DefaultOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public static string Serialize<T>(T value) =>
        JsonSerializer.Serialize(value, DefaultOptions);

    public static string SerializeIndented<T>(T value)
    {
        var options = new JsonSerializerOptions(DefaultOptions) { WriteIndented = true };
        return JsonSerializer.Serialize(value, options);
    }

    public static T? Deserialize<T>(string json) =>
        JsonSerializer.Deserialize<T>(json, DefaultOptions);

    public static T? Deserialize<T>(Stream stream) =>
        JsonSerializer.Deserialize<T>(stream, DefaultOptions);

    public static bool TryDeserialize<T>(string json, out T? result)
    {
        try
        {
            result = JsonSerializer.Deserialize<T>(json, DefaultOptions);
            return true;
        }
        catch (JsonException)
        {
            result = default;
            return false;
        }
    }
}
