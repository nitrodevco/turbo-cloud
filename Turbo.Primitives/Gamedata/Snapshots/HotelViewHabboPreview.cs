using System.Collections.Immutable;
using Orleans;

namespace Turbo.Primitives.Gamedata.Snapshots;

/// <summary>
/// Habbo's reception as its external variables have it now, against the hotel's: each of its
/// variables with the hotel's beside it, and the hotel's reception variables Habbo has none of.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record HotelViewHabboPreview
{
    [Id(0)]
    public required string Domain { get; init; }

    /// <summary>Habbo's reception variables, by key, the hotel's value beside each.</summary>
    [Id(1)]
    public required ImmutableArray<HotelViewHabboEntry> Variables { get; init; }

    /// <summary>The hotel's reception variables Habbo has none of, by key.</summary>
    [Id(2)]
    public required ImmutableArray<string> Missing { get; init; }
}
