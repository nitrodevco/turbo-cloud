using System;
using System.Collections.Generic;
using System.Linq;
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
/// Puts the selected users in a team. Params: the team, and the join mode: the chosen team,
/// the team with the fewest members, or a random team.
/// </summary>
[RoomObjectLogic("wf_act_join_team")]
public class WiredActionJoinTeam(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredActionLogic(grainFactory, stuffDataFactory, ctx)
{
    private const int MODE_CHOSEN = 0;
    private const int MODE_SMALLEST = 1;
    private const int MODE_RANDOM = 2;

    private static readonly GameTeamType[] TEAMS =
    [
        GameTeamType.Red,
        GameTeamType.Green,
        GameTeamType.Blue,
        GameTeamType.Yellow,
    ];

    public override int WiredCode => (int)WiredActionType.JOIN_TEAM;

    public override List<IWiredParamRule> GetIntParamRules() =>
        [
            new WiredEnumParamRule<GameTeamType>(GameTeamType.Red, TEAMS),
            new WiredRangeParamRule(0, 2, 0),
        ];

    public override List<WiredPlayerSourceType[]> GetAllowedPlayerSources() => [WiredSources.Users];

    public override async Task<bool> ExecuteAsync(IWiredExecutionContext ctx, CancellationToken ct)
    {
        var selection = ctx.GetSelection(this);
        var mode = GetIntParamOrDefault(1, MODE_CHOSEN);
        var joined = false;

        foreach (var playerId in selection.SelectedPlayerIds)
        {
            var team = mode switch
            {
                MODE_SMALLEST => TEAMS
                    .OrderBy(t => _roomGrain.GameSystem.GetTeamMembers(t).Count())
                    .First(),
                MODE_RANDOM => TEAMS[Random.Shared.Next(TEAMS.Length)],
                _ => GetIntParamOrDefault(0, GameTeamType.Red),
            };

            joined |= await _roomGrain.GameSystem.JoinTeamAsync(playerId, team, ct);
        }

        return joined;
    }
}
