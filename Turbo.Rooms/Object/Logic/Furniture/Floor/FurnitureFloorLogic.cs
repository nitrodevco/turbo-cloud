using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture.StuffData;
using Turbo.Primitives.Rooms.Events;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Object.Logic.Furniture;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor;

[RoomObjectLogic("default_floor")]
public class FurnitureFloorLogic
    : FurnitureLogicBase<IRoomFloorItem, IRoomFloorItemContext>,
        IFurnitureFloorLogic
{
    public virtual StuffDataType StuffDataKey => StuffDataType.LegacyKey;

    protected IStuffData _stuffData;

    public IStuffData StuffData => _stuffData;
    public IRoomFloorItemContext Context => _ctx;

    public FurnitureFloorLogic(IStuffDataFactory stuffDataFactory, IRoomFloorItemContext ctx)
        : base(stuffDataFactory, ctx)
    {
        _stuffData = CreateStuffData(_ctx.Item.PendingStuffDataRaw);

        void func() => _ctx.Item.MarkDirty();

        _stuffData.SetAction(func);
    }

    public override Task<int> GetStateAsync() => Task.FromResult(_stuffData.GetState());

    public override async Task<bool> SetStateAsync(int state)
    {
        _stuffData.SetState(state.ToString());

        await _ctx.RefreshStuffDataAsync(CancellationToken.None);

        _ctx.RefreshTile();

        return true;
    }

    public virtual bool CanStack() => _ctx.Definition.CanStack;

    public virtual bool CanWalk() => _ctx.Definition.CanWalk;

    public virtual bool CanSit() => _ctx.Definition.CanSit;

    public virtual bool CanLay() => _ctx.Definition.CanLay;

    public virtual Task OnStepAsync(IRoomAvatarContext ctx, CancellationToken ct) =>
        Task.CompletedTask;

    public virtual Task OnInvokeAsync(IRoomAvatarContext ctx, CancellationToken ct) =>
        Task.CompletedTask;

    public override async Task OnUseAsync(ActionContext ctx, int param, CancellationToken ct)
    {
        param = GetNextToggleableState();

        await SetStateAsync(param);
    }

    public override async Task OnMoveAsync(ActionContext ctx, CancellationToken ct)
    {
        _ = _ctx.PublishRoomEventAsync(
            new FloorItemMovedEvent
            {
                RoomId = _ctx.RoomId,
                CausedBy = ctx,
                ItemId = _ctx.Item.ObjectId,
            },
            ct
        );

        await base.OnMoveAsync(ctx, ct);
    }

    protected virtual int GetNextToggleableState()
    {
        var totalStates = _ctx.Item.Definition.TotalStates;

        if (totalStates == 0 || StuffData is null)
            return 0;

        return (StuffData.GetState() + 1) % totalStates;
    }

    protected virtual IStuffData CreateStuffData(string json = "") =>
        _stuffDataFactory.CreateStuffDataFromJson((int)StuffDataKey, json);
}
