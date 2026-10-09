using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms;

namespace Turbo.Primitives.Messages.Outgoing.Roomsettings;

/// <summary>Whether the player may manage the room's raid protection; the client shows Room info's button on it.</summary>
[GenerateSerializer, Immutable]
public sealed record RaidProtectionCapabilityMessageComposer : IComposer
{
    [Id(0)]
    public required RoomId RoomId { get; init; }

    [Id(1)]
    public required bool CanManage { get; init; }
}
