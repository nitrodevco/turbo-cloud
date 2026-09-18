using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture;
using Turbo.Primitives.Furniture.Interactions;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Messages.Outgoing.Room.Furniture;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Furniture;
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

        if (!await HasRightsAsync(ctx))
            return Reject(ctx, interaction, "no rights");

        Altitude? target = interaction switch
        {
            SetStackHeightInteraction set => Resolve(set.Height),
            SetAdjacentStackHeightInteraction adjacent => Adjacent(adjacent.Down),
            _ => null,
        };

        if (target is not { } z)
            return Reject(ctx, interaction, "no height to move to");

        var item = _ctx.RoomObject;

        if (
            !await _roomGrain.FurniModule.MoveFloorItemByIdAsync(
                ctx,
                item.ObjectId,
                item.X,
                item.Y,
                z,
                null,
                ct
            )
        )
            return false;

        await _ctx.SendComposerToRoomAsync(
            new CustomStackingHeightUpdateMessageComposer
            {
                FurniId = _ctx.ObjectId,
                Height = z.ToInt(),
            }
        );

        return true;
    }

    private Altitude? Resolve(int height)
    {
        if (height == CustomStackHeights.ABOVE_STACK)
            return OtherTops().DefaultIfEmpty(Altitude.Zero).Max(x => x.Value);

        if (height < 0)
            return null;

        var max = _roomGrain._roomConfig.MaxStackHeight.Value;

        return Math.Min(Altitude.FromInt(height).Value, max);
    }

    /// <summary>The next item top above (or below) the tile's current height, if any.</summary>
    private Altitude? Adjacent(bool down)
    {
        var current = _ctx.RoomObject.Z.Value;
        var tops = OtherTops().Select(x => x.Value);

        var candidates = down
            ? tops.Where(x => x < current).OrderByDescending(x => x)
            : tops.Where(x => x > current).OrderBy(x => x);

        return candidates.Cast<double?>().FirstOrDefault() is { } next ? next : null;
    }

    /// <summary>Top surfaces of every other floor item sharing this tile.</summary>
    private IEnumerable<Altitude> OtherTops()
    {
        var idx = _ctx.GetTileIdx();
        var stacks = _roomGrain._state.TileFloorStacks;

        if (idx < 0 || idx >= stacks.Length)
            return [];

        return stacks[idx]
            .Where(id => id != _ctx.ObjectId)
            .Select(id => _roomGrain._state.ItemsById.TryGetValue(id, out var other) ? other : null)
            .OfType<IRoomItem>()
            .Select(other => (Altitude)(other.Z.Value + other.GetStackHeight().Value));
    }
}
