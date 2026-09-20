using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.User;

/// <summary>Whether the player is muted in this room right now.</summary>
public sealed class UserIsMutedVariable(RoomGrain roomGrain) : UserVariable<IRoomPlayer>(roomGrain)
{
    private const int TRUE = 1;
    private const int FALSE = 0;

    protected override string VariableName => "@is_muted";

    protected override WiredVariableGroupSubBandType SubBandType =>
        WiredVariableGroupSubBandType.Meta;

    protected override ushort Order => 90;

    protected override WiredVariableFlags Flags => WiredVariableFlags.None;

    protected override WiredVariableValue GetValueForAvatar(IRoomPlayer avatar) =>
        WiredVariableValue.Parse(
            _roomGrain.ModerationModule.GetRemainingMuteSeconds(avatar.PlayerId) > 0 ? TRUE : FALSE
        );
}
