using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Furniture.Wall;

namespace Turbo.Rooms.Grains.Modules;

internal sealed partial class RoomActionModule
{
    public Task<bool> AddWallItemAsync(IRoomWallItem item, CancellationToken ct)
    {
        return _furniModule.AddWallItemAsync(item, ct);
    }

    public async Task<bool> MoveWallItemByIdAsync(
        ActionContext ctx,
        RoomObjectId objectId,
        string newLocation,
        CancellationToken ct
    )
    {
        if (!await _securityModule.CanManipulateFurniAsync(ctx))
            return false;

        return await _furniModule.MoveWallItemByIdAsync(ctx, objectId, newLocation, ct);
    }

    public Task<bool> RemoveWallItemByIdAsync(
        ActionContext ctx,
        RoomObjectId objectId,
        CancellationToken ct
    ) => _furniModule.RemoveWallItemByIdAsync(ctx, objectId, ct);

    public Task<bool> UseWallItemByIdAsync(
        ActionContext ctx,
        RoomObjectId objectId,
        CancellationToken ct,
        int param = -1
    ) => _furniModule.UseWallItemByIdAsync(ctx, objectId, ct, param);

    public Task<bool> ClickWallItemByIdAsync(
        ActionContext ctx,
        RoomObjectId objectId,
        CancellationToken ct,
        int param = -1
    ) => _furniModule.ClickWallItemByIdAsync(ctx, objectId, ct, param);
}
