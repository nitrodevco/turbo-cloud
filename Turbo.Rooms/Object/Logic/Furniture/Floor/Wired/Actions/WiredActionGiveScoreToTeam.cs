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
using Turbo.Rooms.Wired.Rules;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;

/// <summary>Gives points to a fixed team (param 2) rather than the teams of users.</summary>
[RoomObjectLogic("wf_act_give_score_tm")]
public class WiredActionGiveScoreToTeam(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : WiredActionGiveScore(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredActionType.GIVE_SCORE_TO_PREDEFINED_TEAM;

    public override List<IWiredParamRule> GetIntParamRules() =>
        [
            new WiredRangeParamRule(-1000, 1000, 5),
            new WiredRangeParamRule(0, 10, 0),
            new WiredEnumParamRule<GameTeamType>(
                GameTeamType.Red,
                GameTeamType.Red,
                GameTeamType.Green,
                GameTeamType.Blue,
                GameTeamType.Yellow
            ),
        ];

    // No user picker: the official server offers this box no user sources (fresh box, users []
    // [], 2026-10-20T17-39-35-Open-wait), so the team scores once per run and the "times in a
    // game" limit counts runs of the box (inference: there is no user to count it by).
    public override List<WiredPlayerSourceType[]> GetAllowedPlayerSources() => [];

    public override Task<bool> ExecuteAsync(IWiredExecutionContext ctx, CancellationToken ct) =>
        GameSystem.GiveScoreAsync(
            GetIntParamOrDefault(2, GameTeamType.Red),
            GetIntParamOrDefault(0, 5),
            ObjectId,
            default,
            GetIntParamOrDefault(1, 0),
            ct
        );
}
