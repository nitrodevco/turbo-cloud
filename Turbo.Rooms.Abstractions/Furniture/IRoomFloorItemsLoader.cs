using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Rooms;

namespace Turbo.Rooms.Abstractions.Furniture;

public interface IRoomFloorItemsLoader
{
    public Task<IReadOnlyList<IRoomFloorItem>> LoadByRoomIdAsync(
        long roomId,
        CancellationToken ct = default
    );
}
