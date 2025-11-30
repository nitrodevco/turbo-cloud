using System.Threading;
using System.Threading.Tasks;
using Turbo.Contracts.Enums.Furniture;
using Turbo.Primitives.Action;
using Turbo.Primitives.Rooms.Object.Furniture;
using Turbo.Primitives.Rooms.Object.Furniture.StuffData;
using Turbo.Primitives.Rooms.Object.Logic.Furniture;

namespace Turbo.Rooms.Object.Logic.Furniture;

public abstract class FurnitureLogicBase<TItem, TContext>(
    IStuffDataFactory stuffDataFactory,
    TContext ctx
) : RoomObjectLogicBase<TContext>(ctx), IFurnitureLogic
    where TItem : IRoomItem
    where TContext : IRoomItemContext
{
    protected readonly IStuffDataFactory _stuffDataFactory = stuffDataFactory;

    public virtual double GetHeight() => _ctx.Definition.StackHeight;

    public virtual FurniUsagePolicy GetUsagePolicy() =>
        _ctx.Definition.TotalStates == 0 ? FurniUsagePolicy.Nobody : _ctx.Definition.UsagePolicy;

    public virtual bool CanToggle() => false;

    public virtual Task<int> GetStateAsync() => Task.FromResult(0);

    public virtual Task<bool> SetStateAsync(int state) => Task.FromResult(false);

    public virtual Task OnUseAsync(ActionContext ctx, int param, CancellationToken ct) =>
        Task.CompletedTask;

    public virtual Task OnClickAsync(ActionContext ctx, int param, CancellationToken ct) =>
        Task.CompletedTask;

    public virtual Task OnMoveAsync(ActionContext ctx, CancellationToken ct) => Task.CompletedTask;

    public virtual Task OnPlaceAsync(ActionContext ctx, CancellationToken ct) => Task.CompletedTask;

    public virtual Task OnPickupAsync(ActionContext ctx, CancellationToken ct) =>
        Task.CompletedTask;
}
