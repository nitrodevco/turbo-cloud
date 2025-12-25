using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Rooms.Events;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Providers;
using Turbo.Primitives.Rooms.Wired;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor;

[RoomObjectLogic("default_wired")]
public class FurnitureWiredLogic : FurnitureFloorLogic
{
    private readonly IWiredDefinitionProvider _wiredDefinitionProvider;
    private readonly IGrainFactory _grainFactory;

    private readonly IWiredDefinition _wiredDefinition;

    public IWiredDefinition WiredDefinition => _wiredDefinition;

    public FurnitureWiredLogic(
        IWiredDefinitionProvider wiredDefinitionProvider,
        IGrainFactory grainFactory,
        IStuffDataFactory stuffDataFactory,
        IRoomFloorItemContext ctx
    )
        : base(stuffDataFactory, ctx)
    {
        _wiredDefinitionProvider = wiredDefinitionProvider;
        _grainFactory = grainFactory;

        _wiredDefinition = _wiredDefinitionProvider.CreateWiredInstance(
            ctx.Definition.WiredType,
            ctx
        );
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
