using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.User;

/// <summary>
/// The bot's own id, the one its row is kept under, which outlives its being in this room.
/// Only a bot holds it: a player or a pet has none.
/// </summary>
public sealed class UserBotIdVariable(RoomGrain roomGrain) : UserValueVariable<IRoomBot>(roomGrain)
{
    protected override string VariableName => "@bot_id";

    protected override WiredVariableGroupSubBandType SubBandType =>
        WiredVariableGroupSubBandType.Other;

    protected override ushort Order => 70;

    protected override WiredVariableFlags Flags => WiredVariableFlags.HasValue;

    protected override WiredVariableValue GetValueForAvatar(IRoomBot avatar) =>
        WiredVariableValue.Parse(avatar.BotId);
}
