using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Furniture.Interactions;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Furniture.StuffData;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Events.RoomItem;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Furniture;
using Turbo.Primitives.Rooms.Object.Logic.Furniture;

namespace Turbo.Rooms.Object.Logic.Furniture;

public abstract class FurnitureLogic<TObject, TSelf, TContext>
    : RoomObjectLogic<TObject, TSelf, TContext>,
        IFurnitureLogic<TObject, TSelf, TContext>
    where TObject : IRoomItem<TObject, TSelf, TContext>
    where TContext : IRoomItemContext<TObject, TSelf, TContext>
    where TSelf : IFurnitureLogic<TObject, TSelf, TContext>
{
    protected readonly IStuffDataFactory _stuffDataFactory;

    protected virtual StuffPersistanceType _stuffPersistanceType => StuffPersistanceType.Persistent;
    protected virtual StuffDataType _stuffDataType => StuffDataType.LegacyKey;

    public IStuffData StuffData { get; private set; }

    IRoomItemContext IFurnitureLogic.Context => Context;

    public FurnitureLogic(IStuffDataFactory stuffDataFactory, TContext ctx)
        : base(ctx)
    {
        _stuffDataFactory = stuffDataFactory;

        StuffData = _stuffDataFactory.CreateStuffDataFromExtraData(
            _stuffDataType,
            ctx.RoomObject.ExtraData
        );
    }

    public virtual FurnitureUsageType GetUsagePolicy() =>
        _ctx.Definition.TotalStates == 0 ? FurnitureUsageType.Nobody : _ctx.Definition.UsagePolicy;

    /// <summary>
    /// Who may use the item. By default that is its usage policy, which is also what the client
    /// is told. A logic whose use belongs to one person (a seed, a pet package) overrides this
    /// and keeps its policy at Nobody, so nobody else is offered a use at all. Never give such an
    /// item a wider policy just so its use gets through: that is what the client draws.
    /// </summary>
    public virtual Task<bool> CanUseAsync(ActionContext ctx) =>
        _roomGrain.SecurityModule.CanUseFurniAsync(ctx, GetUsagePolicy());

    public virtual bool CanToggle() => false;

    public virtual bool CanRoll() => false;

    public virtual Altitude GetStackHeight() => 0;

    public virtual int GetState() => StuffData.GetState();

    public virtual string GetLegacyString() => StuffData.GetLegacyString();

    public virtual int GetNextToggleableState()
    {
        var totalStates = _ctx.RoomObject.Definition.TotalStates;

        if (totalStates == 0 || StuffData is null)
            return 0;

        return (StuffData.GetState() + 1) % totalStates;
    }

    public virtual int GetPrevToggleableState()
    {
        var totalStates = _ctx.RoomObject.Definition.TotalStates;

        if (totalStates == 0 || StuffData is null)
            return 0;

        return (StuffData.GetState() - 1 + totalStates) % totalStates;
    }

    public virtual async Task SetStateAsync(int state, bool refresh = true)
    {
        StuffData.SetState(state.ToString());

        PersistStuffData(refresh);

        await OnStateChangedAsync(CancellationToken.None);
    }

    public virtual async Task SetLegacyDataAsync(string data, bool refresh = true)
    {
        StuffData.SetState(data);

        PersistStuffData(refresh);

        await OnStateChangedAsync(CancellationToken.None);
    }

    public virtual async Task<bool> SetMapDataAsync(
        IReadOnlyDictionary<string, string> entries,
        bool refresh = true
    )
    {
        if (StuffData is not IMapStuffData mapData)
            return false;

        foreach (var (key, value) in entries)
            mapData.Data[key] = value;

        mapData.MarkDirty();

        PersistStuffData(refresh);

        await OnStateChangedAsync(CancellationToken.None);

        return true;
    }

    public virtual Task<bool> OnInteractAsync(
        ActionContext ctx,
        FurnitureInteraction interaction,
        CancellationToken ct
    ) => Task.FromResult(false);

    /// <summary>Whether the acting player may edit furniture here (room rights).</summary>
    protected Task<bool> HasRightsAsync(ActionContext ctx) =>
        _roomGrain.SecurityModule.CanManipulateFurniAsync(ctx);

    /// <summary>
    /// Whether the acting player owns this item itself. The rule for turning the item into
    /// something else that is theirs to keep (credits, a present's contents, a pet): the room's
    /// owner may not do that with a guest's furni.
    /// </summary>
    protected bool IsItemOwner(ActionContext ctx) => _ctx.RoomObject.OwnerId == ctx.PlayerId;

    /// <summary>
    /// Whether the acting player owns this item or the room. The rule for changing how an item
    /// stands in the room (dressing a mannequin, engraving a trophy), which the room's owner
    /// may do too. Two meanings of "owner", named apart, so a new logic picks one on purpose.
    /// </summary>
    protected async Task<bool> IsItemOrRoomOwnerAsync(ActionContext ctx) =>
        IsItemOwner(ctx) || await _roomGrain.SecurityModule.GetIsRoomOwnerAsync(ctx);

    /// <summary>Logs a refused interaction with the ids that identify it, and yields false.</summary>
    protected bool Reject(ActionContext ctx, FurnitureInteraction interaction, string reason)
    {
        _roomGrain._logger.LogWarning(
            "Rejected {Interaction} on item {ItemId} in room {RoomId} by player {PlayerId}: {Reason}",
            interaction.GetType().Name,
            _ctx.ObjectId,
            _ctx.RoomId,
            ctx.PlayerId,
            reason
        );

        return false;
    }

    /// <summary>Replaces int-array stuff data wholesale. False when this item's data is not numeric.</summary>
    protected async Task<bool> SetNumberDataAsync(IReadOnlyList<int> values, bool refresh = true)
    {
        if (StuffData is not INumberStuffData numbers)
            return false;

        numbers.Data.Clear();
        numbers.Data.AddRange(values);
        numbers.MarkDirty();

        PersistStuffData(refresh);

        await OnStateChangedAsync(CancellationToken.None);

        return true;
    }

    /// <summary>Replaces string-array stuff data wholesale. False when this item's data is not a string array.</summary>
    protected async Task<bool> SetStringDataAsync(IReadOnlyList<string> values, bool refresh = true)
    {
        if (StuffData is not IStringStuffData strings)
            return false;

        strings.Data.Clear();
        strings.Data.AddRange(values);
        strings.MarkDirty();

        PersistStuffData(refresh);

        await OnStateChangedAsync(CancellationToken.None);

        return true;
    }

    /// <summary>Writes the current stuff data into the item's extra data and, optionally, to the room.</summary>
    protected void PersistStuffData(bool refresh)
    {
        if (_stuffPersistanceType == StuffPersistanceType.Persistent)
            _ctx.RoomObject.ExtraData.UpdateSection(
                ExtraDataSectionType.STUFF,
                JsonSerializer.SerializeToNode(StuffData, StuffData.GetType())
            );

        if (refresh)
            _ctx.RefreshStuffDataAsync()
                .LogAndForget(
                    _roomGrain._logger,
                    $"refresh furni data in room {_roomGrain.RoomId}"
                );
    }

    public override Task OnAttachAsync(CancellationToken ct) =>
        _ctx.PublishRoomEventAsync(
            new RoomItemAttatchedEvent
            {
                RoomId = _ctx.RoomId,
                CausedBy = ActionContext.System,
                ObjectId = _ctx.ObjectId,
            },
            ct
        );

    public override Task OnDetachAsync(CancellationToken ct) =>
        _ctx.PublishRoomEventAsync(
            new RoomItemDetachedEvent
            {
                RoomId = _ctx.RoomId,
                CausedBy = ActionContext.System,
                ObjectId = _ctx.ObjectId,
            },
            ct
        );

    public virtual Task OnStateChangedAsync(CancellationToken ct) =>
        _ctx.PublishRoomEventAsync(
            new RoomItemStateChangedEvent
            {
                RoomId = _ctx.RoomId,
                CausedBy = ActionContext.System,
                ObjectId = _ctx.ObjectId,
            },
            ct
        );

    public virtual Task OnMoveAsync(ActionContext ctx, int prevIdx, CancellationToken ct) =>
        _ctx.PublishRoomEventAsync(
            new RoomItemMovedEvent
            {
                RoomId = _ctx.RoomId,
                CausedBy = ctx,
                ObjectId = _ctx.ObjectId,
                PrevIdx = prevIdx,
            },
            ct
        );

    public virtual Task OnPlaceAsync(ActionContext ctx, CancellationToken ct) =>
        _ctx.PublishRoomEventAsync(
            new RoomItemPlacedEvent
            {
                RoomId = _ctx.RoomId,
                CausedBy = ctx,
                ObjectId = _ctx.ObjectId,
            },
            ct
        );

    public virtual Task OnPickupAsync(ActionContext ctx, CancellationToken ct) =>
        _ctx.PublishRoomEventAsync(
            new RoomItemPickupEvent
            {
                RoomId = _ctx.RoomId,
                CausedBy = ctx,
                ObjectId = _ctx.ObjectId,
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
                ObjectId = _ctx.ObjectId,
            },
            ct
        );
}
