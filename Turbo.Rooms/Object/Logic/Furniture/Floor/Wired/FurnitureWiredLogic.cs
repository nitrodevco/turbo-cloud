using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Furniture.WiredData;
using Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Rooms.Events;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired;

public abstract class FurnitureWiredLogic : FurnitureFloorLogic
{
    public virtual WiredType WiredType => WiredType.None;

    protected readonly IWiredDataFactory _wiredDataFactory;
    protected readonly IGrainFactory _grainFactory;

    public IWiredData WiredData { get; private set; }

    public FurnitureWiredLogic(
        IWiredDataFactory wiredDataFactory,
        IGrainFactory grainFactory,
        IStuffDataFactory stuffDataFactory,
        IRoomFloorItemContext ctx
    )
        : base(stuffDataFactory, ctx)
    {
        _wiredDataFactory = wiredDataFactory;
        _grainFactory = grainFactory;

        WiredData = _wiredDataFactory.CreateWiredDataFromExtraData(WiredType, ctx.Item.ExtraData);

        WiredData.SetAction(async () =>
        {
            _ctx.Item.ExtraData.UpdateSection(
                "wired",
                JsonSerializer.SerializeToNode(WiredData, WiredData.GetType())
            );
        });
    }

    public override async Task OnAttachAsync(CancellationToken ct)
    {
        await base.OnAttachAsync(ct);

        _ = _ctx.PublishRoomEventAsync(
            new RoomWiredStackChangedEvent
            {
                RoomId = _ctx.RoomId,
                CausedBy = null,
                StackIds = [_ctx.GetTileIdx()],
            },
            ct
        );
    }

    public override async Task OnDetachAsync(CancellationToken ct)
    {
        await base.OnDetachAsync(ct);

        _ = _ctx.PublishRoomEventAsync(
            new RoomWiredStackChangedEvent
            {
                RoomId = _ctx.RoomId,
                CausedBy = null,
                StackIds = [_ctx.GetTileIdx()],
            },
            ct
        );
    }

    public override async Task OnMoveAsync(ActionContext ctx, int prevIdx, CancellationToken ct)
    {
        await base.OnMoveAsync(ctx, prevIdx, ct);

        _ = _ctx.PublishRoomEventAsync(
            new RoomWiredStackChangedEvent
            {
                RoomId = _ctx.RoomId,
                CausedBy = ctx,
                StackIds = [_ctx.GetTileIdx(), prevIdx],
            },
            ct
        );
    }

    public override async Task OnUseAsync(ActionContext ctx, int param, CancellationToken ct)
    {
        OpenWiredInterface(ctx);
    }

    private void OpenWiredInterface(ActionContext ctx)
    {
        if (ctx.Origin != ActionOrigin.Player)
            return;

        _ = _grainFactory
            .GetPlayerPresenceGrain(ctx.PlayerId)
            .SendComposerAsync(new OpenEventMessageComposer { ItemId = _ctx.ObjectId });
    }
}
