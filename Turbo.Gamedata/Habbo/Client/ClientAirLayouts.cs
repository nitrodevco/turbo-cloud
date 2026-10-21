using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Turbo.Assets.Conversion;
using Turbo.Assets.Hab;
using Turbo.Assets.Swf;

namespace Turbo.Gamedata.Habbo.Client;

/// <summary>
/// The window layouts the templates take from Habbo's AIR client (<c>HabboAir.swf</c>) over the HTML
/// client's, named <c>&lt;library&gt;/&lt;asset&gt;</c> (<c>habbo-toolbar-com/prog_menu_view_xml</c>):
/// the ones whose HTML client copy lacks what the client looks for. Build 1745's Progression menu adds
/// a reward track's item to the list named <c>menu_items</c> (ProgMenuController.as:185), which the
/// AIR client's <c>prog_menu_view</c> names and the HTML client's 95 leaves unnamed. A layout is the
/// static field its library's class embeds it in, found as <see cref="ClientAirImages"/> finds a bitmap.
/// </summary>
internal static class ClientAirLayouts
{
    /// <summary>What was taken: the layout XMLs by library and asset name, and the ones that could not be.</summary>
    public sealed record Taken(
        Dictionary<string, Dictionary<string, string>> Layouts,
        List<string> Problems
    );

    public static Taken Take(
        SwfLibrary air,
        IReadOnlyList<(string Name, HabLibrary Hab)> libraries,
        IEnumerable<string> wanted
    )
    {
        var byClass = ClientAirImages.ByClass(libraries);
        var layouts = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
        var problems = new List<string>();
        var asked = wanted
            .Select(x => x.Trim().Split('/', 2))
            .Where(x => x.Length == 2 && x[0].Length > 0 && x[1].Length > 0)
            .Select(x => (Library: x[0], Asset: x[1]))
            .Where(x => libraries.Any(l => l.Name == x.Library))
            .Distinct()
            .ToList();

        if (asked.Count == 0)
            return new Taken(layouts, problems);

        var embedded = air.EmbeddedBinaries(byClass.ContainsKey);

        foreach (var (library, asset) in asked)
        {
            var xml = embedded
                .Where(x => byClass[x.Key].Any(l => l.Name == library))
                .Select(x => x.Value.GetValueOrDefault(asset))
                .FirstOrDefault(x => x is not null)
                is { } data
                ? Encoding.UTF8.GetString(data)
                : null;

            if (xml is null)
            {
                problems.Add($"{library}: {asset} (not in the AIR client)");

                continue;
            }

            if (LayoutTemplateMapper.RootTag(xml) != "layout")
            {
                problems.Add($"{library}: {asset} (not a layout in the AIR client)");

                continue;
            }

            if (!layouts.TryGetValue(library, out var taken))
                layouts[library] = taken = new(StringComparer.Ordinal);

            taken[asset] = xml;
        }

        return new Taken(layouts, problems);
    }
}
