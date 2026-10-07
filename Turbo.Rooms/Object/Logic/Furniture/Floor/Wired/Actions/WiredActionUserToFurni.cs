using System;
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

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;

/// <summary>
/// "Move user to furni", the user equivalent of "Move furni to user" (Wired Faculty, variables-info
/// #8): moves each selected user onto one of the picked furni, chosen at random per user. Param 0
/// is what happens to a walk the user was on (<c>wiredfurni.params.user_move.walkmode.0</c> to
/// <c>.2</c>): they keep walking to where they were going if the move took them closer, keep
/// walking, or stop.
/// </summary>
[RoomObjectLogic("wf_act_user_to_furni")]
public class WiredActionUserToFurni(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredActionLogic(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredActionType.MOVE_USER_TO_FURNI;

    public override List<IWiredParamRule> GetIntParamRules() =>
        [new WiredEnumParamRule<WiredUserWalkModeType>(WiredUserWalkModeType.KeepWalkingIfCloser)];

    public override List<WiredFurniSourceType[]> GetAllowedFurniSources() => [WiredSources.Furni];

    public override List<WiredPlayerSourceType[]> GetAllowedPlayerSources() => [WiredSources.Users];

    public override async Task<bool> ExecuteAsync(IWiredExecutionContext ctx, CancellationToken ct)
    {
        var selection = ctx.GetSelection(this);
        var items = GetFloorItems(selection);
        var players = GetAvatars(selection);

        if (items.Count == 0 || players.Count == 0)
            return false;

        var mode = GetIntParamOrDefault(0, WiredUserWalkModeType.KeepWalkingIfCloser);
        var map = MapModule;
        var moved = false;

        foreach (var player in players)
        {
            var target = items[Random.Shared.Next(items.Count)];
            var tileIdx = map.ToIdx(target.X, target.Y);
            // Moving stops a walk under way; where it was going decides whether it goes on.
            var goalIdx = player.IsWalking ? player.GoalTileId : -1;
            var (fromX, fromY) = (player.X, player.Y);

            if (!await ctx.ProcessUserMovementAsync(player, tileIdx, SlideAvatarMoveType.Slide))
                continue;

            moved = true;

            if (goalIdx < 0 || goalIdx == tileIdx || mode == WiredUserWalkModeType.StopWalking)
                continue;

            var (goalX, goalY) = map.GetTileXY(goalIdx);

            if (
                mode == WiredUserWalkModeType.KeepWalking
                || Steps(target.X, target.Y, goalX, goalY) < Steps(fromX, fromY, goalX, goalY)
            )
                await AvatarModule.WalkAvatarToAsync(player, goalX, goalY, ct);
        }

        return moved;
    }

    /// <summary>Tiles between two points for a walker that can step diagonally.</summary>
    private static int Steps(int x1, int y1, int x2, int y2) =>
        Math.Max(Math.Abs(x1 - x2), Math.Abs(y1 - y2));
}
