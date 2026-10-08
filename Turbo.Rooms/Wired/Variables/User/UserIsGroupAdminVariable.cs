using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.User;

/// <summary>
/// Whether the player is an admin (or the owner) of the group whose homeroom this is. The Wired
/// Faculty tutorial "How to do an AUTO PRIZE COUNTER?" lets only a group's admins host with
/// "Has variable" on <c>@is_group_admin</c> of the triggering user. Read from the group level
/// the room works out for a player when they come in and keeps until their rank changes.
/// </summary>
public sealed class UserIsGroupAdminVariable(RoomGrain roomGrain)
    : UserFlagVariable<IRoomPlayer>(roomGrain)
{
    protected override string VariableName => "@is_group_admin";

    protected override WiredVariableGroupSubBandType SubBandType =>
        WiredVariableGroupSubBandType.Base;

    protected override ushort Order => 35;

    protected override bool HasFlag(IRoomPlayer avatar) =>
        _roomGrain._state.GroupLevelByPlayerId.TryGetValue(avatar.PlayerId, out var level)
        && level == RoomControllerType.GroupAdmin;
}
