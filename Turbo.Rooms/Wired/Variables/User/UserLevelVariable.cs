using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.User;

/// <summary>
/// The level the player's profile shows (<c>ExtendedProfile.accountLevel</c>), not their
/// achievement level: on Habbo, Creator Tools' inspection read @level 3 for a player whose profile
/// says Level 3 and whose achievement level is 39 (official client capture 2026-10-14).
/// </summary>
public sealed class UserLevelVariable(RoomGrain roomGrain)
    : UserValueVariable<IRoomPlayer>(roomGrain)
{
    protected override string VariableName => "@level";

    protected override WiredVariableGroupSubBandType SubBandType =>
        WiredVariableGroupSubBandType.Base;

    protected override ushort Order => 45;

    protected override WiredVariableFlags Flags =>
        WiredVariableFlags.HasValue | WiredVariableFlags.AlwaysAvailable;

    protected override WiredVariableValue GetValueForAvatar(IRoomPlayer avatar) =>
        WiredVariableValue.Parse(avatar.AccountLevel);
}
