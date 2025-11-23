using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Actor;

namespace Turbo.Primitives.Rooms.Furniture.Logic;

public interface IFurnitureFloorLogic : IFurnitureLogic
{
    public bool CanStack();
    public bool CanWalk();
    public bool CanSit();
    public bool CanLay();
    public Task OnStopAsync(ActorContext ctx, CancellationToken ct);
}
