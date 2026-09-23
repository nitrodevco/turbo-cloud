using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.User;

/// <summary>Whether the room is this player's.</summary>
public sealed class UserIsOwnerVariable(RoomGrain roomGrain)
    : UserFlagVariable<IRoomPlayer>(roomGrain)
{
    protected override string VariableName => "@is_owner";

    protected override WiredVariableGroupSubBandType SubBandType =>
        WiredVariableGroupSubBandType.Base;

    protected override ushort Order => 10;

    protected override bool HasFlag(IRoomPlayer avatar) =>
        _roomGrain.SecurityModule.IsRoomOwner(avatar.PlayerId);
}
