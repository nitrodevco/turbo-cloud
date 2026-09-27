using Orleans;

namespace Turbo.Primitives.Players.Snapshots.Messenger;

/// <summary>
/// What a profile shows about its owner's friends list, as seen by the player looking at it.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record MessengerProfileRelationSnapshot
{
    [Id(0)]
    public required int FriendCount { get; init; }

    /// <summary>The viewer is on the owner's friends list.</summary>
    [Id(1)]
    public required bool IsFriend { get; init; }

    /// <summary>The viewer has a friend request waiting with the owner.</summary>
    [Id(2)]
    public required bool IsFriendRequestSent { get; init; }
}
