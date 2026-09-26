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
/// Brings the selected users to one of the picked furni: walking, sliding or teleporting
/// (param 0). A random picked furni is chosen per user.
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
        [new WiredEnumParamRule<WiredUserWalkModeType>(WiredUserWalkModeType.Walk)];

    public override List<WiredFurniSourceType[]> GetAllowedFurniSources() => [WiredSources.Furni];

    public override List<WiredPlayerSourceType[]> GetAllowedPlayerSources() => [WiredSources.Users];

    public override async Task<bool> ExecuteAsync(IWiredExecutionContext ctx, CancellationToken ct)
    {
        var selection = ctx.GetSelection(this);
        var items = GetFloorItems(selection);
        var players = GetAvatars(selection);

        if (items.Count == 0 || players.Count == 0)
            return false;

        var mode = GetIntParamOrDefault(0, WiredUserWalkModeType.Walk);
        var map = _roomGrain.MapModule;
        var moved = false;

        foreach (var player in players)
        {
            var target = items[Random.Shared.Next(items.Count)];
            var tileIdx = map.ToIdx(target.X, target.Y);

            moved |= mode switch
            {
                WiredUserWalkModeType.Walk => !player.IsFrozen
                    && await _roomGrain.AvatarModule.WalkAvatarToAsync(
                        player,
                        target.X,
                        target.Y,
                        ct
                    ),
                WiredUserWalkModeType.Slide => await ctx.ProcessUserMovementAsync(
                    player,
                    tileIdx,
                    SlideAvatarMoveType.Slide
                ),
                _ => await TeleportAvatarAsync(ctx, player, tileIdx),
            };
        }

        return moved;
    }
}
