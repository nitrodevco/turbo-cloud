using System.Xml.Linq;

namespace Turbo.Assets;

/// <summary>Reading the attributes of Habbo's asset XML (<c>assets.xml</c>, <c>index.xml</c>, the avatar tables).</summary>
internal static class XmlAttributes
{
    /// <summary>The attribute's value as written; null when the element or the attribute is missing.</summary>
    public static string? Attr(XElement? element, string name) => element?.Attribute(name)?.Value;
}
