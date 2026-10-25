using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture;
using Turbo.Primitives.Furniture.Interactions;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor;

/// <summary>
/// A magic stack tile: its own height is what the client edits, in hundredths of a tile, so
/// furniture placed on it stacks at that height. -100 rests it on whatever else is on the tile.
/// </summary>
[RoomObjectLogic("custom_stack_height")]
public class FurnitureStackTileLogic(IStuffDataFactory stuffDataFactory, IRoomFloorItemContext ctx)
    : FurnitureFloorLogic(stuffDataFactory, ctx)
{
    public override FurnitureUsageType GetUsagePolicy() => FurnitureUsageType.Controller;

    /// <summary>
    /// "Adjust the height of stacking helper as you want, then move the other furni on top of
    /// it" (widget.custom.stack.height.text): whatever its definition says, furni go on it.
    /// </summary>
    public override bool CanStack() => true;

    public override Task OnUseAsync(ActionContext ctx, int param, CancellationToken ct) =>
        Task.CompletedTask;

    public override async Task<bool> OnInteractAsync(
        ActionContext ctx,
        FurnitureInteraction interaction,
        CancellationToken ct
    )
    {
        if (interaction is not (SetStackHeightInteraction or SetAdjacentStackHeightInteraction))
            return false;

        // Flash opens the helper's window for rights and for the helper's own owner too
        // (FurnitureCustomStackHeightWidgetHandler.as:143-150 validateRights, WIN63-202610201032).
        if (!IsItemOwner(ctx) && !await HasRightsAsync(ctx))
            return Reject(ctx, interaction, "no rights");

        // A move that goes nowhere is still answered with the helper where it is (lab49).
        Altitude? target = interaction switch
        {
            SetStackHeightInteraction set => Resolve(set.Height),
            SetAdjacentStackHeightInteraction adjacent => Adjacent(adjacent.Down),
            _ => null,
        };

        if (target is not { } z)
            return Reject(ctx, interaction, "no height to move to");

        var item = _ctx.RoomObject;

        // The move's ObjectUpdate is the answer: the helper reads the tile's height from where it
        // stands (FurnitureCustomStackHeightWidgetHandler.update), and Flash has no message of its own for it.
        return await FurniModule.MoveFloorItemByIdAsync(
            ctx,
            item.ObjectId,
            item.X,
            item.Y,
            z,
            null,
            ct
        );
    }

    private Altitude? Resolve(int height)
    {
        if (height == CustomStackHeights.ABOVE_STACK)
            return OtherTops().DefaultIfEmpty(Altitude.Zero).Max(x => x.Value);

        if (height < 0)
            return null;

        var max = RoomConfig.MaxStackHeight.Value;

        return Math.Min(Altitude.FromInt(height).Value, max);
    }

    /// <summary>
    /// Move up / Move down (official client capture lab49, build 1745, 26/26 clicks answered by the
    /// helper's ObjectUpdate alone): up stacks it one furni higher, by the height of what rests on
    /// its tile (a 1-high table at 0: 1, 2, 3), and not at all with nothing of any height there
    /// (alone or on a rug: stays). Down goes to the next lower top of what rests there, then the
    /// floor (3, 1, 0, 0). Furni on the neighbouring tiles and other helpers play no part.
    /// Inference: with furni of different heights on the tile the tallest sets the step (only
    /// 1-high furni were tried).
    /// </summary>
    private Altitude Adjacent(bool down)
    {
        var current = _ctx.RoomObject.Z.Value;
        var others = FurniModule
            .GetFloorItemsOnTile(_ctx.GetTileIdx())
            .Where(other => other.ObjectId != _ctx.ObjectId)
            .ToList();

        if (!down)
        {
            var step = others
                .Select(x => x.GetStackHeight().Value)
                .Where(x => x > 0)
                .DefaultIfEmpty(0)
                .Max();

            return step > 0 ? Math.Min(current + step, RoomConfig.MaxStackHeight.Value) : current;
        }

        var floor = State.Model?.BaseHeights[_ctx.GetTileIdx()].Value ?? 0;
        var below = others
            .Select(x => x.Z.Value + x.GetStackHeight().Value)
            .Where(x => x < current && x > floor)
            .DefaultIfEmpty(floor)
            .Max();

        return Math.Min(below, current);
    }

    /// <summary>Top surfaces of every other floor item sharing this tile.</summary>
    private IEnumerable<Altitude> OtherTops()
    {
        // The furni module reads the stack, and answers nothing for a tile off the map.
        return FurniModule
            .GetFloorItemsOnTile(_ctx.GetTileIdx())
            .Where(other => other.ObjectId != _ctx.ObjectId)
            .Select(other => (Altitude)(other.Z.Value + other.GetStackHeight().Value));
    }
}
