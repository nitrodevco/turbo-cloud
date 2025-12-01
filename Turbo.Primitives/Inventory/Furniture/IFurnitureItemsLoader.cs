using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Turbo.Primitives.Inventory.Furniture;

public interface IFurnitureItemsLoader
{
    public Task<(
        IReadOnlyList<IFurnitureFloorItem>,
        IReadOnlyList<IFurnitureWallItem>
    )> LoadByPlayerIdAsync(long playerId, CancellationToken ct);
}
