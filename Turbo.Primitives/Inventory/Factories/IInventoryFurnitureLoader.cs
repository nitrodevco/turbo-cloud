using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Inventory.Furniture;

namespace Turbo.Primitives.Inventory.Factories;

public interface IInventoryFurnitureLoader
{
    public Task<IReadOnlyList<IFurnitureItem>> LoadByPlayerIdAsync(
        long playerId,
        CancellationToken ct
    );
}
