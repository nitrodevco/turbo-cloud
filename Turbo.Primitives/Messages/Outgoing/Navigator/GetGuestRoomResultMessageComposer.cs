using Orleans;
using Turbo.Contracts.Abstractions;
using Turbo.Primitives.Orleans.Snapshots.Rooms;

namespace Turbo.Primitives.Messages.Outgoing.Navigator;

[GenerateSerializer, Immutable]
public sealed record GetGuestRoomResultMessageComposer : IComposer
{
    [Id(0)]
    public required bool EnterRoom { get; init; }

    [Id(1)]
    public required RoomSnapshot RoomSnapshot { get; init; }

    [Id(2)]
    public required bool RoomForward { get; init; }

    [Id(3)]
    public required bool StaffPick { get; init; }

    [Id(4)]
    public required bool IsGroupMember { get; init; }

    [Id(5)]
    public required bool AllInRoomMuted { get; init; }

    [Id(6)]
    public required bool CanMute { get; init; }
}
