using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.User;

/// <summary>Whether a wired freeze is holding the avatar still.</summary>
public sealed class UserIsFrozenVariable(RoomGrain roomGrain) : UserVariable<IRoomAvatar>(roomGrain)
{
    private const int TRUE = 1;
    private const int FALSE = 0;

    protected override string VariableName => "@is_frozen";

    protected override WiredVariableGroupSubBandType SubBandType =>
        WiredVariableGroupSubBandType.Meta;

    protected override ushort Order => 100;

    protected override WiredVariableFlags Flags => WiredVariableFlags.None;

    protected override WiredVariableValue GetValueForAvatar(IRoomAvatar avatar) =>
        WiredVariableValue.Parse(avatar.IsFrozen ? TRUE : FALSE);
}
