using System.Collections.Generic;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Wired;
using Turbo.Rooms.Wired.Rules;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Conditions;

/// <summary>
/// Compares a team score to a value. Params: team (zero: the triggerer's team), points, and
/// the three-way comparison (less, equal, greater).
/// </summary>
[RoomObjectLogic("wf_cnd_team_has_score")]
public class WiredConditionTeamHasScore(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : WiredConditionTeamHasRank(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredConditionType.TEAM_HAS_SCORE;

    public override List<IWiredParamRule> GetIntParamRules() =>
        [
            new WiredEnumParamRule<GameTeamType>(GameTeamType.None),
            new WiredRangeParamRule(0, 1000, 0),
            new WiredRangeParamRule(0, 2, 1),
        ];

    protected override bool EvaluateCore(IWiredProcessingContext ctx)
    {
        var points = GetIntParamOrDefault(1, 0);
        var comparison = GetIntParamOrDefault(2, 1);

        foreach (var team in ResolveTeams(GetIntParamOrDefault(0, GameTeamType.None), ctx))
        {
            if (
                WiredComparison.CompareThreeWay(
                    comparison,
                    _roomGrain.GameSystem.GetScore(team),
                    points
                )
            )
                return true;
        }

        return false;
    }
}
