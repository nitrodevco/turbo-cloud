using System.Collections.Generic;
using System.Linq;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Wired;
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
        [new WiredEnumParamRule<WiredTeamType>(WiredTeamType.None)];

    public override List<WiredPlayerSourceType[]> GetAllowedPlayerSources() =>
        [
            [
                WiredPlayerSourceType.TriggeredUser,
                WiredPlayerSourceType.SelectorUsers,
                WiredPlayerSourceType.SignalUsers,
            ],
        ];

    protected override bool EvaluateCore(IWiredProcessingContext ctx)
    {
        var wanted = GetIntParamOrDefault(0, WiredTeamType.None);
        var players = ctx.GetSelection(this).SelectedPlayerIds;

        return Quantify(
            players.Select(playerId =>
            {
                var team = _roomGrain.WiredSystem.GetTeam(playerId);

                return wanted == WiredTeamType.None ? team != WiredTeamType.None : team == wanted;
            }),
            true
        );
    }
}
