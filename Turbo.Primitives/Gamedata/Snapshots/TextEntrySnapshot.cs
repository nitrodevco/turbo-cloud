using Orleans;

namespace Turbo.Primitives.Gamedata.Snapshots;

/// <summary>One of the hotel's texts, with Habbo's as last taken in beside it.</summary>
[GenerateSerializer, Immutable]
public sealed record TextEntrySnapshot
{
    [Id(0)]
    public required string Key { get; init; }

    /// <summary>As the file writes it, line breaks as <c>\n</c> escapes.</summary>
    [Id(1)]
    public required string Value { get; init; }

    /// <summary>Habbo's value as last taken in; null for the hotel's own text.</summary>
    [Id(2)]
    public string? Habbo { get; init; }
}
