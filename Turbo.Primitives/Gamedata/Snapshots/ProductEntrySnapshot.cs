using Orleans;

namespace Turbo.Primitives.Gamedata.Snapshots;

/// <summary>One of the hotel's products, with Habbo's as last taken in beside it.</summary>
[GenerateSerializer, Immutable]
public sealed record ProductEntrySnapshot
{
    [Id(0)]
    public required string Code { get; init; }

    [Id(1)]
    public string? Name { get; init; }

    [Id(2)]
    public string? Description { get; init; }

    /// <summary>Whether Habbo has the product: false for the hotel's own.</summary>
    [Id(3)]
    public required bool FromHabbo { get; init; }

    [Id(4)]
    public string? HabboName { get; init; }

    [Id(5)]
    public string? HabboDescription { get; init; }
}
