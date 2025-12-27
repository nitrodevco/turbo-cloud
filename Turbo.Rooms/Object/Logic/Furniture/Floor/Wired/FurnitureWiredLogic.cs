using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Furniture.Snapshots.WiredData;
using Turbo.Primitives.Furniture.WiredData;
using Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic.Furniture;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired;

public abstract class FurnitureWiredLogic : FurnitureFloorLogic, IFurnitureWiredLogic
{
    protected readonly IWiredDataFactory _wiredDataFactory;
    protected readonly IGrainFactory _grainFactory;

    public abstract WiredType WiredType { get; }
    public abstract int WiredCode { get; }

    public IWiredData WiredData { get; private set; }

    protected int _furniLimit = 20;
    protected Dictionary<string, string> _params = [];

    protected Dictionary<int, WiredSourceType> _furniSources = [];

    private WiredDataSnapshot? _snapshot;

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

        WiredData.WiredCode = WiredCode;

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
        _ = _grainFactory
            .GetPlayerPresenceGrain(ctx.PlayerId)
            .SendComposerAsync(new OpenEventMessageComposer { ItemId = _ctx.ObjectId })
            .ConfigureAwait(false);
    }

    public async Task ConfigureWiredAsync(CancellationToken ct)
    {
        if (_params.Count == 0)
        {
            FillDefaultSources();

            return;
        }

        if (_params.ContainsKey(WiredParamType.WIRED_TIMER))
        {
            //
        }

        if (_params.ContainsKey(WiredParamType.WIRED_SELECTION_TYPE))
        {
            //
        }

        if (_params.ContainsKey(WiredParamType.WIRED_MATCH_TYPE))
        {
            //
        }

        if (_params.ContainsKey(WiredParamType.WIRED_TEXT))
        {
            //
        }

        if (_params.ContainsKey(WiredParamType.WIRED_PARAMETERS))
        {
            //
        }

        if (_params.ContainsKey(WiredParamType.WIRED_FURNI_SOURCES))
        {
            //
        }

        if (_params.ContainsKey(WiredParamType.WIRED_USER_SOURCES))
        {
            //
        }

        if (_params.ContainsKey(WiredParamType.WIRED_SELECTED_ITEMS))
        {
            //
        }

        if (_params.ContainsKey(WiredParamType.WIRED_SNAPSHOTS))
        {
            //
        }

        FillDefaultSources();
    }

    public virtual List<WiredSourceType[]> GetFurniSources() => [];

    public virtual List<WiredSourceType[]> GetPlayerSources() => [];

    protected virtual void FillDefaultSources()
    {
        var index = -1;

        foreach (var source in GetFurniSources())
        {
            index++;

            if (WiredData.FurniSources.ContainsKey(index))
                continue;

            WiredData.FurniSources.Add(index, source[0]);
        }

        index = -1;

        foreach (var source in GetPlayerSources())
        {
            index++;

            if (WiredData.PlayerSources.ContainsKey(index))
                continue;

            WiredData.PlayerSources.Add(index, source[0]);
        }
    }

    public WiredDataSnapshot GetSnapshot()
    {
        if (_snapshot is null)
        {
            _snapshot = BuildSnapshot();
        }

        return _snapshot;
    }

    protected abstract WiredDataSnapshot BuildSnapshot();
}
