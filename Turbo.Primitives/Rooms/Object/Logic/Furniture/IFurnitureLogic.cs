using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture.StuffData;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object.Furniture;

namespace Turbo.Primitives.Rooms.Object.Logic.Furniture;

public interface IFurnitureLogic<out TObject, out TLogic, out TContext>
    : IRoomObjectLogic<TObject, TLogic, TContext>,
        IFurnitureLogic
    where TObject : IRoomItem<TObject, TLogic, TContext>
    where TContext : IRoomItemContext<TObject, TLogic, TContext>
    where TLogic : IFurnitureLogic<TObject, TLogic, TContext>
{
    new TContext Context { get; }
}

public interface IFurnitureLogic : IRoomObjectLogic, IRollableObject
{
    new IRoomItemContext Context { get; }
    public IStuffData StuffData { get; }
    public FurnitureUsageType GetUsagePolicy();
    public bool CanToggle();
    public Altitude GetStackHeight();
    public Task<int> GetStateAsync();
    public Task SetStateAsync(int state);
    public Task OnMoveAsync(ActionContext ctx, int prevIdx, CancellationToken ct);
    public Task OnPlaceAsync(ActionContext ctx, CancellationToken ct);
    public Task OnPickupAsync(ActionContext ctx, CancellationToken ct);
    public Task OnUseAsync(ActionContext ctx, int param, CancellationToken ct);
    public Task OnClickAsync(ActionContext ctx, int param, CancellationToken ct);
}
