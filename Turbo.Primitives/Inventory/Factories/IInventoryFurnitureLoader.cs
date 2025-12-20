using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Inventory.Furniture;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Snapshots.Furniture;

namespace Turbo.Primitives.Inventory.Factories;

public interface IInventoryFurnitureLoader
{
    public Task<IReadOnlyList<IFurnitureItem>> LoadByPlayerIdAsync(
        PlayerId playerId,
        CancellationToken ct
    );
    public IFurnitureItem CreateFromRoomItemSnapshot(RoomItemSnapshot snapshot);
}
