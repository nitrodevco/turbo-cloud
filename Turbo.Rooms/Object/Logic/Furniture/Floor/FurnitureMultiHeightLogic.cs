using System;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Furniture;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor;

/// <summary>
/// A building block whose height follows its state (<see cref="MultiHeightFurniture"/>): each
/// state stands one step lower, never below the floor. What stands or stacks on it goes with it,
/// and the room is sent its new height, as the client draws stacking from the item's height.
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

        var state = Math.Clamp(GetState(), 0, states - 1);
        var step = MultiHeightFurniture.StepOf(_ctx.Definition) ?? full.Value / (states - 1);

        return Math.Round(Math.Max(0, full.Value - (state * step)), 2);
    }

    public override async Task OnStateChangedAsync(CancellationToken ct)
    {
        await base.OnStateChangedAsync(ct);

        await _ctx.UpdateItemAsync();
    }
}
