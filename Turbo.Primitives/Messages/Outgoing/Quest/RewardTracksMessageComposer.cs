using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Quests.Snapshots;

namespace Turbo.Primitives.Messages.Outgoing.Quest;

/// <summary>The player's reward tracks, sent at login; the client replaces its tracks with them.</summary>
[GenerateSerializer, Immutable]
public sealed record RewardTracksMessageComposer : IComposer
{
    [Id(0)]
    public required bool Disabled { get; init; }

    [Id(1)]
    public required ImmutableArray<RewardTrackSnapshot> Tracks { get; init; }

    /// <summary>The tracks changed under an open window; the client shows reward_track.reload.*.</summary>
    [Id(2)]
    public required bool Reload { get; init; }
}
