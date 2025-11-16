using System.Threading;
using System.Threading.Tasks;

namespace Turbo.Primitives.Rooms.Furniture;

public interface IFurnitureFloorLogic : IFurnitureLogic
{
    public ValueTask OnStopAsync(FurnitureContext ctx, CancellationToken ct);
}
