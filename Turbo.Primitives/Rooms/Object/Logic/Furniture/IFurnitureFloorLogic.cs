using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Rooms.Furniture.StuffData;

namespace Turbo.Primitives.Rooms.Object.Logic.Furniture;

public interface IFurnitureFloorLogic : IFurnitureLogic
{
    public StuffDataType StuffDataKey { get; }
    public IStuffData StuffData { get; }

    public bool CanStack();
    public bool CanWalk();
    public bool CanSit();
    public bool CanLay();
    public Task OnStopAsync(ActionContext ctx, CancellationToken ct);
}
