using System.Collections.Generic;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Wired.Rules;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;

/// <summary>Gives points to a fixed team (param 2) rather than the team of the user.</summary>
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
            new WiredRangeParamRule(-1000, 1000, 1),
            new WiredRangeParamRule(0, 10, 1),
            new WiredEnumParamRule<WiredTeamType>(
                WiredTeamType.Red,
                WiredTeamType.Red,
                WiredTeamType.Green,
                WiredTeamType.Blue,
                WiredTeamType.Yellow
            ),
        ];

    protected override WiredTeamType ResolveTeam(int playerId) =>
        GetIntParamOrDefault(2, WiredTeamType.Red);
}
