using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Furniture.StuffData;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Events.RoomItem;
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

    public IStuffData StuffData { get; private set; }

    protected virtual StuffPersistanceType _stuffPersistanceType => StuffPersistanceType.External;
    protected virtual StuffDataType _stuffDataType => StuffDataType.LegacyKey;

    public FurnitureLogicBase(IStuffDataFactory stuffDataFactory, TContext ctx)
        : base(ctx)
    {
        _stuffDataFactory = stuffDataFactory;

        StuffData = _stuffDataFactory.CreateStuffDataFromExtraData(
            _stuffDataType,
            ctx.Item.ExtraData
        );

        StuffData.SetAction(async () =>
        {
            _ = ctx.RefreshStuffDataAsync();

            if (_stuffPersistanceType == StuffPersistanceType.External)
            {
                ctx.Item.ExtraData.UpdateSection(
                    "stuff",
                    JsonSerializer.SerializeToNode(StuffData, StuffData.GetType())
                );

                if (_ctx is IRoomFloorItemContext floorCtx)
                    floorCtx.RefreshTile();
            }
        });
    }

    public virtual FurnitureUsageType GetUsagePolicy() =>
        _ctx.Definition.TotalStates == 0 ? FurnitureUsageType.Nobody : _ctx.Definition.UsagePolicy;

    public virtual bool CanToggle() => false;

    public virtual Task<int> GetStateAsync() => Task.FromResult(StuffData.GetState());

    public virtual Task SetStateAsync(int state) => StuffData.SetStateAsync(state.ToString());

    public virtual Task SetStateSilentlyAsync(int state) =>
        StuffData.SetStateSilentlyAsync(state.ToString());

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
        _ctx.PublishRoomEventAsync(
            new RoomItemClickedEvent
            {
                RoomId = _ctx.RoomId,
                CausedBy = ctx,
                ItemId = _ctx.ObjectId,
            },
            ct
        );

    public virtual Task OnMoveAsync(ActionContext ctx, int prevIdx, CancellationToken ct) =>
        _ctx.PublishRoomEventAsync(
            new RoomItemMovedEvent
            {
                RoomId = _ctx.RoomId,
                CausedBy = ctx,
                ItemId = _ctx.ObjectId,
                PrevIdx = prevIdx,
            },
            ct
        );

    protected virtual int GetNextToggleableState()
    {
        var totalStates = _ctx.Item.Definition.TotalStates;

        if (totalStates == 0 || StuffData is null)
            return 0;

        return (StuffData.GetState() + 1) % totalStates;
    }
}
