using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Actor;
using Turbo.Primitives.Rooms.Furniture.Wall;

namespace Turbo.Rooms.Grains.Modules;

internal sealed partial class RoomActionModule
{
    public Task<bool> AddWallItemAsync(IRoomWallItem item, CancellationToken ct)
    {
        return _furniModule.AddWallItemAsync(item, ct);
    }

    public async Task<bool> MoveWallItemByIdAsync(
        ActionContext ctx,
        long itemId,
        string newLocation,
        CancellationToken ct
    )
    {
        if (!await CanManipulateFurniAsync(ctx))
            return false;

        return await _furniModule.MoveWallItemByIdAsync(ctx, itemId, newLocation, ct);
    }

    public Task<bool> RemoveWallItemByIdAsync(
        ActionContext ctx,
        long itemId,
        CancellationToken ct
    ) => _furniModule.RemoveWallItemByIdAsync(ctx, itemId, ct);

    public Task<bool> UseWallItemByIdAsync(
        ActionContext ctx,
        long itemId,
        int param = -1,
        CancellationToken ct = default
    ) => _furniModule.UseWallItemByIdAsync(ctx, itemId, param, ct);

    public Task<bool> ClickWallItemByIdAsync(
        ActionContext ctx,
        long itemId,
        int param = -1,
        CancellationToken ct = default
    ) => _furniModule.ClickWallItemByIdAsync(ctx, itemId, param, ct);
}
