using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.User;

/// <summary>Whether the player is muted in this room right now.</summary>
public sealed class UserIsMutedVariable(RoomGrain roomGrain)
    : UserFlagVariable<IRoomPlayer>(roomGrain)
{
    protected override string VariableName => "@is_muted";

    protected override WiredVariableGroupSubBandType SubBandType =>
        WiredVariableGroupSubBandType.Meta;

    protected override ushort Order => 90;

    protected override bool HasFlag(IRoomPlayer avatar) =>
        _roomGrain.ModerationModule.GetRemainingMuteSeconds(avatar.PlayerId) > 0;
}
