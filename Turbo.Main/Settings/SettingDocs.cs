using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace Turbo.Main.Settings;

/// <summary>
/// A setting's summary, from the XML documentation written beside its assembly
/// (<c>GenerateDocumentationFile</c>): the <c>/// &lt;summary&gt;</c> on its property, as text.
/// Empty when the property has none or the file isn't there.
/// </summary>
internal static partial class SettingDocs
{
    private static readonly ConcurrentDictionary<Assembly, XDocument?> FILES = new();

    public static string Summary(PropertyInfo property) =>
        property.DeclaringType is { } type
            ? Summary(FILES.GetOrAdd(type.Assembly, Read), property)
            : string.Empty;

    /// <summary>
    /// The summaries of a plugin's settings, from the documentation file beside its assembly. A
    /// plugin is loaded from bytes and unloaded again, so its file is read here, not kept by assembly.
    /// </summary>
    public static Func<PropertyInfo, string> From(string? docsFile)
    {
        var file = docsFile is null ? null : Load(docsFile);

        return property => Summary(file, property);
    }

    private static string Summary(XDocument? file, PropertyInfo property)
    {
        var type = property.DeclaringType;

        if (type is null)
            return string.Empty;

        var member = $"P:{type.FullName?.Replace('+', '.')}.{property.Name}";
        var summary = file
            ?.Root?.Element("members")
            ?.Elements("member")
            .FirstOrDefault(x => (string?)x.Attribute("name") == member)
            ?.Element("summary");

        return summary is null ? string.Empty : Text(summary);
    }

    private static XDocument? Read(Assembly assembly)
    {
        if (string.IsNullOrEmpty(assembly.Location))
            return null;

        return Load(Path.ChangeExtension(assembly.Location, ".xml"));
    }

    private static XDocument? Load(string path)
    {
        try
        {
            return File.Exists(path) ? XDocument.Load(path) : null;
        }
        catch (Exception ex) when (ex is IOException or XmlException or UnauthorizedAccessException)
        {
            // Summaries are a help: the page works without them.
            return null;
        }
    }

    /// <summary>The summary as one line of text: <c>&lt;c&gt;</c> kept as its text, a reference as its name.</summary>
    private static string Text(XElement summary)
    {
        var text = new StringBuilder();

        foreach (var node in summary.DescendantNodes())
        {
            switch (node)
            {
                case XText part when part.Parent?.Name != "see":
                    text.Append(part.Value);

                    break;
                case XElement { Name.LocalName: "see" or "paramref" or "typeparamref" } reference:
                    var name =
                        (string?)reference.Attribute("cref")
                        ?? (string?)reference.Attribute("langword")
                        ?? (string?)reference.Attribute("name")
                        ?? reference.Value;

                    // A method's parameters are no part of its name.
                    name = name.Split('(')[0];

                    text.Append(
                        reference.Value.Length > 0
                            ? reference.Value
                            : name[(name.LastIndexOf('.') + 1)..]
                    );

                    break;
            }
        }

        return Spaces().Replace(text.ToString(), " ").Trim();
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();
}
