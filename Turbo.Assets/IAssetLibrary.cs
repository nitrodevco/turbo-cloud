namespace Turbo.Assets;

/// <summary>
/// A Habbo asset library whatever its container - an SWF's binary tags, or a <c>.hab</c>'s
/// entries. Its documents are named the way the client's <c>AssetLibrary</c> names them:
/// <c>index</c>, <c>manifest</c>, <c>&lt;type&gt;_assets</c>, <c>&lt;type&gt;_logic</c>,
/// <c>&lt;type&gt;_visualization</c> - without the SWF's <c>&lt;DocumentClass&gt;_</c> prefix.
/// </summary>
public interface IAssetLibrary
{
    /// <summary>The library's name: the SWF's document class, the <c>.hab</c>'s name.</summary>
    public string DocumentClass { get; }

    /// <summary>An XML document's text, comments and all; null when there is none.</summary>
    public string? GetXml(string name);

    /// <summary>A binary entry (a palette); null when there is none.</summary>
    public byte[]? GetBinary(string name);
}
