using System;
using System.Collections.Generic;
using System.Linq;
using Turbo.Assets;
using Turbo.Assets.Hab;
using Turbo.Assets.Swf;

namespace Turbo.Gamedata.Habbo.Client;

/// <summary>
/// The bitmaps a library's class embeds in Habbo's AIR client (<c>HabboAir.swf</c>) that its
/// library in the HTML client lacks: Habbo adds some to the AIR client only (build 1745's
/// <c>cursed_dragon_adoption_icon</c> in <c>HabboWindowManagerCom</c>). A library's class is the one
/// its manifest names (<c>HabboWindowManagerCom</c>), else its name without the dashes, either in any
/// case; an embed is the static field its class's static initializer sets
/// (<see cref="SwfLibrary.EmbeddedImages"/>), named as the client asks for the asset.
/// </summary>
internal static class ClientAirImages
{
    /// <summary>What was taken: the bitmaps (PNG) by library and asset name, and the embeds that could not be read.</summary>
    public sealed record Taken(
        Dictionary<string, Dictionary<string, byte[]>> Images,
        List<string> Problems
    );

    public static Taken Missing(
        SwfLibrary air,
        IReadOnlyList<(string Name, HabLibrary Hab)> libraries
    )
    {
        var byClass = ByClass(libraries);
        var images = new Dictionary<string, Dictionary<string, byte[]>>(StringComparer.Ordinal);
        var problems = new List<string>();

        foreach (var (className, fields) in air.EmbeddedImages(byClass.ContainsKey))
        {
            foreach (var (library, hab) in byClass[className])
            {
                foreach (var (asset, tag) in fields.OrderBy(x => x.Key, StringComparer.Ordinal))
                {
                    if (ClientTemplates.HasBitmap(hab, asset))
                        continue;

                    if (images.GetValueOrDefault(library)?.ContainsKey(asset) == true)
                        continue;

                    try
                    {
                        var png = SwfImageDecoder.Decode(tag).EncodePng();

                        if (!images.TryGetValue(library, out var taken))
                            images[library] = taken = new(StringComparer.Ordinal);

                        taken[asset] = png;
                    }
                    catch (AssetFormatException ex)
                    {
                        problems.Add($"{library}: {asset} ({ex.Message})");
                    }
                }
            }
        }

        return new Taken(images, problems);
    }

    /// <summary>The libraries by the class of the AIR client that is theirs, either name in any case.</summary>
    internal static Dictionary<string, List<(string Name, HabLibrary Hab)>> ByClass(
        IReadOnlyList<(string Name, HabLibrary Hab)> libraries
    )
    {
        var byClass = new Dictionary<string, List<(string Name, HabLibrary Hab)>>(
            StringComparer.OrdinalIgnoreCase
        );

        foreach (var library in libraries)
        {
            foreach (
                var className in new[]
                {
                    library.Hab.Manifest.Name,
                    library.Name.Replace("-", "", StringComparison.Ordinal),
                }.Distinct(StringComparer.OrdinalIgnoreCase)
            )
            {
                if (className.Length == 0)
                    continue;

                if (!byClass.TryGetValue(className, out var named))
                    byClass[className] = named = [];

                if (!named.Contains(library))
                    named.Add(library);
            }
        }

        return byClass;
    }
}
