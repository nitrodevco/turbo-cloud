using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor;

[RoomObjectLogic("gate")]
public class FurnitureGateLogic(IStuffDataFactory stuffDataFactory, IRoomFloorItemContext ctx)
    : FurnitureFloorLogic(stuffDataFactory, ctx)
{
    public override FurnitureUsageType GetUsagePolicy() => FurnitureUsageType.Controller;

    public override bool CanWalk()
    {
        var state = StuffData.GetState();

        if (state == GateStates.OPEN)
            return true;

        return false;
    }

    public override async Task OnUseAsync(ActionContext ctx, int param, CancellationToken ct)
    {
        var tile = await _ctx.GetTileSnapshotAsync(ct);

        if (tile.Flags.Has(RoomTileFlags.AvatarOccupied))
            return;

        await base.OnUseAsync(ctx, param, ct);
    }
}
