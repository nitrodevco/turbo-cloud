using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Turbo.Primitives.Inventory.Furniture;

public interface IFurnitureItemsLoader
{
    public Task<IReadOnlyList<IFurnitureItem>> LoadByPlayerIdAsync(
        long playerId,
        CancellationToken ct
    );
}
