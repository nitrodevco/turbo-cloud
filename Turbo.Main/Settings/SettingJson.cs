using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Turbo.Main.Settings;

/// <summary>
/// A setting's value as JSON, and as the configuration keys it stands for. Enums are written by
/// name, as <c>appsettings.json</c> writes them; text is written as it is, for staff to read.
/// </summary>
internal static class SettingJson
{
    public const char KEY_DELIMITER = ':';

    private static readonly JsonSerializerOptions OPTIONS = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter() },
    };

    public static string Write(object? value, Type type) =>
        JsonSerializer.Serialize(value, type, OPTIONS);

    /// <summary>
    /// The JSON read as the type, written back the way <see cref="Write"/> writes it. Throws
    /// <see cref="ArgumentException"/> when it isn't JSON, or isn't a value of the type.
    /// </summary>
    public static string Normalize(string json, Type type)
    {
        try
        {
            var value = JsonSerializer.Deserialize(json, type, OPTIONS);

            if (value is null && Nullable.GetUnderlyingType(type) is null && type.IsValueType)
                throw new ArgumentException("This setting needs a value.", nameof(json));

            return Write(value, type);
        }
        catch (JsonException ex)
        {
            throw new ArgumentException(
                $"That isn't a value this setting takes. {ex.Message}",
                nameof(json),
                ex
            );
        }
        catch (NotSupportedException ex)
        {
            throw new ArgumentException(
                $"That isn't a value this setting takes. {ex.Message}",
                nameof(json),
                ex
            );
        }
    }

    /// <summary>
    /// What configuration has to say for a list or map to come out as <paramref name="json"/>:
    /// the binder fills one on top of the items its class starts with
    /// (<paramref name="defaults"/>), so those can be kept or added to, never taken away. A list
    /// keeps its defaults first and says only what follows; a map says only what differs. Throws
    /// <see cref="ArgumentException"/> for a default left out.
    /// </summary>
    public static string BeyondDefaults(string json, string? defaults, string kind)
    {
        if (defaults is null)
            return json;

        using var start = JsonDocument.Parse(defaults);
        using var value = JsonDocument.Parse(json);

        if (
            kind == "list"
            && start.RootElement.ValueKind == JsonValueKind.Array
            && value.RootElement.ValueKind == JsonValueKind.Array
        )
        {
            var kept = start.RootElement.EnumerateArray().Select(x => x.GetRawText()).ToList();
            var items = value.RootElement.EnumerateArray().Select(x => x.GetRawText()).ToList();

            if (kept.Count == 0)
                return json;

            if (items.Count < kept.Count || !items.Take(kept.Count).SequenceEqual(kept))
                throw new ArgumentException(
                    $"This list always starts with its {kept.Count} default items ({string.Join(", ", kept)}): configuration can add to them, not take them away. Keep them first.",
                    nameof(json)
                );

            return $"[{string.Join(",", items.Skip(kept.Count))}]";
        }

        if (
            kind == "map"
            && start.RootElement.ValueKind == JsonValueKind.Object
            && value.RootElement.ValueKind == JsonValueKind.Object
        )
        {
            var kept = start
                .RootElement.EnumerateObject()
                .ToDictionary(x => x.Name, x => x.Value.GetRawText());
            var entries = value
                .RootElement.EnumerateObject()
                .ToDictionary(x => x.Name, x => x.Value.GetRawText());

            if (kept.Keys.FirstOrDefault(x => !entries.ContainsKey(x)) is { } missing)
                throw new ArgumentException(
                    $"{missing} is one of this map's defaults: configuration can change it, not take it away.",
                    nameof(json)
                );

            return "{"
                + string.Join(
                    ",",
                    entries
                        .Where(x => !kept.TryGetValue(x.Key, out var was) || was != x.Value)
                        .Select(x => $"{JsonSerializer.Serialize(x.Key)}:{x.Value}")
                )
                + "}";
        }

        return json;
    }

    /// <summary>
    /// The configuration keys the JSON value at the path stands for, as a JSON configuration file
    /// gives them: an object's properties and a list's items under the path, each with its own
    /// key.
    /// </summary>
    public static void Flatten(string path, JsonElement value, IDictionary<string, string?> into)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in value.EnumerateObject())
                    Flatten($"{path}{KEY_DELIMITER}{property.Name}", property.Value, into);

                break;
            case JsonValueKind.Array:
                var index = 0;

                foreach (var item in value.EnumerateArray())
                    Flatten(
                        $"{path}{KEY_DELIMITER}{index++.ToString(CultureInfo.InvariantCulture)}",
                        item,
                        into
                    );

                break;
            case JsonValueKind.String:
                into[path] = value.GetString();

                break;
            case JsonValueKind.True:
                into[path] = "true";

                break;
            case JsonValueKind.False:
                into[path] = "false";

                break;
            case JsonValueKind.Number:
                into[path] = value.GetRawText();

                break;
            default:
                into[path] = null;

                break;
        }
    }
}
