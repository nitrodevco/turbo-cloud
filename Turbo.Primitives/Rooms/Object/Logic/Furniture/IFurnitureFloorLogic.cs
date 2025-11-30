using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Rooms.Object.Furniture.StuffData;

namespace Turbo.Primitives.Rooms.Object.Logic.Furniture;

public interface IFurnitureFloorLogic : IFurnitureLogic
{
    public StuffDataType StuffDataKey { get; }
    public IStuffData StuffData { get; }

    public bool CanStack();
    public bool CanWalk();
    public bool CanSit();
    public bool CanLay();
    public Task OnStepAsync(ActionContext ctx, CancellationToken ct);
    public Task OnStopAsync(ActionContext ctx, CancellationToken ct);
}
