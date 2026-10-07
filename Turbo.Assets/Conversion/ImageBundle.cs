using System.Collections.Generic;
using System.Linq;
using Turbo.Assets.Hab;
using Turbo.Assets.Swf;

namespace Turbo.Assets.Conversion;

/// <summary>
/// A library's images by asset name, the other names that share an image (<see cref="Sources"/>),
/// and the images the asset data uses (<see cref="Referenced"/>), which alone are packed.
/// </summary>
public sealed class ImageBundle
{
    private readonly List<string> _referenced = [];
    private readonly HashSet<string> _referencedSet = [];

    public Dictionary<string, RgbaImage> Images { get; } = [];

    /// <summary>Another name for an image: alias → the name it is stored under.</summary>
    public Dictionary<string, string> Sources { get; } = [];

    public IReadOnlyList<string> Referenced => _referenced;

    public void Reference(string name)
    {
        if (Images.ContainsKey(name) && _referencedSet.Add(name))
            _referenced.Add(name);
    }

    /// <summary>
    /// An SWF's images, named by their class less the document class's <c>&lt;Name&gt;_</c>; a
    /// character exported under more names gives each of those as a source of the first.
    /// </summary>
    public static ImageBundle FromSwf(SwfLibrary swf)
    {
        var bundle = new ImageBundle();
        var prefix = swf.DocumentClass.Length + 1;
        var named = new List<(SwfImageTag Tag, string ClassName)>();

        foreach (var tag in swf.Images)
        {
            if (swf.NameOf(tag.CharacterId) is not { } className || className.Length < prefix)
                continue;

            bundle.Images[className[prefix..]] = SwfImageDecoder.Decode(tag);
            named.Add((tag, className));
        }

        foreach (var (tag, className) in named)
        {
            foreach (var (id, name) in swf.Symbols)
            {
                if (id != tag.CharacterId || name == className || name.Length < prefix)
                    continue;

                var stored = className[prefix..];

                if (bundle.Images.ContainsKey(stored))
                    bundle.Sources[name[prefix..]] = stored;
            }
        }

        return bundle;
    }

    /// <summary>
    /// A <c>.hab</c>'s PNG entries, by name, and its image aliases as sources. An alias with a
    /// region is a slice of an image (a window skin), not an asset of its own.
    /// </summary>
    public static ImageBundle FromHab(HabLibrary hab)
    {
        var bundle = new ImageBundle();

        foreach (var entry in hab.Entries.Where(x => x.MimeType == "image/png"))
            bundle.Images[entry.Name] = RgbaImage.Decode(hab.Get(entry));

        foreach (var alias in hab.Manifest.Aliases ?? [])
        {
            if (
                alias.MimeType == "image/png"
                && !(alias.Params?.ContainsKey("region") ?? false)
                && bundle.Images.ContainsKey(alias.Ref)
            )
                bundle.Sources[alias.Name] = alias.Ref;
        }

        return bundle;
    }
}
