using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.User;

/// <summary>Whether the room is this player's.</summary>
public sealed class UserIsOwnerVariable(RoomGrain roomGrain) : UserVariable<IRoomPlayer>(roomGrain)
{
    private const int TRUE = 1;
    private const int FALSE = 0;

    protected override string VariableName => "@is_owner";

    protected override WiredVariableGroupSubBandType SubBandType =>
        WiredVariableGroupSubBandType.Base;

    protected override ushort Order => 10;

    protected override WiredVariableFlags Flags => WiredVariableFlags.None;

    protected override WiredVariableValue GetValueForAvatar(IRoomPlayer avatar) =>
        WiredVariableValue.Parse(
            _roomGrain.SecurityModule.IsRoomOwner(avatar.PlayerId) ? TRUE : FALSE
        );
}
