using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.User;

/// <summary>Whether the avatar has stood still long enough to be idle.</summary>
public sealed class UserIsIdleVariable(RoomGrain roomGrain) : UserVariable<IRoomAvatar>(roomGrain)
{
    private const int TRUE = 1;
    private const int FALSE = 0;

    protected override string VariableName => "@is_idle";

    protected override WiredVariableGroupSubBandType SubBandType =>
        WiredVariableGroupSubBandType.Meta;

    protected override ushort Order => 40;

    protected override WiredVariableFlags Flags => WiredVariableFlags.None;

    protected override WiredVariableValue GetValueForAvatar(IRoomAvatar avatar) =>
        WiredVariableValue.Parse(avatar.IsIdle ? TRUE : FALSE);
}
