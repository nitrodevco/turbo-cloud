using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Furniture.StuffData;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;

namespace Turbo.Primitives.Rooms.Object.Logic.Furniture;

public interface IFurnitureFloorLogic : IFurnitureLogic
{
    public StuffDataType StuffDataKey { get; }
    public IStuffData StuffData { get; }
    public IRoomFloorItemContext Context { get; }

    public bool CanStack();
    public bool CanWalk();
    public bool CanSit();
    public bool CanLay();
    public Task OnStepAsync(IRoomAvatarContext ctx, CancellationToken ct);
    public Task OnStopAsync(IRoomAvatarContext ctx, CancellationToken ct);
}
