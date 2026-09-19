using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Wired;
using Turbo.Rooms.Wired.Rules;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Selectors;

/// <summary>Picks the members of a team (param 0), or of every team when zero.</summary>
[RoomObjectLogic("wf_slc_users_team")]
public class WiredSelectorEntitiesInTeam(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredSelectorLogic(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredSelectorType.USERS_IN_TEAM;

    public override List<IWiredParamRule> GetIntParamRules() =>
        [new WiredEnumParamRule<WiredTeamType>(WiredTeamType.None)];

    public override Task<IWiredSelectionSet> SelectAsync(
        IWiredProcessingContext ctx,
        CancellationToken ct
    )
    {
        var output = new WiredSelectionSet();
        var wanted = GetIntParamOrDefault(0, WiredTeamType.None);

        foreach (
            var team in new[]
            {
                WiredTeamType.Red,
                WiredTeamType.Green,
                WiredTeamType.Blue,
                WiredTeamType.Yellow,
            }
        )
        {
            if (wanted != WiredTeamType.None && wanted != team)
                continue;

            foreach (var playerId in _roomGrain.WiredSystem.GetTeamMembers(team))
                output.SelectedPlayerIds.Add(playerId);
        }

        return Task.FromResult<IWiredSelectionSet>(output);
    }
}
