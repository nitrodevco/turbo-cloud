using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Turbo.Assets.Hab;

/// <summary>A <c>.hab</c>'s manifest: what the library is and every file it carries.</summary>
public sealed class HabManifest
{
    public string Format { get; init; } = string.Empty;

    public int Version { get; init; }

    public string Name { get; init; } = string.Empty;

    /// <summary>The library's <c>&lt;manifest&gt;</c> XML, as the SWF embedded it; absent in a prebuilt library.</summary>
    [JsonPropertyName("manifest")]
    public string? ManifestXml { get; init; }

    public List<HabAlias>? Aliases { get; init; }

    /// <summary>Assets the manifest lists that the library does not carry.</summary>
    public List<string>? UnresolvedAssets { get; init; }

    public List<HabEntry> Entries { get; init; } = [];
}
