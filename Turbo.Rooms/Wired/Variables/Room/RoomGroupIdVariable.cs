using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.Room;

/// <summary>
/// <c>@group_id</c>: the group the room belongs to, as the room's group homeroom badge shows it.
/// Read only; held only while the room has a group, as <c>@favourite_group_id</c> is.
/// </summary>
public sealed class RoomGroupIdVariable(RoomGrain roomGrain) : RoomVariable(roomGrain)
{
    protected override string VariableName => "@group_id";

    // After @room_id, before @current_time (20), as the official list has it.
    protected override ushort Order => 25;

    protected override WiredVariableFlags Flags => WiredVariableFlags.HasValue;

    public override bool TryGetValue(in WiredVariableKey key, out WiredVariableValue value)
    {
        value = WiredVariableValue.Default;

        return GroupId > 0 && base.TryGetValue(key, out value);
    }

    protected override WiredVariableValue GetValueForRoom(RoomGrain roomGrain) =>
        WiredVariableValue.Parse(GroupId);

    private int GroupId => _roomGrain._state.RoomSnapshot.Guild?.GuildId.Value ?? 0;
}
