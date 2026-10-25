using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Games;

/// <summary>
/// A team gate (<c>bb_gate_*</c> for Battle Banzai, <c>es_gate_*</c> for Freeze). Stepping on it
/// puts the player in its team, wearing that game's team effect: a player put on the Freeze red
/// gate wore 40 and the gate went from 0 to 1 the same millisecond (official client capture
/// 2026-10-18T09-00-17).
/// Its state is how many are in its colour's team, whichever game they joined it for, so the
/// Banzai and the Freeze gate of a colour always show the same number: in four rooms with both
/// (official client captures 2026-10-17T18-24-44, 22-42-57, 22-45-40, 22-46-19) every colour's
/// two gates agreed, from 0 to 5, and a player joining green moved both green gates together.
/// It shows no more than 5 (Sulake's <c>es_gate</c> has states 0 to 5, the <c>bb_gate</c> asset
/// animations 0 to 5).
/// Inference, not yet seen on Habbo: stepping on your own team's gate takes you out of the team,
/// a team full at 5 takes nobody else through its gate, and the gates do nothing while a game runs.
/// </summary>
[RoomObjectLogic(LOGIC_NAME)]
public class FurnitureTeamGateLogic(IStuffDataFactory stuffDataFactory, IRoomFloorItemContext ctx)
    : FurnitureFloorLogic(stuffDataFactory, ctx),
        IRoomGameFurni
{
    public const string LOGIC_NAME = "game_team_gate";

    /// <summary>The most a gate shows, and the most players its team takes through a gate.</summary>
    public const int MAX_TEAM_SIZE = 5;

    // The count is of the teams in the room now, which do not outlive it.
    protected override StuffPersistanceType _stuffPersistanceType =>
        StuffPersistanceType.RoomActive;

    private GameTeamType Team =>
        GameFurniture.TryParseGate(_ctx.Definition.Name, out var team, out _)
            ? team
            : GameTeamType.None;

    private WiredTeamType TeamType =>
        GameFurniture.TryParseGate(_ctx.Definition.Name, out _, out var type)
            ? type
            : WiredTeamType.Wired;

    public override FurnitureUsageType GetUsagePolicy() => FurnitureUsageType.Nobody;

    public override Task OnAttachAsync(CancellationToken ct)
    {
        StuffData.SetState(ShownCount().ToString());

        return base.OnAttachAsync(ct);
    }

    public override async Task OnWalkOnAsync(IRoomAvatarContext ctx, CancellationToken ct)
    {
        await base.OnWalkOnAsync(ctx, ct);

        if (Team == GameTeamType.None || GameSystem.IsGameRunning)
            return;

        var avatarId = ctx.ObjectId;
        var current = GameSystem.GetTeam(avatarId);

        if (current == Team && GameSystem.GetTeamType(avatarId) == TeamType)
        {
            await GameSystem.LeaveTeamAsync(avatarId, ct);

            return;
        }

        if (current != Team && GameSystem.GetTeamMembers(Team).Count() >= MAX_TEAM_SIZE)
            return;

        if (current != GameTeamType.None)
            await GameSystem.LeaveTeamAsync(avatarId, ct);

        await GameSystem.JoinTeamAsync(avatarId, Team, TeamType, ct);
    }

    public Task<bool> OnGameChangedAsync(RoomGameChange change, CancellationToken ct)
    {
        var count = ShownCount();

        if (GetState() == count)
            return Task.FromResult(false);

        StuffData.SetState(count.ToString());

        return Task.FromResult(true);
    }

    private int ShownCount() =>
        Team == GameTeamType.None
            ? 0
            : Math.Min(MAX_TEAM_SIZE, GameSystem.GetTeamMembers(Team).Count());
}
