using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Turbo.Plugins;

/// <summary>
/// What a window patch is (nitro-next <c>docs/template-patches.md</c>, Operations): a template id
/// and ops, each naming an element and doing one of <c>set</c>, <c>remove</c>, <c>insert</c> or
/// <c>move</c>. The client skips and reports what it can't apply; this refuses, before a patch is
/// saved, what no client could.
/// </summary>
internal static partial class TemplatePatchSchema
{
    /// <summary>The layout properties <c>set</c> (and an added element) may give.</summary>
    private static readonly HashSet<string> PROPERTIES =
    [
        "x",
        "y",
        "width",
        "height",
        "style",
        "dynamicStyle",
        "caption",
        "hidden",
        "color",
        "background",
        "blend",
        "vars",
        "params",
    ];

    private static readonly HashSet<string> NUMBERS =
    [
        "x",
        "y",
        "width",
        "height",
        "style",
        "dynamicStyle",
        "color",
        "blend",
    ];

    private static readonly string[] PLACES = ["before", "after", "first", "last"];

    private static readonly string[] ACTIONS = ["set", "remove", "insert", "move"];

    /// <summary>What is wrong with the template id and ops, a sentence each; none for a patch a client applies.</summary>
    public static List<string> Problems(string template, JsonElement ops)
    {
        var problems = new List<string>();

        if (!TemplatePattern().IsMatch(template))
            problems.Add(
                $"The template \"{template}\" is named <library>/<asset>, as habbo-toolbar-com/purse_xml."
            );

        if (ops.ValueKind != JsonValueKind.Array || ops.GetArrayLength() == 0)
        {
            problems.Add("The ops are a list of at least one op.");

            return problems;
        }

        var i = 0;

        foreach (var op in ops.EnumerateArray())
            Check(op, ++i, problems);

        return problems;
    }

    private static void Check(JsonElement op, int n, List<string> problems)
    {
        if (op.ValueKind != JsonValueKind.Object)
        {
            problems.Add($"Op {n} is an object.");

            return;
        }

        if (
            !op.TryGetProperty("element", out var element)
            || element.ValueKind != JsonValueKind.String
        )
            problems.Add(
                $"Op {n} names its element: \"element\": \"help_button\" ('' for the window itself)."
            );

        var actions = ACTIONS.Where(x => op.TryGetProperty(x, out _)).ToList();

        if (actions.Count != 1)
        {
            problems.Add(
                $"Op {n} does one of set, remove, insert or move{(actions.Count > 1 ? $", not {string.Join(" and ", actions)}" : "")}."
            );

            return;
        }

        switch (actions[0])
        {
            case "set":
                if (op.GetProperty("set") is not { ValueKind: JsonValueKind.Object } set)
                    problems.Add($"Op {n}'s set is an object of layout properties.");
                else
                    Properties(set, $"Op {n}'s set", problems);
                break;
            case "remove":
                if (op.GetProperty("remove").ValueKind != JsonValueKind.True)
                    problems.Add($"Op {n}'s remove is true.");
                break;
            case "insert":
                Place(op, "insert", n, problems);

                if (op.TryGetProperty("add", out var add) && add.ValueKind == JsonValueKind.Object)
                {
                    if (!add.TryGetProperty("like", out _) && !add.TryGetProperty("tag", out _))
                        problems.Add(
                            $"Op {n} adds an element \"like\" one the template has, or gives its \"tag\"."
                        );

                    Properties(add, $"Op {n}'s add", problems, "like", "name", "tag", "children");
                }
                else
                    problems.Add($"Op {n} inserts the element its \"add\" object describes.");
                break;
            case "move":
                Place(op, "move", n, problems);

                if (
                    op.GetProperty("move").GetString() is "before" or "after"
                    && !op.TryGetProperty("to", out _)
                )
                    problems.Add($"Op {n} moves its element \"to\" the one it goes beside.");
                break;
        }
    }

    private static void Place(JsonElement op, string action, int n, List<string> problems)
    {
        if (
            op.GetProperty(action) is not { ValueKind: JsonValueKind.String } place
            || !PLACES.Contains(place.GetString())
        )
            problems.Add($"Op {n}'s {action} is before, after, first or last.");
    }

    private static void Properties(
        JsonElement properties,
        string what,
        List<string> problems,
        params string[] more
    )
    {
        foreach (var property in properties.EnumerateObject())
        {
            if (!PROPERTIES.Contains(property.Name) && !more.Contains(property.Name))
            {
                problems.Add(
                    $"{what} has {property.Name}, which is no layout property ({string.Join(", ", PROPERTIES)})."
                );

                continue;
            }

            var value = property.Value;

            // A theme's ${style:name} stands for any value.
            if (value.ValueKind == JsonValueKind.String && IsStyle(value.GetString()!))
                continue;

            var ok = property.Name switch
            {
                _ when NUMBERS.Contains(property.Name) => value.ValueKind == JsonValueKind.Number,
                "hidden" => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
                "vars" or "params" => value.ValueKind == JsonValueKind.Object,
                "caption" or "like" or "name" or "tag" => value.ValueKind == JsonValueKind.String,
                _ => true,
            };

            if (!ok)
                problems.Add(
                    $"{what}'s {property.Name} is {(NUMBERS.Contains(property.Name) ? "a number (a colour 0xAARRGGBB, as 4285229153)" : property.Name == "hidden" ? "true or false" : property.Name is "vars" or "params" ? "an object" : "text")}."
                );
        }
    }

    private static bool IsStyle(string value) => StylePattern().IsMatch(value);

    [GeneratedRegex(@"^[a-z0-9][a-z0-9._-]*/[A-Za-z0-9_.-]+$")]
    private static partial Regex TemplatePattern();

    [GeneratedRegex(@"^\$\{style:[A-Za-z0-9._:-]+\}$")]
    private static partial Regex StylePattern();
}
