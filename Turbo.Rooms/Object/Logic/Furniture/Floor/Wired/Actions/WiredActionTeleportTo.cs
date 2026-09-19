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
using Turbo.Rooms.Wired.Rules;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;

/// <summary>
/// Teleports the selected users onto one of the picked furni, chosen at random. Param 0 is
/// the client option checkbox; when set, users already on a picked furni stay put.
/// </summary>
[RoomObjectLogic("wf_act_teleport_to")]
public class WiredActionTeleportTo(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredActionLogic(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredActionType.TELEPORT;

    public override List<IWiredParamRule> GetIntParamRules() => [new WiredBoolParamRule(false)];

    public override List<WiredFurniSourceType[]> GetAllowedFurniSources() =>
        [
            [
                WiredFurniSourceType.SelectedItems,
                WiredFurniSourceType.SelectorItems,
                WiredFurniSourceType.SignalItems,
                WiredFurniSourceType.TriggeredItem,
            ],
        ];

    public override List<WiredPlayerSourceType[]> GetAllowedPlayerSources() =>
        [
            [
                WiredPlayerSourceType.TriggeredUser,
                WiredPlayerSourceType.SelectorUsers,
                WiredPlayerSourceType.SignalUsers,
            ],
        ];

    public override async Task<bool> ExecuteAsync(IWiredExecutionContext ctx, CancellationToken ct)
    {
        var selection = ctx.GetSelection(this);
        var items = GetFloorItems(selection);
        var players = GetPlayers(selection);

        if (items.Count == 0 || players.Count == 0)
            return false;

        var stayIfAlreadyThere = GetIntParamOrDefault(0, false);
        var map = _roomGrain.MapModule;
        var moved = false;

        foreach (var player in players)
        {
            var currentIdx = map.ToIdx(player.X, player.Y);

            if (stayIfAlreadyThere && items.Any(i => map.ToIdx(i.X, i.Y) == currentIdx))
                continue;

            var target = items[Random.Shared.Next(items.Count)];
            var tileIdx = map.ToIdx(target.X, target.Y);

            if (player.IsFrozen && _roomGrain.WiredSystem.FreezeCancelsOnTeleport(player.ObjectId))
                player.SetFrozen(false);

            moved |= await ctx.ProcessUserMovementAsync(player, tileIdx, SlideAvatarMoveType.None);
        }

        return moved;
    }
}
