using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.Contracts.Plugins.Mcp;

/// <summary>The rules a tool, a resource and a prompt share: names, descriptions, nodes and values sent as text.</summary>
internal static class McpContribution
{
    private const int MAX_NAME = 64;

    public static string CheckName(string? name, string kind, string type)
    {
        name ??= string.Empty;

        if (
            name.Length is 0 or > MAX_NAME
            || !name.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-' or '.')
        )
            throw new InvalidOperationException(
                $"The MCP {kind} {type} is named '{name}': a name is 1 to {MAX_NAME} letters, digits, '_', '-' or '.', such as room_state."
            );

        return name;
    }

    public static void CheckDescription(string? description, string what, string says)
    {
        if (string.IsNullOrWhiteSpace(description))
            throw new InvalidOperationException(
                $"{what} has no description: the assistant picks it by what its description says ({says})."
            );
    }

    /// <summary>The nodes of the type's <c>[RequiresPermission]</c>; throws when it has none.</summary>
    public static ImmutableArray<string> Nodes(Type type, string what)
    {
        ImmutableArray<string> nodes =
        [
            .. (type.GetCustomAttribute<RequiresPermissionAttribute>()?.Nodes ?? []).Where(x =>
                !string.IsNullOrWhiteSpace(x)
            ),
        ];

        if (nodes.IsEmpty)
            throw new InvalidOperationException(
                $"{what} has no [RequiresPermission]: each needs a permission node, so staff are given it on purpose."
            );

        return nodes;
    }

    public static async Task<bool> HoldsAnyAsync(
        ImmutableArray<string> nodes,
        Func<string, CancellationToken, Task<bool>> holds,
        CancellationToken ct
    )
    {
        foreach (var node in nodes)
            if (await holds(node, ct).ConfigureAwait(false))
                return true;

        return false;
    }

    /// <summary>Refuses a property that can't be sent as text: only text, numbers, true or false and names (enums).</summary>
    public static void CheckScalars(JsonObject properties, string what)
    {
        foreach (var (name, node) in properties)
        {
            var types = McpToolDefinition.Types(node as JsonObject);

            if (types.Count == 0 || types.Any(x => x is "object" or "array"))
                throw new InvalidOperationException(
                    $"{what}'s argument {name} is not text, a number or true or false: an MCP client sends each as text."
                );
        }
    }

    /// <summary>
    /// Values sent as text (a URI template's, a prompt's arguments) as the JSON object their
    /// schema reads: a number for a number, true or false for a boolean. Each that does not fit,
    /// each unknown name and each required one missing is a sentence in <paramref name="problems"/>.
    /// </summary>
    public static JsonElement FromText(
        IReadOnlyDictionary<string, string> values,
        JsonObject schema,
        List<string> problems
    )
    {
        var properties = schema["properties"] as JsonObject ?? [];
        var names = properties.Select(x => x.Key).ToList();
        var args = new JsonObject();

        foreach (var (given, text) in values)
        {
            var name = names.FirstOrDefault(x =>
                string.Equals(x, given, StringComparison.OrdinalIgnoreCase)
            );

            if (name is null)
            {
                problems.Add(
                    names.Count == 0
                        ? $"There is no argument \"{given}\": it takes none."
                        : $"There is no argument \"{given}\"; the arguments are {string.Join(", ", names.Select(x => $"\"{x}\""))}."
                );

                continue;
            }

            var types = McpToolDefinition.Types(properties[name] as JsonObject);

            if (types.Contains("integer") || types.Contains("number"))
            {
                if (
                    !double.TryParse(
                        text,
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out var number
                    ) || (!types.Contains("number") && number != Math.Floor(number))
                )
                {
                    problems.Add(
                        $"\"{name}\" must be {(types.Contains("number") ? "a number" : "a whole number")}; it was \"{text}\"."
                    );

                    continue;
                }

                args[name] = types.Contains("number")
                    ? JsonValue.Create(number)
                    : JsonValue.Create((long)number);
            }
            else if (types.Contains("boolean"))
            {
                if (!bool.TryParse(text, out var flag))
                {
                    problems.Add($"\"{name}\" must be true or false; it was \"{text}\".");

                    continue;
                }

                args[name] = flag;
            }
            else
                args[name] = text;
        }

        foreach (
            var required in (schema["required"] as JsonArray ?? []).Select(x =>
                x!.GetValue<string>()
            )
        )
            if (!args.ContainsKey(required) && !problems.Any(x => x.Contains($"\"{required}\"")))
                problems.Add(
                    properties[required]?["description"]?.GetValue<string>() is { } words
                        ? $"\"{required}\" is required: {words}"
                        : $"\"{required}\" is required."
                );

        var element = JsonSerializer.SerializeToElement(args);

        if (problems.Count == 0)
            McpToolDefinition.Check(element, schema, string.Empty, problems);

        return element;
    }
}
