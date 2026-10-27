using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Turbo.Plugins;

/// <summary>
/// What a window patch is (nitro-next <c>docs/template-patches.md</c>, Operations): a template id
/// and ops, each naming an element and doing one of <c>set</c>, <c>remove</c>, <c>insert</c>,
/// <c>move</c>, <c>wrap</c> (a new element in its place, it inside), <c>bind</c> (a binding as
/// data, "A binding as data") or <c>arrange</c> (a post-layout hook as data, "An arrangement as data"). The client skips and reports what it can't apply; this refuses,
/// before a patch is saved, what no client could. A window the client draws without a template is
/// named by its frame id (<c>wired-setup</c>): its patch restyles that frame through the root's
/// <c>set</c>.
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
        "position",
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

    private static readonly string[] ACTIONS =
    [
        "set",
        "remove",
        "insert",
        "move",
        "wrap",
        "bind",
        "arrange",
    ];

    /// <summary>The fields an <c>arrange</c> op sets (nitro-next <c>templatePatchArrange.ts</c>, <c>TEMPLATE_ARRANGE_FIELDS</c>).</summary>
    private static readonly string[] ARRANGE_FIELDS = ["x", "y", "width", "height"];

    /// <summary>What an added element (<c>insert</c>'s <c>add</c>, <c>wrap</c>) gives beyond layout properties.</summary>
    private static readonly string[] ADDED =
    [
        "like",
        "name",
        "tag",
        "children",
        "bindAs",
        "margins",
    ];

    /// <summary>
    /// The binding fields a <c>bind</c> op may give and the JSON kind each takes (nitro-next
    /// <c>templatePatchBind.ts</c>, <c>TEMPLATE_BIND_FIELDS</c>); text with a placeholder stands for any.
    /// </summary>
    private static readonly Dictionary<string, JsonValueKind> BIND_FIELDS = new()
    {
        ["caption"] = JsonValueKind.String,
        ["htmlText"] = JsonValueKind.String,
        ["tooltip"] = JsonValueKind.String,
        ["asset"] = JsonValueKind.String,
        ["helpPage"] = JsonValueKind.String,
        ["visible"] = JsonValueKind.True,
        ["background"] = JsonValueKind.True,
        ["greyscale"] = JsonValueKind.True,
        ["disabled"] = JsonValueKind.True,
        ["selected"] = JsonValueKind.True,
        ["italic"] = JsonValueKind.True,
        ["underline"] = JsonValueKind.True,
        ["crop"] = JsonValueKind.True,
        ["color"] = JsonValueKind.Number,
        ["backgroundColor"] = JsonValueKind.Number,
        ["etchingColor"] = JsonValueKind.Number,
        ["alpha"] = JsonValueKind.Number,
        ["blend"] = JsonValueKind.Number,
        ["rotation"] = JsonValueKind.Number,
    };

    /// <summary>What is wrong with the template id and ops, a sentence each; none for a patch a client applies.</summary>
    public static List<string> Problems(string template, JsonElement ops)
    {
        var problems = new List<string>();

        if (!TemplatePattern().IsMatch(template) && !FramePattern().IsMatch(template))
            problems.Add(
                $"The template \"{template}\" is named <library>/<asset>, as habbo-toolbar-com/purse_xml, a plugin's window defined as data by <plugin>/windows/<name>, or a window drawn without a template by its frame id, as wired-setup."
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
                $"Op {n} does one of set, remove, insert, move, wrap, bind or arrange{(actions.Count > 1 ? $", not {string.Join(" and ", actions)}" : "")}."
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
                Added(
                    op,
                    "add",
                    $"Op {n} inserts the element its \"add\" object describes.",
                    n,
                    problems
                );
                break;
            case "wrap":
                if (op.TryGetProperty("element", out var wrapped) && wrapped.GetString() == "")
                    problems.Add(
                        $"Op {n} wraps the window itself (''), which stays the window: wrap a part of it."
                    );

                Added(
                    op,
                    "wrap",
                    $"Op {n}'s wrap is an object describing the element put in its place.",
                    n,
                    problems
                );
                break;
            case "move":
                Place(op, "move", n, problems);

                if (
                    op.GetProperty("move").GetString() is "before" or "after"
                    && !op.TryGetProperty("to", out _)
                )
                    problems.Add($"Op {n} moves its element \"to\" the one it goes beside.");
                break;
            case "bind":
                Bind(op, n, problems);
                break;
            case "arrange":
                Arrange(op, n, problems);
                break;
        }
    }

    /// <summary>An element an op adds: one "like" the template's, or with its "tag".</summary>
    private static void Added(
        JsonElement op,
        string action,
        string missing,
        int n,
        List<string> problems
    )
    {
        if (!op.TryGetProperty(action, out var added) || added.ValueKind != JsonValueKind.Object)
        {
            problems.Add(missing);

            return;
        }

        if (!added.TryGetProperty("like", out _) && !added.TryGetProperty("tag", out _))
            problems.Add(
                $"Op {n} adds an element \"like\" one the template has, or gives its \"tag\"."
            );

        Properties(added, $"Op {n}'s {action}", problems, ADDED);
    }

    private static void Bind(JsonElement op, int n, List<string> problems)
    {
        var items = Flag(op, "items", n, problems);
        var over = Flag(op, "override", n, problems);

        if (
            op.TryGetProperty("element", out var element)
            && element.ValueKind == JsonValueKind.String
            && element.GetString() == ""
            && !over
        )
            problems.Add(
                $"Op {n} binds the window itself (''), which is the code's: give it \"override\": true."
            );

        if (
            op.GetProperty("bind") is not { ValueKind: JsonValueKind.Object } bind
            || !bind.EnumerateObject().Any()
        )
        {
            problems.Add(
                $"Op {n}'s bind is an object of binding fields ({string.Join(", ", BIND_FIELDS.Keys)})."
            );

            return;
        }

        foreach (var field in bind.EnumerateObject())
        {
            if (!BIND_FIELDS.TryGetValue(field.Name, out var kind))
            {
                problems.Add(
                    $"Op {n}'s bind has {field.Name}, which is no binding field ({string.Join(", ", BIND_FIELDS.Keys)})."
                );

                continue;
            }

            var value = field.Value;

            if (value.ValueKind == JsonValueKind.String)
            {
                var text = value.GetString()!;
                var placeholders = PlaceholderPattern().Matches(text);

                foreach (Match placeholder in placeholders)
                {
                    var name = placeholder.Groups[1].Value;

                    // ${text.icon.${item.index}}: a placeholder is filled once, from the key as written, so
                    // the inner one is never filled and the outer names a key no text has.
                    if (name.Contains("${", StringComparison.Ordinal))
                        problems.Add(
                            $"Op {n}'s bind {field.Name} puts a placeholder inside another ({text}): placeholders are filled once and do not nest, so it names no key. Bind each row's own value (${{item.<key>}}) or one whole key."
                        );
                    else if (!RootedPattern().IsMatch(name) && !IsStyle($"${{{name}}}"))
                        problems.Add(
                            $"Op {n}'s bind {field.Name} names ${{{name}}}: a placeholder is ${{ctx.<key>}}, ${{item.<key>}}, ${{text.<key>}} or ${{style:<name>}}."
                        );
                    else if (name.StartsWith("item.", StringComparison.Ordinal) && !items)
                        problems.Add(
                            $"Op {n}'s bind {field.Name} names ${{{name}}}, which a row has: give the op \"items\": true."
                        );
                }

                // Text stands for a value of another kind only with a placeholder in it, or as one ("true", "4285229153").
                if (kind == JsonValueKind.String || placeholders.Count > 0 || Parses(text, kind))
                    continue;
            }
            else if (
                value.ValueKind == kind
                || (kind == JsonValueKind.True && value.ValueKind == JsonValueKind.False)
            )
                continue;

            problems.Add(
                $"Op {n}'s bind {field.Name} is {(kind == JsonValueKind.Number ? "a number (a colour 0xAARRGGBB, as 4285229153)" : kind == JsonValueKind.True ? "true or false" : "text")}."
            );
        }
    }

    private static void Arrange(JsonElement op, int n, List<string> problems)
    {
        if (
            op.GetProperty("arrange") is not { ValueKind: JsonValueKind.Object } arrange
            || !arrange.EnumerateObject().Any()
        )
        {
            problems.Add(
                $"Op {n}'s arrange is an object of {string.Join(", ", ARRANGE_FIELDS)}, each a number or a formula."
            );

            return;
        }

        foreach (var field in arrange.EnumerateObject())
        {
            if (!ARRANGE_FIELDS.Contains(field.Name))
            {
                problems.Add(
                    $"Op {n}'s arrange has {field.Name}, which it does not set ({string.Join(", ", ARRANGE_FIELDS)})."
                );

                continue;
            }

            if (field.Value.ValueKind == JsonValueKind.Number)
                continue;

            if (
                field.Value.ValueKind != JsonValueKind.String
                || string.IsNullOrWhiteSpace(field.Value.GetString())
            )
            {
                problems.Add($"Op {n}'s arrange {field.Name} is a number or a formula.");

                continue;
            }

            var formula = field.Value.GetString()!;

            if (TemplateArrangeFormula.Problem(formula) is { } problem)
                problems.Add($"Op {n}'s arrange {field.Name} ({formula}): {problem}.");
        }
    }

    private static bool Flag(JsonElement op, string name, int n, List<string> problems)
    {
        if (!op.TryGetProperty(name, out var flag))
            return false;

        if (flag.ValueKind is JsonValueKind.True or JsonValueKind.False)
            return flag.ValueKind == JsonValueKind.True;

        problems.Add($"Op {n}'s {name} is true or false.");

        return false;
    }

    private static bool Parses(string text, JsonValueKind kind) =>
        kind == JsonValueKind.True
            ? text is "true" or "false"
            : double.TryParse(
                text,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out _
            );

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
                // A layout's style is text ("9", the client's TemplateElement.style); a number is read the same.
                "style" or "dynamicStyle" => value.ValueKind
                    is JsonValueKind.String
                        or JsonValueKind.Number,
                _ when NUMBERS.Contains(property.Name) => value.ValueKind == JsonValueKind.Number,
                "hidden" => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
                "vars" or "params" => value.ValueKind == JsonValueKind.Object,
                "position" => IsPoint(value),
                "caption" or "like" or "name" or "tag" or "bindAs" => value.ValueKind
                    == JsonValueKind.String,
                "children" or "margins" => value.ValueKind == JsonValueKind.Array,
                _ => true,
            };

            if (!ok)
                problems.Add(
                    $"{what}'s {property.Name} is {(NUMBERS.Contains(property.Name) ? "a number (a colour 0xAARRGGBB, as 4285229153)" : property.Name == "hidden" ? "true or false" : property.Name is "vars" or "params" ? "an object" : property.Name is "children" or "margins" ? "a list" : property.Name == "position" ? "where the window first opens, { \"x\": 84, \"y\": 153 }" : "text")}."
                );
        }
    }

    /// <summary><c>{ "x": 84, "y": 153 }</c>: a window's first position, two numbers.</summary>
    private static bool IsPoint(JsonElement value) =>
        value.ValueKind == JsonValueKind.Object
        && value.TryGetProperty("x", out var x)
        && x.ValueKind == JsonValueKind.Number
        && value.TryGetProperty("y", out var y)
        && y.ValueKind == JsonValueKind.Number;

    private static bool IsStyle(string value) => StylePattern().IsMatch(value);

    // A plugin's window defined as data is <plugin>/windows/<name> (DataWindowSchema).
    [GeneratedRegex(@"^[a-z0-9][a-z0-9._-]*/(windows/)?[A-Za-z0-9_.-]+$")]
    private static partial Regex TemplatePattern();

    /// <summary>A frame id, as nitro-next's <c>skins.json</c> lists a window drawn without a template (<c>wired-setup</c>).</summary>
    [GeneratedRegex(@"^[a-z0-9][a-z0-9_-]*$")]
    private static partial Regex FramePattern();

    [GeneratedRegex(@"^\$\{style:[A-Za-z0-9._:-]+\}$")]
    private static partial Regex StylePattern();

    [GeneratedRegex(@"\$\{([^}]*)\}")]
    private static partial Regex PlaceholderPattern();

    [GeneratedRegex(@"^(ctx|item|text)\..+$")]
    private static partial Regex RootedPattern();
}
