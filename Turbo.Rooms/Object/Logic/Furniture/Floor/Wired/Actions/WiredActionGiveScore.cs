using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Wired;
using Turbo.Rooms.Wired.Rules;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;

/// <summary>
/// Gives points to the teams of the selected users. Params: signed points and how many times
/// per game one user may score through this box (zero: unlimited).
/// </summary>
[RoomObjectLogic("wf_act_give_score")]
public class WiredActionGiveScore(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredActionLogic(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredActionType.GIVE_SCORE;

    public override List<IWiredParamRule> GetIntParamRules() =>
        [new WiredRangeParamRule(-1000, 1000, 1), new WiredRangeParamRule(0, 10, 1)];

    public override List<WiredPlayerSourceType[]> GetAllowedPlayerSources() => [WiredSources.Users];

    public override async Task<bool> ExecuteAsync(IWiredExecutionContext ctx, CancellationToken ct)
    {
        var points = GetIntParamOrDefault(0, 1);
        var timesPerGame = GetIntParamOrDefault(1, 1);
        var selection = ctx.GetSelection(this);
        var scored = false;

        foreach (var playerId in selection.SelectedPlayerIds)
        {
            var team = ResolveTeam(playerId);

            if (team == GameTeamType.None)
                continue;

            scored |= await _roomGrain.GameSystem.GiveScoreAsync(
                team,
                points,
                ObjectId,
                playerId,
                timesPerGame,
                ct
            );
        }

        return scored;
    }

    protected virtual GameTeamType ResolveTeam(int playerId) =>
        _roomGrain.GameSystem.GetTeam(playerId);
}
