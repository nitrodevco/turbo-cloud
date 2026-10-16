using System;
using System.Collections.Immutable;
using Orleans;

namespace Turbo.Primitives.Gamedata.Snapshots;

/// <summary>
/// Habbo's values of reception texts, from the newest version of its texts the hotel has found,
/// the hotel's beside each; a key Habbo's texts lack is left out.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record HotelViewHabboTexts
{
    [Id(0)]
    public required ImmutableArray<HotelViewHabboEntry> Texts { get; init; }

    /// <summary>When the version the texts come from was last checked; null before Habbo's texts were ever found.</summary>
    [Id(1)]
    public DateTime? CheckedAt { get; init; }
}
