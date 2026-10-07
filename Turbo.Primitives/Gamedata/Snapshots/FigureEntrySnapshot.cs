using Orleans;
using Turbo.Primitives.Gamedata.Enums;

namespace Turbo.Primitives.Gamedata.Snapshots;

/// <summary>One record of the hotel's figure data, with Habbo's as last taken in beside it.</summary>
[GenerateSerializer, Immutable]
public sealed record FigureEntrySnapshot
{
    [Id(0)]
    public required FigureRecordKind Kind { get; init; }

    [Id(1)]
    public required string Key { get; init; }

    /// <summary>A piece's kind of clothing, a colour's palette, a kind's own type.</summary>
    [Id(2)]
    public required string Group { get; init; }

    /// <summary>Its fields, as JSON, by the names Habbo's file gives them.</summary>
    [Id(3)]
    public required string Data { get; init; }

    /// <summary>Whether Habbo has the record: false for the hotel's own.</summary>
    [Id(4)]
    public required bool FromHabbo { get; init; }

    /// <summary>Habbo's fields as last taken in; null for the hotel's own.</summary>
    [Id(5)]
    public string? HabboData { get; init; }
}
