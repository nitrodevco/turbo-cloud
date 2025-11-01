using Turbo.Contracts.Abstractions;
using Turbo.Primitives.Snapshots.Rooms;

namespace Turbo.Primitives.Messages.Outgoing.Navigator;

public record GetGuestRoomResultMessageComposer : IComposer
{
    public required bool EnterRoom { get; init; }
    public required RoomSnapshot RoomSnapshot { get; init; }
    public required bool RoomForward { get; init; }
    public required bool StaffPick { get; init; }
    public required bool IsGroupMember { get; init; }
    public required bool AllInRoomMuted { get; init; }
    public required bool CanMute { get; init; }
}
