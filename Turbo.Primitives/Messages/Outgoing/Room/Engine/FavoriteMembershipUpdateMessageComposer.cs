using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Primitives.Messages.Outgoing.Room.Engine;

/// <summary>
/// Somebody in the room changed the group badge they wear. It carries the avatar's index in the
/// room rather than a player id, because that is what the client has on screen to redraw.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record FavoriteMembershipUpdateMessageComposer : IComposer
{
    [Id(0)]
    public required RoomObjectId RoomIndex { get; init; }

    /// <summary>The group, or <c>-1</c> when they stopped wearing one.</summary>
    [Id(1)]
    public required int GuildId { get; init; }

    [Id(2)]
    public required int Status { get; init; }

    [Id(3)]
    public required string GuildName { get; init; }
}
