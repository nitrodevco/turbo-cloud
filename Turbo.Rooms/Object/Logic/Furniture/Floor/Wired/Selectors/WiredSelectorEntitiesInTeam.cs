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
        [new WiredEnumParamRule<GameTeamType>(GameTeamType.None)];

    public override Task<IWiredSelectionSet> SelectAsync(
        IWiredProcessingContext ctx,
        CancellationToken ct
    )
    {
        var output = new WiredSelectionSet();
        var wanted = GetIntParamOrDefault(0, GameTeamType.None);

        foreach (
            var team in new[]
            {
                GameTeamType.Red,
                GameTeamType.Green,
                GameTeamType.Blue,
                GameTeamType.Yellow,
            }
        )
        {
            if (wanted != GameTeamType.None && wanted != team)
                continue;

            foreach (var playerId in _roomGrain.GameSystem.GetTeamMembers(team))
            {
                // A team is kept by player id; a selection names the avatar.
                if (_roomGrain.AvatarModule.TryGetPlayer(playerId, out var member))
                    output.SelectedAvatarIds.Add(member.ObjectId);
            }
        }

        return Task.FromResult<IWiredSelectionSet>(output);
    }
}
