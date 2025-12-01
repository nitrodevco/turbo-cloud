using System.Threading;
using System.Threading.Tasks;
using Turbo.Contracts.Enums.Furniture;
using Turbo.Primitives.Action;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Furniture.StuffData;
using Turbo.Primitives.Rooms.Object.Logic;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor;

[RoomObjectLogic("roller")]
public class FurnitureRollerLogic(IStuffDataFactory stuffDataFactory, IRoomFloorItemContext ctx)
    : FurnitureFloorLogic(stuffDataFactory, ctx)
{
    private const int CLOSED_STATE = 0;
    private const int OPEN_STATE = 1;

    public override FurniUsagePolicy GetUsagePolicy() => FurniUsagePolicy.Controller;

    public override async Task OnUseAsync(ActionContext ctx, int param, CancellationToken ct)
    {
        var tile = await _ctx.GetTileSnapshotAsync(ct);

        if (tile.Flags.Has(RoomTileFlags.AvatarOccupied))
            return;

        await base.OnUseAsync(ctx, param, ct);
    }
}
