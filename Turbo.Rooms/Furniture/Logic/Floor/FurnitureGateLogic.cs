using System.Threading;
using System.Threading.Tasks;
using Turbo.Contracts.Enums.Furniture;
using Turbo.Primitives.Actor;
using Turbo.Primitives.Rooms.Furniture.Floor;
using Turbo.Primitives.Rooms.Furniture.Logic;
using Turbo.Primitives.Rooms.Furniture.StuffData;
using Turbo.Primitives.Rooms.Mapping;

namespace Turbo.Rooms.Furniture.Logic.Floor;

[FurnitureLogic("gate")]
public class FurnitureGateLogic(IStuffDataFactory stuffDataFactory, IRoomFloorItemContext ctx)
    : FurnitureFloorLogic(stuffDataFactory, ctx)
{
    private const int CLOSED_STATE = 0;
    private const int OPEN_STATE = 1;

    public override FurniUsagePolicy GetUsagePolicy() => FurniUsagePolicy.Controller;

    public override async Task OnUseAsync(ActorContext ctx, int param, CancellationToken ct)
    {
        var tile = await _ctx.GetTileSnapshotAsync(ct);

        if (tile.Flags.Has(RoomTileFlags.AvatarOccupied))
            return;

        await base.OnUseAsync(ctx, param, ct);
    }
}
