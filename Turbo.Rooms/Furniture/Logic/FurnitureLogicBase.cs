using System.Threading;
using System.Threading.Tasks;
using Turbo.Contracts.Enums.Furniture;
using Turbo.Contracts.Enums.Rooms.Furniture.Data;
using Turbo.Primitives.Rooms.Furniture;
using Turbo.Primitives.Rooms.Furniture.Logic;
using Turbo.Primitives.Rooms.Furniture.StuffData;

namespace Turbo.Rooms.Furniture.Logic;

public abstract class FurnitureLogicBase<TContext>(IStuffDataFactory stuffDataFactory, TContext ctx)
    : IFurnitureLogic
    where TContext : IRoomItemContext
{
    protected readonly IStuffDataFactory _stuffDataFactory = stuffDataFactory;
    protected readonly TContext _ctx = ctx;

    public virtual StuffDataType StuffDataKey => StuffDataType.LegacyKey;

    public virtual bool CanToggle() => false;

    public virtual double GetHeight() => _ctx.Definition.StackHeight;

    public virtual FurniUsagePolicy GetUsagePolicy() =>
        _ctx.Definition.TotalStates == 0 ? FurniUsagePolicy.Nobody : _ctx.Definition.UsagePolicy;

    public virtual Task SetStateAsync(int state, CancellationToken ct) => Task.CompletedTask;

    public virtual void SetupStuffDataFromJson(string json)
    {
        var stuffData = _stuffDataFactory.CreateStuffDataFromJson((int)StuffDataKey, json);

        _ctx.SetStuffData(stuffData);
    }

    public virtual Task OnInteractAsync(int param, CancellationToken ct) => Task.CompletedTask;

    public virtual Task OnMoveAsync(CancellationToken ct) => Task.CompletedTask;

    public virtual Task OnPlaceAsync(CancellationToken ct) => Task.CompletedTask;

    public virtual Task OnPickupAsync(CancellationToken ct) => Task.CompletedTask;
}
