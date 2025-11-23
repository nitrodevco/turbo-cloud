using System.Threading;
using System.Threading.Tasks;
using Turbo.Contracts.Enums.Furniture;
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
    protected IStuffData _stuffData = default!;

    public virtual StuffDataType StuffDataKey => StuffDataType.LegacyKey;
    public virtual IStuffData StuffData => _stuffData;

    public virtual void Setup(string stuffDataRaw)
    {
        _stuffData = CreateStuffDataFromJson(stuffDataRaw);
    }

    public virtual double GetHeight() => _ctx.Definition.StackHeight;

    public virtual FurniUsagePolicy GetUsagePolicy() =>
        _ctx.Definition.TotalStates == 0 ? FurniUsagePolicy.Nobody : _ctx.Definition.UsagePolicy;

    public virtual bool CanToggle() => false;

    public virtual bool SetState(int state) => false;

    public virtual Task OnAttachAsync(CancellationToken ct) => Task.CompletedTask;

    public virtual Task OnUseAsync(int param, CancellationToken ct) => Task.CompletedTask;

    public virtual Task OnClickAsync(CancellationToken ct) => Task.CompletedTask;

    public virtual Task OnMoveAsync(CancellationToken ct) => Task.CompletedTask;

    public virtual Task OnPlaceAsync(CancellationToken ct) => Task.CompletedTask;

    public virtual Task OnPickupAsync(CancellationToken ct) => Task.CompletedTask;

    protected virtual IStuffData CreateStuffData() =>
        _stuffDataFactory.CreateStuffData((int)StuffDataKey);

    protected virtual IStuffData CreateStuffDataFromJson(string json) =>
        _stuffDataFactory.CreateStuffDataFromJson((int)StuffDataKey, json);
}
