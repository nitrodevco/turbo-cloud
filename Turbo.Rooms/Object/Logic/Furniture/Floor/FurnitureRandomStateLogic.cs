using System;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor;

/// <summary>
/// Furniture that lands on a random state when used instead of cycling to the next one. The
/// client sends <c>SetRandomState</c> for these, which routes through the ordinary use path.
/// </summary>
[RoomObjectLogic("random_state")]
public class FurnitureRandomStateLogic(
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureFloorLogic(stuffDataFactory, ctx)
{
    public override async Task OnUseAsync(ActionContext ctx, int param, CancellationToken ct)
    {
        var totalStates = _ctx.Definition.TotalStates;

        if (totalStates <= 1)
            return;

        // Always move: a random pick that lands on the current state reads as a dead click.
        var next = Random.Shared.Next(totalStates - 1);

        if (next >= GetState())
            next++;

        await SetStateAsync(next);
    }
}
