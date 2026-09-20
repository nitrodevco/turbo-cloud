using System.Collections.Generic;
using System.Linq;
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

/// <summary>True when the triggering users are in the given team (zero: in any team).</summary>
[RoomObjectLogic("wf_cnd_actor_in_team")]
public class WiredConditionHabboInTeam(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredConditionLogic(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredConditionType.ACTOR_IS_IN_TEAM;

    public override List<IWiredParamRule> GetIntParamRules() =>
        [new WiredEnumParamRule<GameTeamType>(GameTeamType.None)];

    public override List<WiredPlayerSourceType[]> GetAllowedPlayerSources() => [WiredSources.Users];

    protected override bool EvaluateCore(IWiredProcessingContext ctx)
    {
        var wanted = GetIntParamOrDefault(0, GameTeamType.None);
        var players = GetPlayers(ctx.GetSelection(this));

        return Quantify(
            players.Select(player =>
            {
                var team = _roomGrain.GameSystem.GetTeam(player.PlayerId);

                return wanted == GameTeamType.None ? team != GameTeamType.None : team == wanted;
            }),
            true
        );
    }
}
