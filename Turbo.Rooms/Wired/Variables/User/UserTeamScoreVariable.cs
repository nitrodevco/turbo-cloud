using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.User;

/// <summary>The score of the game team the player is on, or zero when they are on none.</summary>
public sealed class UserTeamScoreVariable(RoomGrain roomGrain)
    : UserValueVariable<IRoomPlayer>(roomGrain)
{
    protected override string VariableName => "@team.score";

    protected override WiredVariableGroupSubBandType SubBandType =>
        WiredVariableGroupSubBandType.Meta;

    protected override ushort Order => 150;

    protected override WiredVariableFlags Flags =>
        WiredVariableFlags.HasValue | WiredVariableFlags.CanWriteValue;

    /// <summary>Writing it puts the score of the team this player is on at that number.</summary>
    public override async Task<bool> SetValueAsync(
        IWiredExecutionContext ctx,
        WiredVariableKey key,
        WiredVariableValue value
    )
    {
        if (!CanBind(key) || !TryGetAvatarForKey(key, out var avatar))
            return false;

        var team = _roomGrain.GameSystem.GetTeam(avatar.PlayerId);

        return await _roomGrain.GameSystem.SetScoreAsync(team, value, CancellationToken.None);
    }

    protected override WiredVariableValue GetValueForAvatar(IRoomPlayer avatar) =>
        WiredVariableValue.Parse(
            _roomGrain.GameSystem.GetScore(_roomGrain.GameSystem.GetTeam(avatar.PlayerId))
        );
}
