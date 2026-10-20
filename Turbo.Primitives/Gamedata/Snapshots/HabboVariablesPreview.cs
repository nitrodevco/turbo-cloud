using System.Collections.Immutable;
using Orleans;

namespace Turbo.Primitives.Gamedata.Snapshots;

/// <summary>
/// Habbo's external variables under a prefix as it has them now, against the hotel's: each of its
/// variables with the hotel's beside it, and the hotel's under the prefix Habbo has none of.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record HabboVariablesPreview
{
    [Id(0)]
    public required string Domain { get; init; }

    [Id(1)]
    public required string Prefix { get; init; }

    /// <summary>Habbo's variables under the prefix, by key, the hotel's value beside each.</summary>
    [Id(2)]
    public required ImmutableArray<HotelViewHabboEntry> Variables { get; init; }

    /// <summary>The hotel's variables under the prefix Habbo has none of, by key.</summary>
    [Id(3)]
    public required ImmutableArray<string> Missing { get; init; }
}
