using System.Threading;
using System.Threading.Tasks;

namespace Turbo.Primitives.Rooms.Furniture.Logic;

public interface IFurnitureFloorLogic : IFurnitureLogic
{
    public Task OnStopAsync(CancellationToken ct);
}
