using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Turbo.Plugins;

/// <summary>
/// What a window defined as data is (nitro-next <c>docs/template-patches.md</c>, "Windows as data"):
/// a plugin's <c>wwwroot/windows/&lt;name&gt;.json</c>, a template in the shape the client's template
/// bundles carry, what its buttons do (open an in-client link, close the window) and where players
/// open it from (a me menu entry, a toolbar icon). The client skips what it can't draw; this
/// refuses, before a window is saved, what no client could.
/// </summary>
internal static partial class DataWindowSchema
{
    /// <summary>The folder of a plugin's <c>wwwroot/</c> its windows are in.</summary>
    public const string FOLDER = "windows";

    /// <summary>How deep elements nest, and how many a window has, at most.</summary>
    private const int MAX_DEPTH = 32;
    private const int MAX_ELEMENTS = 2000;

    private static readonly string[] RECT = ["x", "y", "width", "height"];

    private static readonly string[] PLACES = ["memenu", "toolbar"];

    /// <summary>A window's name: its file's, and the last part of its link.</summary>
    public static bool IsName(string name) => NamePattern().IsMatch(name);

    /// <summary>What is wrong with <paramref name="file"/> as a window; empty when it is one.</summary>
    public static List<string> Problems(JsonElement file)
    {
        var problems = new List<string>();

        if (file.ValueKind != JsonValueKind.Object)
            return ["A window is a JSON object with its \"template\"."];

        if (
            file.TryGetProperty("title", out var title)
            && title.ValueKind is not (JsonValueKind.String or JsonValueKind.Null)
        )
            problems.Add("\"title\" is text.");

        var names = new HashSet<string>(System.StringComparer.Ordinal);

        if (
            !file.TryGetProperty("template", out var template)
            || template.ValueKind != JsonValueKind.Object
        )
            problems.Add("A window has a \"template\": its name, width, height and elements.");
        else
            TemplateProblems(template, names, problems);

        if (file.TryGetProperty("buttons", out var buttons))
            ButtonProblems(buttons, names, problems);

        if (file.TryGetProperty("entries", out var entries))
            EntryProblems(entries, problems);

        return problems;
    }

    private static void TemplateProblems(
        JsonElement template,
        HashSet<string> names,
        List<string> problems
    )
    {
        foreach (var size in new[] { "width", "height" })
            if (!template.TryGetProperty(size, out var value) || !IsPositive(value))
                problems.Add($"The template's \"{size}\" is a number above 0.");

        if (
            !template.TryGetProperty("elements", out var elements)
            || elements.ValueKind != JsonValueKind.Array
            || elements.GetArrayLength() == 0
        )
        {
            problems.Add("The template's \"elements\" is a list with the window's root in it.");
            return;
        }

        var count = 0;

        foreach (var element in elements.EnumerateArray())
            ElementProblems(element, "elements", 1, names, ref count, problems);

        if (count > MAX_ELEMENTS)
            problems.Add($"A window has at most {MAX_ELEMENTS} elements; this one {count}.");
    }

    private static void ElementProblems(
        JsonElement element,
        string at,
        int depth,
        HashSet<string> names,
        ref int count,
        List<string> problems
    )
    {
        count++;

        if (element.ValueKind != JsonValueKind.Object)
        {
            problems.Add($"{at}: an element is a JSON object.");
            return;
        }

        if (depth > MAX_DEPTH)
        {
            problems.Add($"{at}: elements nest at most {MAX_DEPTH} deep.");
            return;
        }

        var name =
            element.TryGetProperty("name", out var named) && named.ValueKind == JsonValueKind.String
                ? named.GetString()
                : null;
        var where = name is { Length: > 0 } ? $"{at} \"{name}\"" : at;

        if (name is { Length: > 0 })
            names.Add(name);

        if (
            !element.TryGetProperty("tag", out var tag)
            || tag.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(tag.GetString())
        )
            problems.Add(
                $"{where}: an element has a \"tag\", its window type (frame, text, button, ...)."
            );

        foreach (var key in RECT)
            if (
                !element.TryGetProperty(key, out var value)
                || value.ValueKind != JsonValueKind.Number
            )
                problems.Add($"{where}: \"{key}\" is a number.");

        if (element.TryGetProperty("vars", out var vars) && vars.ValueKind != JsonValueKind.Object)
            problems.Add($"{where}: \"vars\" is an object.");

        if (!element.TryGetProperty("children", out var children))
            return;

        if (children.ValueKind != JsonValueKind.Array)
        {
            problems.Add($"{where}: \"children\" is a list.");
            return;
        }

        foreach (var child in children.EnumerateArray())
            ElementProblems(child, $"{where} >", depth + 1, names, ref count, problems);
    }

