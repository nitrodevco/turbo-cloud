using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Rooms.Object.Avatars;

namespace Turbo.Primitives.Rooms.Object.Logic.Furniture;

public interface IFurnitureFloorLogic : IFurnitureLogic, IRollableObject
{
    public bool CanStack();
    public bool CanWalk();
    public bool CanSit();
    public bool CanLay();
    public double GetPostureOffset();
    public double GetStackHeight();
    public Task OnInvokeAsync(IRoomAvatarContext ctx, CancellationToken ct);
    public Task OnWalkAsync(IRoomAvatarContext ctx, CancellationToken ct);
}
