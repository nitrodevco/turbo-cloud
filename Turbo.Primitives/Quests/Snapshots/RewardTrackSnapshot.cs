using System.Collections.Immutable;
using Orleans;

namespace Turbo.Primitives.Quests.Snapshots;

/// <summary>A reward track with the player's points, tasks and prizes.</summary>
[GenerateSerializer, Immutable]
public sealed record RewardTrackSnapshot
{
    /// <summary>Names the <c>reward_track.&lt;id&gt;.*</c> texts.</summary>
    [Id(0)]
    public required string Id { get; init; }

    /// <summary>A colour the client knows (blue, orange, forest_green, red, cyan) or empty.</summary>
    [Id(1)]
    public required string Theme { get; init; }

    [Id(2)]
    public required int Points { get; init; }

    /// <summary>Null when the track has no premium.</summary>
    [Id(3)]
    public required RewardTrackPremiumSnapshot? PremiumConfig { get; init; }

    /// <summary>Whether the player owns the track's premium.</summary>
    [Id(4)]
    public required bool Premium { get; init; }

    /// <summary>Every free prize claimed.</summary>
    [Id(5)]
    public required bool Complete { get; init; }

    /// <summary>No premium, or every prize claimed.</summary>
    [Id(6)]
    public required bool PremiumComplete { get; init; }

    [Id(7)]
    public required ImmutableArray<RewardTrackTaskSnapshot> Tasks { get; init; }

    [Id(8)]
    public required ImmutableArray<RewardTrackPrizeSnapshot> Prizes { get; init; }
}