    private static void ButtonProblems(
        JsonElement buttons,
        HashSet<string> names,
        List<string> problems
    )
    {
        if (buttons.ValueKind != JsonValueKind.Object)
        {
            problems.Add("\"buttons\" is an object: what each named element does when clicked.");
            return;
        }

        foreach (var button in buttons.EnumerateObject())
        {
            if (!names.Contains(button.Name))
                problems.Add(
                    $"Button \"{button.Name}\": the template has no element by that name."
                );

            var action = button.Value;
            var close =
                action.ValueKind == JsonValueKind.Object
                && action.TryGetProperty("close", out var closes)
                && closes.ValueKind == JsonValueKind.True;
            var link =
                action.ValueKind == JsonValueKind.Object
                && action.TryGetProperty("link", out var linked)
                && linked.ValueKind == JsonValueKind.String
                    ? linked.GetString()
                    : null;

            if (close == (link is not null))
                problems.Add(
                    $"Button \"{button.Name}\" either closes the window ({{ \"close\": true }}) or opens a link ({{ \"link\": \"...\" }})."
                );
            else if (link is not null && !IsClientLink(link))
                problems.Add(
                    $"Button \"{button.Name}\": \"{link}\" is not an in-client link (navigator/goto/<room>, habbopages/<page>, window/<plugin>/<name>, ...)."
                );
        }
    }

    private static void EntryProblems(JsonElement entries, List<string> problems)
    {
        if (entries.ValueKind != JsonValueKind.Array)
        {
            problems.Add("\"entries\" is a list of where players open the window from.");
            return;
        }

        foreach (var entry in entries.EnumerateArray())
        {
            var where =
                entry.ValueKind == JsonValueKind.Object
                && entry.TryGetProperty("where", out var place)
                && place.ValueKind == JsonValueKind.String
                    ? place.GetString()
                    : null;

            if (where is null || !PLACES.Contains(where))
            {
                problems.Add(
                    $"An entry's \"where\" is one of {string.Join(", ", PLACES)}; this one {where ?? "none"}."
                );
                continue;
            }

            if (where == "memenu" && !HasText(entry, "caption"))
                problems.Add("A me menu entry has a \"caption\".");

            if (where == "toolbar" && !HasText(entry, "icon"))
                problems.Add("A toolbar entry has an \"icon\": an image's address.");
        }
    }

    /// <summary>
    /// An in-client link, as a habbo page or a notice carries it (<c>event:</c> in front, or not):
    /// parts of the router's own (<c>navigator/goto/123</c>), never an address of the web.
    /// </summary>
    private static bool IsClientLink(string link)
    {
        var bare = link.StartsWith("event:", System.StringComparison.Ordinal) ? link[6..] : link;

        return LinkPattern().IsMatch(bare);
    }

    private static bool HasText(JsonElement entry, string key) =>
        entry.TryGetProperty(key, out var value)
        && value.ValueKind == JsonValueKind.String
        && !string.IsNullOrWhiteSpace(value.GetString());

    private static bool IsPositive(JsonElement value) =>
        value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number) && number > 0;

    [GeneratedRegex("^[a-z0-9][a-z0-9_-]{0,63}$")]
    private static partial Regex NamePattern();

    [GeneratedRegex(@"^[A-Za-z0-9_-]+(/[^/\s:]+)*$")]
    private static partial Regex LinkPattern();
}
