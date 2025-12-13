using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture.StuffData;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Events;
using Turbo.Primitives.Rooms.Object.Furniture;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic.Furniture;

namespace Turbo.Rooms.Object.Logic.Furniture;

public abstract class FurnitureLogicBase<TItem, TContext>
    : RoomObjectLogicBase<TContext>,
        IFurnitureLogic
    where TItem : IRoomItem
    where TContext : IRoomItemContext<TItem>
{
    protected readonly IStuffDataFactory _stuffDataFactory;

    public virtual StuffDataType StuffDataKey => StuffDataType.LegacyKey;

    public IStuffData StuffData { get; private set; }

    public FurnitureLogicBase(IStuffDataFactory stuffDataFactory, TContext ctx)
        : base(ctx)
    {
        _stuffDataFactory = stuffDataFactory;

        StuffData = CreateStuffData(_ctx.Item.PendingStuffDataRaw);

        StuffData.SetAction(async () =>
        {
            _ctx.Item.MarkDirty();

            await _ctx.RefreshStuffDataAsync(CancellationToken.None);

            if (_ctx is IRoomFloorItemContext floorCtx)
                floorCtx.RefreshTile();
        });
    }

    public virtual double GetHeight() => _ctx.Definition.StackHeight;

    public virtual FurnitureUsageType GetUsagePolicy() =>
        _ctx.Definition.TotalStates == 0 ? FurnitureUsageType.Nobody : _ctx.Definition.UsagePolicy;

    public virtual bool CanToggle() => false;

    public virtual Task<int> GetStateAsync() => Task.FromResult(StuffData.GetState());

    public virtual Task SetStateAsync(int state) => StuffData.SetStateAsync(state.ToString());

    public override Task OnAttachAsync(CancellationToken ct) =>
        _ctx.PublishRoomEventAsync(
            new RoomItemAttatchedEvent
            {
                RoomId = _ctx.RoomId,
                CausedBy = null,
                ItemId = _ctx.ObjectId,
            },
            ct
        );

    public override Task OnDetachAsync(CancellationToken ct) =>
        _ctx.PublishRoomEventAsync(
            new RoomItemDetachedEvent
            {
                RoomId = _ctx.RoomId,
                CausedBy = null,
                ItemId = _ctx.ObjectId,
            },
            ct
        );

    public virtual async Task OnUseAsync(ActionContext ctx, int param, CancellationToken ct)
    {
        param = GetNextToggleableState();

        await SetStateAsync(param);
    }

    public virtual Task OnClickAsync(ActionContext ctx, int param, CancellationToken ct) =>
        Task.CompletedTask;

    public virtual Task OnMoveAsync(ActionContext ctx, CancellationToken ct) =>
        _ctx.PublishRoomEventAsync(
            new RoomItemMovedEvent
            {
                RoomId = _ctx.RoomId,
                CausedBy = ctx,
                ItemId = _ctx.ObjectId,
            },
            ct
        );

    protected virtual IStuffData CreateStuffData(string json = "") =>
        _stuffDataFactory.CreateStuffDataFromJson((int)StuffDataKey, json);

    protected virtual int GetNextToggleableState()
    {
        var totalStates = _ctx.Item.Definition.TotalStates;

        if (totalStates == 0 || StuffData is null)
            return 0;

        return (StuffData.GetState() + 1) % totalStates;
    }
}
