using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.User;

/// <summary>Whether a wired freeze is holding the avatar still.</summary>
public sealed class UserIsFrozenVariable(RoomGrain roomGrain)
    : UserFlagVariable<IRoomAvatar>(roomGrain)
{
    protected override string VariableName => "@is_frozen";

    protected override WiredVariableGroupSubBandType SubBandType =>
        WiredVariableGroupSubBandType.Meta;

    protected override ushort Order => 100;

    protected override bool HasFlag(IRoomAvatar avatar) => avatar.IsFrozen;
}
