using System.Threading;
using System.Threading.Tasks;
using Turbo.Contracts.Enums.Furniture;
using Turbo.Primitives.Actor;
using Turbo.Primitives.Rooms.Furniture;
using Turbo.Primitives.Rooms.Furniture.Logic;
using Turbo.Primitives.Rooms.Furniture.StuffData;

namespace Turbo.Rooms.Furniture.Logic;

public abstract class FurnitureLogicBase<TItem, TContext>(
    IStuffDataFactory stuffDataFactory,
    TContext ctx
) : IFurnitureLogic
    where TItem : IRoomItem
    where TContext : IRoomItemContext
{
    protected readonly IStuffDataFactory _stuffDataFactory = stuffDataFactory;
    protected readonly TContext _ctx = ctx;

    public virtual double GetHeight() => _ctx.Definition.StackHeight;

    public virtual FurniUsagePolicy GetUsagePolicy() =>
        _ctx.Definition.TotalStates == 0 ? FurniUsagePolicy.Nobody : _ctx.Definition.UsagePolicy;

    public virtual bool CanToggle() => false;

    public virtual Task<bool> SetStateAsync(int state) => Task.FromResult(false);

    public virtual Task OnAttachAsync(CancellationToken ct) => Task.CompletedTask;

    public virtual Task OnUseAsync(ActorContext ctx, int param, CancellationToken ct) =>
        Task.CompletedTask;

    public virtual Task OnClickAsync(ActorContext ctx, int param, CancellationToken ct) =>
        Task.CompletedTask;

    public virtual Task OnMoveAsync(ActorContext ctx, CancellationToken ct) => Task.CompletedTask;

    public virtual Task OnPlaceAsync(ActorContext ctx, CancellationToken ct) => Task.CompletedTask;

    public virtual Task OnPickupAsync(ActorContext ctx, CancellationToken ct) => Task.CompletedTask;
}
