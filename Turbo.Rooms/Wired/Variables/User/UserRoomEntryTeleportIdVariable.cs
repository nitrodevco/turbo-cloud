using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.User;

/// <summary>
/// The teleporter in this room the player arrived at, when they came through one; zero for
/// every other way in. It is the far half of the pair they stepped into, so a stack can tell
/// which door someone used.
/// </summary>
public sealed class UserRoomEntryTeleportIdVariable(RoomGrain roomGrain)
    : UserVariable<IRoomPlayer>(roomGrain)
{
    protected override string VariableName => "@room_entry.teleport_id";

    protected override WiredVariableGroupSubBandType SubBandType =>
        WiredVariableGroupSubBandType.Meta;

    protected override ushort Order => 20;

    protected override WiredVariableFlags Flags => WiredVariableFlags.HasValue;

    protected override WiredVariableValue GetValueForAvatar(IRoomPlayer avatar) =>
        WiredVariableValue.Parse(avatar.RoomEntry.TeleportId);
}
