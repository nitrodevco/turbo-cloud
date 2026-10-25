using System;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor;

/// <summary>
/// A building block whose height follows its state (<see cref="MultiHeightFurniture"/>): each
/// state stands one step lower, never below the floor. What stands or stacks on it goes with it,
/// and the room is sent its new height, as the client draws stacking from the item's height. A
/// user standing on it does not stop a change, but furni stacked on it does: Toggle Furni State
/// leaves a block with another block on top out of its state update, and toggles the top one
/// (official client capture 2026-10-24, WIN63-202610201032: covered 0/5, top 2/2, again after the
/// top one was picked up 2/2; with a user on it 5/5; action.0.B4, action.0.B3).
/// </summary>
[RoomObjectLogic(MultiHeightFurniture.LOGIC_NAME)]
public class FurnitureMultiHeightLogic(
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureFloorLogic(stuffDataFactory, ctx)
{
    public override Altitude GetStackHeight()
    {
        var full = base.GetStackHeight();
        var states = _ctx.Definition.TotalStates;

        if (states <= 1)
            return full;

        var step = MultiHeightFurniture.StepOf(_ctx.Definition) ?? full.Value / (states - 1);

        return MultiHeightFurniture.HeightAt(full.Value, GetState(), states, step);
    }

    public override bool CanChangeState() => !IsCovered();

    // Nor by hand: a use of a covered block got no reply and left its state (official client
    // capture 2026-10-24 22-26-27, wired matrix trigger.4.B4 / action.0.B4c, 2/2; the top block
    // and uncovered blocks toggled 7/7).
    public override async Task OnUseAsync(ActionContext ctx, int param, CancellationToken ct)
    {
        if (!CanChangeState())
            return;

        await base.OnUseAsync(ctx, param, ct);
    }

    /// <summary>
    /// Whether another furni stands on one of its tiles at or above it: higher up, or at the same
    /// altitude stacked after it (a block at its lowest state is 0 high, and the one put on it
    /// stands at its altitude).
    /// </summary>
    private bool IsCovered()
    {
        var self = _ctx.RoomObject;

        foreach (var tile in FloorFootprint.Of(self).Tiles())
        {
            var passedSelf = false;

            foreach (var item in FurniModule.GetFloorItemsOnTile(MapModule.ToIdx(tile.X, tile.Y)))
            {
                if (item.ObjectId == self.ObjectId)
                {
                    passedSelf = true;

                    continue;
                }

                if (item.Z > self.Z || (passedSelf && item.Z == self.Z))
                    return true;
            }
        }

        return false;
    }

    public override async Task OnStateChangedAsync(CancellationToken ct)
    {
        await base.OnStateChangedAsync(ct);

        await _ctx.UpdateItemAsync();
    }
}
