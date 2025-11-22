using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Rooms.Furniture.Floor;
using Turbo.Primitives.Rooms.Furniture.Wall;

namespace Turbo.Primitives.Rooms.Furniture;

public interface IRoomItemsLoader
{
    public Task<(IReadOnlyList<IRoomFloorItem>, IReadOnlyList<IRoomWallItem>)> LoadByRoomIdAsync(
        long roomId,
        CancellationToken ct = default
    );
}
