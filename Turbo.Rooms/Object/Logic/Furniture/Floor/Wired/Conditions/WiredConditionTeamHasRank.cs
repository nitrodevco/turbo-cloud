using System.Collections.Generic;
using System.Linq;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Wired.Rules;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Conditions;

/// <summary>
/// True when a team holds a placement. Param 0 is the team (zero: the triggerer's team),
/// param 1 the placement index (zero for first).
/// </summary>
[RoomObjectLogic("wf_cnd_team_has_rank")]
public class WiredConditionTeamHasRank(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredConditionLogic(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredConditionType.TEAM_IS_WINNING;

    public override List<IWiredParamRule> GetIntParamRules() =>
        [new WiredEnumParamRule<GameTeamType>(GameTeamType.None), new WiredRangeParamRule(0, 3, 0)];

    protected override bool EvaluateCore(IWiredProcessingContext ctx)
    {
        var placement = GetIntParamOrDefault(1, 0) + 1;

        foreach (var team in ResolveTeams(GetIntParamOrDefault(0, GameTeamType.None), ctx))
        {
            if (_roomGrain.GameSystem.GetPlacement(team) == placement)
                return true;
        }

        return false;
    }

    /// <summary>The configured team, or the teams of the triggering users when set to "triggerer".</summary>
    protected IEnumerable<GameTeamType> ResolveTeams(
        GameTeamType configured,
        IWiredProcessingContext ctx
    )
    {
        if (configured != GameTeamType.None)
            return [configured];

        return GetPlayers(ctx.Selected)
            .Select(x => _roomGrain.GameSystem.GetTeam(x.PlayerId))
            .Where(x => x != GameTeamType.None)
            .Distinct()
            .ToList();
    }
}
