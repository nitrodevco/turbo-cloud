using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Turbo.Gamedata.Habbo.Client.Theme;

/// <summary>
/// An element of the window manager library's XML, as nitro-studio's tokenizer reads it
/// (<c>server/theme/windowManagerLibrary.ts</c>, <c>parseXml</c>): its tag, its attributes as
/// written (no entity is decoded) and its child elements. Text between tags is dropped.
/// </summary>
internal sealed class ThemeXmlNode(string tag, Dictionary<string, string> attrs)
{
    public string Tag { get; } = tag;

    public Dictionary<string, string> Attrs { get; } = attrs;

    public List<ThemeXmlNode> Children { get; } = [];

    /// <summary>An attribute's value, or null when the element does not have it.</summary>
    public string? Attr(string name) => Attrs.TryGetValue(name, out var value) ? value : null;

    /// <summary>The first child element with the tag, or null.</summary>
    public ThemeXmlNode? Child(string tag) => Children.FirstOrDefault(x => x.Tag == tag);

    /// <summary>Every child element with the tag, in order.</summary>
    public IEnumerable<ThemeXmlNode> ChildrenOf(string tag) => Children.Where(x => x.Tag == tag);

    /// <summary>This element and every element under it, depth first.</summary>
    public IEnumerable<ThemeXmlNode> Walk()
    {
        yield return this;

        foreach (var child in Children)
        foreach (var node in child.Walk())
            yield return node;
    }
}

/// <summary>
/// The attribute-only XML the client's window assets are written in, read with Studio's tokenizer
/// rather than an XML parser, so a file a strict parser refuses (an unescaped ampersand) is read
/// the same way the theme Studio builds was.
/// </summary>
internal static partial class ThemeXml
{
    /// <summary>The root element of <paramref name="text"/>, or null when it has none.</summary>
    public static ThemeXmlNode? Parse(string text)
    {
        var root = new ThemeXmlNode("#root", []);
        var stack = new List<ThemeXmlNode> { root };

        foreach (Match match in TagPattern().Matches(text))
        {
            if (match.Value.StartsWith("<!--") || match.Value.StartsWith("<?"))
                continue;

            if (match.Groups[1].Success)
            {
                if (stack.Count > 1)
                    stack.RemoveAt(stack.Count - 1);

                continue;
            }

            var attrs = new Dictionary<string, string>();

            foreach (Match attr in AttrPattern().Matches(match.Groups[3].Value))
                attrs[attr.Groups[1].Value] = attr.Groups[2].Value;

            var node = new ThemeXmlNode(match.Groups[2].Value, attrs);

            stack[^1].Children.Add(node);

            if (match.Groups[4].Value.Length == 0)
                stack.Add(node);
        }

        return root.Children.FirstOrDefault();
    }

    /// <summary>
    /// <c>0xff418db0</c> as <c>#418db0</c>: a Flash colour as the theme writes one, its alpha byte
    /// dropped; null for nothing.
    /// </summary>
    public static string? HexColor(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return null;

        var digits = NonHexDigit().Replace(HexPrefix().Replace(value, "", 1), "");

        if (digits.Length == 0)
            return null;

        return "#" + (digits.Length > 6 ? digits[^6..] : digits).PadLeft(6, '0').ToLowerInvariant();
    }

    [GeneratedRegex(
        """<!--[\s\S]*?-->|<\?[\s\S]*?\?>|</([A-Za-z0-9_:-]+)\s*>|<([A-Za-z0-9_:-]+)((?:\s+[A-Za-z0-9_:.-]+\s*=\s*"[^"]*")*)\s*(/?)>"""
    )]
    private static partial Regex TagPattern();

    [GeneratedRegex(@"([A-Za-z0-9_:.-]+)\s*=\s*""([^""]*)""")]
    private static partial Regex AttrPattern();

    [GeneratedRegex("^0x", RegexOptions.IgnoreCase)]
    private static partial Regex HexPrefix();

    [GeneratedRegex("[^0-9a-fA-F]")]
    private static partial Regex NonHexDigit();
}
