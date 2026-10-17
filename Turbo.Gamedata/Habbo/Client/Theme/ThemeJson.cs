using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace Turbo.Gamedata.Habbo.Client.Theme;

/// <summary>The few object operations Studio's theme build makes on its JSON, with JavaScript's meaning.</summary>
internal static class ThemeJson
{
    /// <summary>
    /// <c>{ ...target, ...source }</c> into <paramref name="target"/>: each field of the source set
    /// over the target's (in place where the target has it, after it where not), copied.
    /// </summary>
    public static void Assign(JsonObject target, JsonObject? source)
    {
        if (source is null)
            return;

        foreach (var (key, value) in source.ToList())
            target[key] = value?.DeepClone();
    }

    /// <summary>A copy of an object, or a new empty one for none.</summary>
    public static JsonObject Copy(JsonObject? source) => source?.DeepClone().AsObject() ?? [];

    /// <summary>Every string in a value, field names and values alike: what a search of its JSON text for a quoted name finds.</summary>
    public static HashSet<string> Strings(JsonNode? node)
    {
        var found = new HashSet<string>();

        void Visit(JsonNode? value)
        {
            switch (value)
            {
                case JsonObject obj:
                    foreach (var (key, entry) in obj)
                    {
                        _ = found.Add(key);
                        Visit(entry);
                    }

                    break;
                case JsonArray array:
                    foreach (var entry in array)
                        Visit(entry);

                    break;
                case JsonValue leaf when leaf.TryGetValue<string>(out var text):
                    _ = found.Add(text);
                    break;
            }
        }

        Visit(node);

        return found;
    }

    /// <summary>The object at a key of another, made where there is none (<c>(obj[key] ??= {})</c>).</summary>
    public static JsonObject Ensure(JsonObject parent, string key)
    {
        if (parent[key] is JsonObject existing)
            return existing;

        var created = new JsonObject();

        parent[key] = created;

        return created;
    }
}
