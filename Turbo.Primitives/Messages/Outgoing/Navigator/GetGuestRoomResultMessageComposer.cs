using System.Collections.Generic;
using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Outgoing.Navigator;

public record GetGuestRoomResultMessageComposer : IComposer
{
    public required bool EnterRoom { get; init; }
    public object? Data { get; init; }
    public required bool RoomForward { get; init; }
    public required bool StaffPick { get; init; }
    public required bool IsGroupMember { get; init; }
    public required bool AllInRoomMuted { get; init; }
    public required bool CanMute { get; init; }
    public object? RoomModerationSettings { get; init; }
    public object? ChatSettings { get; init; }
}
