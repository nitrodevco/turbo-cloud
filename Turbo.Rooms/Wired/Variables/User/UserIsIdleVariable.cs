using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.User;

/// <summary>Whether the avatar has stood still long enough to be idle.</summary>
public sealed class UserIsIdleVariable(RoomGrain roomGrain)
    : UserFlagVariable<IRoomAvatar>(roomGrain)
{
    protected override string VariableName => "@is_idle";

    protected override WiredVariableGroupSubBandType SubBandType =>
        WiredVariableGroupSubBandType.Meta;

    protected override ushort Order => 40;

    protected override bool HasFlag(IRoomAvatar avatar) => avatar.IsIdle;
}
