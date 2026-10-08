using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.Player;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.Context;

/// <summary>
/// <c>@event.link.source_room_id</c>: for a "User Enters Room" stack, the room the user came from
/// when a room link (Teleport To Room through a Room Linker) or a teleporter sent them; 0 for any
/// other way in, and for a stack something else started.
/// </summary>
public sealed class ContextLinkSourceRoomIdVariable(RoomGrain roomGrain)
    : ContextVariable(roomGrain)
{
    protected override string VariableName => "@event.link.source_room_id";

    // The official client's Creator Tools list the context variables in this order.
    protected override ushort Order => 30;

    protected override WiredVariableFlags Flags =>
        WiredVariableFlags.HasValue | WiredVariableFlags.AlwaysAvailable;

    protected override WiredVariableValue GetValueForExecution(WiredRunningExecution execution) =>
        execution.Event is PlayerEnterEvent entered
        && AvatarModule.TryGetPlayer(entered.PlayerId, out var player)
            ? player.RoomEntry.SourceRoomId.Value
            : 0;
}
