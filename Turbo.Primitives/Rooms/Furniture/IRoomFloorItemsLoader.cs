using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Turbo.Primitives.Rooms.Furniture;

public interface IRoomFloorItemsLoader
{
    public Task<IReadOnlyList<IRoomFloorItem>> LoadByRoomIdAsync(
        long roomId,
        CancellationToken ct = default
    );
}
