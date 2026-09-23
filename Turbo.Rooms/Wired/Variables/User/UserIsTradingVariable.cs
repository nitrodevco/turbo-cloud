using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.User;

/// <summary>Whether the player has a trade open.</summary>
public sealed class UserIsTradingVariable(RoomGrain roomGrain)
    : UserFlagVariable<IRoomPlayer>(roomGrain)
{
    protected override string VariableName => "@is_trading";

    protected override WiredVariableGroupSubBandType SubBandType =>
        WiredVariableGroupSubBandType.Meta;

    protected override ushort Order => 80;

    protected override bool HasFlag(IRoomPlayer avatar) =>
        avatar.HasStatus(AvatarStatusType.Trading);
}
