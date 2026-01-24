using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;

namespace Turbo.Primitives.Rooms.Object.Logic.Furniture;

public interface IFurnitureFloorLogic<out TObject, out TLogic, out TContext>
    : IFurnitureLogic<TObject, TLogic, TContext>
    where TObject : IRoomFloorItem<TObject, TLogic, TContext>
    where TContext : IRoomFloorItemContext<TObject, TLogic, TContext>
    where TLogic : IFurnitureFloorLogic<TObject, TLogic, TContext>
{
    new TContext Context { get; }
}

public interface IFurnitureFloorLogic
    : IFurnitureLogic<IRoomFloorItem, IFurnitureFloorLogic, IRoomFloorItemContext>,
        IRollableObject
{
    public bool CanStack();
    public bool CanWalk();
    public bool CanSit();
    public bool CanLay();
    public Altitude GetPostureOffset();
    public Task OnInvokeAsync(IRoomAvatarContext ctx, CancellationToken ct);
    public Task OnWalkOnAsync(IRoomAvatarContext ctx, CancellationToken ct);
    public Task OnWalkOffAsync(IRoomAvatarContext ctx, CancellationToken ct);
}
