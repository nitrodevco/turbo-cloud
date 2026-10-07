using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Turbo.Gamedata.Products;

/// <summary>
/// Habbo's product data: <c>{ "productdata": { "product": [ { code, name, description } ] } }</c>.
/// A code is read as text whatever Habbo wrote (one of its codes is the number 25, which the client
/// keys as <c>"25"</c> all the same); a name or description may be null, and stays so. A code given
/// twice keeps its first place and its last values. A code is trimmed: Habbo lists a few with
/// stray spaces or tabs (<c>"avatar_effect27 "</c> beside <c>"avatar_effect27"</c>), which no
/// offer's name key could match, and which MySQL would take for the code without them.
/// </summary>
internal static class ProductDataFile
{
    private static readonly JsonWriterOptions WRITER_OPTIONS = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static Dictionary<string, (string? Name, string? Description)> Parse(byte[] data)
    {
        var products = new Dictionary<string, (string?, string?)>(StringComparer.Ordinal);
        JsonNode? root;

        try
        {
            root = JsonNode.Parse(data);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("The product data is not JSON.", ex);
        }

        if (root?["productdata"]?["product"] is not JsonArray list)
            return products;

        foreach (var node in list.OfType<JsonObject>())
        {
            if (Text(node["code"]) is not { } raw || raw.Trim() is not { Length: > 0 } code)
                continue;

            // A padded copy of a code Habbo also lists as it is gives way to that one.
            if (raw != code && products.ContainsKey(code))
                continue;

            products[code] = (Text(node["name"]), Text(node["description"]));
        }

        return products;
    }

    /// <summary>The products as the file, by code in ordinal order, UTF-8.</summary>
    public static byte[] Write(
        IEnumerable<(string Code, string? Name, string? Description)> products
    )
    {
        using var stream = new MemoryStream();

        using (var writer = new Utf8JsonWriter(stream, WRITER_OPTIONS))
        {
            writer.WriteStartObject();
            writer.WritePropertyName("productdata");
            writer.WriteStartObject();
            writer.WritePropertyName("product");
            writer.WriteStartArray();

            foreach (
                var (code, name, description) in products.OrderBy(
                    x => x.Code,
                    StringComparer.Ordinal
                )
            )
            {
                writer.WriteStartObject();
                writer.WriteString("code", code);
                writer.WriteString("name", name);
                writer.WriteString("description", description);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        return stream.ToArray();
    }

    private static string? Text(JsonNode? node) =>
        node switch
        {
            null => null,
            JsonValue value when value.GetValueKind() == JsonValueKind.String =>
                value.GetValue<string>(),
            JsonValue value when value.GetValueKind() == JsonValueKind.Null => null,
            _ => node.ToJsonString(),
        };
}
