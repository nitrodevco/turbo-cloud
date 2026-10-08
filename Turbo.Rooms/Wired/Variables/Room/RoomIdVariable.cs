using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.Room;

/// <summary><c>@room_id</c>: this room's id. Read only.</summary>
public sealed class RoomIdVariable(RoomGrain roomGrain) : RoomVariable(roomGrain)
{
    protected override string VariableName => "@room_id";

    // After the team sizes, before @group_id, as the official list has it.
    protected override ushort Order => 26;

    protected override WiredVariableFlags Flags =>
        WiredVariableFlags.HasValue | WiredVariableFlags.AlwaysAvailable;

    protected override WiredVariableValue GetValueForRoom(RoomGrain roomGrain) =>
        WiredVariableValue.Parse(roomGrain._state.RoomSnapshot.RoomId.Value);
}
