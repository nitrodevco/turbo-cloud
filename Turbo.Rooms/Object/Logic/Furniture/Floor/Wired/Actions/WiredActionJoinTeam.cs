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
/// Puts the selected users in a team. Params, as the client's editor saves them: the team, and
/// the kind of team (<c>wiredfurni.params.team_type.0</c> to <c>.2</c>: Wired, Battle Banzai,
/// Freeze), which sets the team effect they wear.
/// </summary>
[RoomObjectLogic("wf_act_join_team")]
public class WiredActionJoinTeam(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredActionLogic(grainFactory, stuffDataFactory, ctx)
{
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
            new WiredEnumParamRule<WiredTeamType>(WiredTeamType.Wired),
        ];

    public override List<WiredPlayerSourceType[]> GetAllowedPlayerSources() => [WiredSources.Users];

    public override async Task<bool> ExecuteAsync(IWiredExecutionContext ctx, CancellationToken ct)
    {
        var selection = ctx.GetSelection(this);
        var team = GetIntParamOrDefault(0, GameTeamType.Red);
        var teamType = GetIntParamOrDefault(1, WiredTeamType.Wired);
        var joined = false;

        foreach (var player in GetPlayers(selection))
            joined |= await GameSystem.JoinTeamAsync(player.PlayerId, team, teamType, ct);

        return joined;
    }
}
