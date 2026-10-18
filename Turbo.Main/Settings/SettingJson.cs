using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Turbo.Main.Settings;

/// <summary>
/// A setting's value as JSON, and as the configuration keys it stands for. Enums are written by
/// name, as <c>appsettings.json</c> writes them; text is written as it is, for staff to read.
/// </summary>
internal static partial class SettingJson
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
    /// A JSON text value without one pair of quotes (<c>"</c> or <c>'</c>) wrapped around the
    /// whole of it, as typed by someone who thought the field wanted them; anything else as it is.
    /// </summary>
    public static string Unquoted(string json)
    {
        if (JsonSerializer.Deserialize<string>(json, OPTIONS) is not { Length: >= 2 } text)
            return json;

        var quote = text[0];

        return quote is '"' or '\'' && text[^1] == quote
            ? Write(text[1..^1], typeof(string))
            : json;
    }

    /// <summary>
    /// Throws <see cref="ArgumentException"/> unless the JSON text value is empty or a whole web
    /// address: <c>http</c> or <c>https</c>, with a host. Placeholders (<c>{name}</c>,
    /// <c>%name%</c>) are read as any text.
    /// </summary>
    public static void CheckUrl(string json)
    {
        var text = JsonSerializer.Deserialize<string>(json, OPTIONS);

        if (string.IsNullOrEmpty(text))
            return;

        var filled = Placeholder().Replace(text, "x");

        if (
            filled.Any(char.IsWhiteSpace)
            || !Uri.TryCreate(filled, UriKind.Absolute, out var url)
            || (url.Scheme != Uri.UriSchemeHttp && url.Scheme != Uri.UriSchemeHttps)
            // Uri takes https:/host and https:host as https://host.
            || !filled.StartsWith(url.Scheme + "://", StringComparison.OrdinalIgnoreCase)
            || url.Host.Length == 0
        )
            throw new ArgumentException(
                $"{text} isn't a web address: give the whole of it, such as https://example.com/, or leave it empty.",
                nameof(json)
            );
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
            // An empty list or map is its key with no value, as a JSON file gives it: a list
            // the panel empties is empty, not the items its class starts with.
            case JsonValueKind.Object or JsonValueKind.Array when IsEmpty(value):
                into[path] = null;

                break;
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

    private static bool IsEmpty(JsonElement value) =>
        value.ValueKind == JsonValueKind.Array
            ? value.GetArrayLength() == 0
            : !value.EnumerateObject().Any();

    // What a setting fills in itself: {roomId}, %libname%.
    [GeneratedRegex(@"\{[^{}]*\}|%[^%]*%")]
    private static partial Regex Placeholder();
}
