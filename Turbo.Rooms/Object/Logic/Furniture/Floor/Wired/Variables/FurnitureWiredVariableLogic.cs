using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Logging;
using Turbo.Primitives;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Snapshots.Wired.Variables;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Wired.Storage;
using Turbo.Rooms.Wired.Variables;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Variables;

public abstract class FurnitureWiredVariableLogic
    : FurnitureWiredLogic,
        IWiredVariable,
        IWiredVariableStore
{
    public override WiredType WiredType => WiredType.Variable;

    protected readonly WiredVariableId _variableId;

    protected virtual WiredVariableType VariableType => WiredVariableType.Created;
    protected abstract WiredVariableTargetType TargetType { get; }
    protected abstract WiredAvailabilityType AvailabilityType { get; }

    /// <summary>
    /// Index of the availability choice in the box's int params, or null when the box has a
    /// fixed availability the player cannot change.
    /// </summary>
    protected virtual int? AvailabilityParamIndex => null;

    /// <summary>
    /// Turning a box permanent consumes one of the room's permanent variable slots for its
    /// target type, so the switch is refused once the configured cap is reached.
    /// </summary>
    public override async Task<bool> ApplyWiredUpdateAsync(
        ActionContext ctx,
        UpdateWiredMessage update,
        CancellationToken ct
    )
    {
        if (
            AvailabilityParamIndex is int index
            && index < update.IntParams.Count
            && (WiredAvailabilityType)update.IntParams[index] == WiredAvailabilityType.Persistent
            && GetVarSnapshot().AvailabilityType != WiredAvailabilityType.Persistent
            && await WiredSystem.IsPermanentVariableCapReachedAsync(TargetType, ct)
        )
            throw new TurboException(TurboErrorCodeEnum.WiredPermanentVariableLimitReached);

        return await base.ApplyWiredUpdateAsync(ctx, update, ct);
    }

    protected virtual WiredVariableFlags Flags => WiredVariableFlags.None;
    protected KeyValueStore? _storage = null;
    protected WiredVariableSnapshot? _varSnapshot;

    public FurnitureWiredVariableLogic(
        IGrainFactory grainFactory,
        IStuffDataFactory stuffDataFactory,
        IRoomFloorItemContext ctx
    )
        : base(grainFactory, stuffDataFactory, ctx)
    {
        _variableId = WiredVariableIdBuilder.CreateFromBoxId(ctx.ObjectId.Value);
    }

    public virtual bool CanBind(in WiredVariableKey key)
    {
        var snapshot = GetVarSnapshot();

        return key.VariableId == snapshot.VariableId && key.TargetType == snapshot.TargetType;
    }

    public virtual bool TryGetValue(in WiredVariableKey key, out WiredVariableValue value)
    {
        value = WiredVariableValue.Default;

        if (!CanBind(key) || !TryGetStore(key, out var store, out var storedKey) || store is null)
            return false;

        // A global always holds its value: 0 until something changes it.
        return store.TryGetValue(storedKey, out value) || HoldsByDefault(key);
    }

    /// <summary>
    /// A global variable has no holders to give it to: the room holds it from the moment the box
    /// is placed (<see cref="WiredVariableFlags.AlwaysAvailable"/>), at 0, and a change to it
    /// always has something to change.
    /// </summary>
    private bool HoldsByDefault(in WiredVariableKey key) =>
        key.TargetType == WiredVariableTargetType.Global
        && GetVarSnapshot().Flags.Has(WiredVariableFlags.AlwaysAvailable);

    public virtual Task<bool> GiveValueAsync(
        WiredVariableKey key,
        WiredVariableValue value,
        bool replace = false
    )
    {
        var snapshot = GetVarSnapshot();

        if (
            !snapshot.Flags.Has(WiredVariableFlags.CanCreateAndDelete)
            || !CanBind(key)
            || !TryGetStore(key, out var store, out var storedKey)
            || store is null
            || (store.ContainsKey(storedKey) && !replace)
        )
            return Task.FromResult(false);

        return GiveAndNotifyAsync(store, key, storedKey, value, replace);
    }

    public virtual Task<bool> SetValueAsync(
        IWiredExecutionContext ctx,
        WiredVariableKey key,
        WiredVariableValue value
    )
    {
        if (!TryGetStore(key, out var store, out var storedKey) || store is null)
            return Task.FromResult(false);

        if (!store.ContainsKey(storedKey))
        {
            if (!HoldsByDefault(key))
                return Task.FromResult(false);

            return SetDefaultHeldAsync(store, ctx, key, storedKey, value);
        }

        return SetAndNotifyAsync(store, ctx, key, storedKey, value);
    }

    /// <summary>The first change to a global: it is stored at its 0, then changed like any value.</summary>
    private async Task<bool> SetDefaultHeldAsync(
        KeyValueStore store,
        IWiredExecutionContext ctx,
        WiredVariableKey key,
        WiredVariableKey storedKey,
        WiredVariableValue value
    )
    {
        if (!await store.GiveValueAsync(storedKey, WiredVariableValue.Default, false))
            return false;

        return await SetAndNotifyAsync(store, ctx, key, storedKey, value);
    }

    public bool TryGetTimestamps(
        in WiredVariableKey key,
        out long createdAtMs,
        out long updatedAtMs
    )
    {
        createdAtMs = 0;
        updatedAtMs = 0;

        return TryGetStore(key, out var store, out var storedKey)
            && store is not null
            && store.TryGetTimestamps(storedKey, out createdAtMs, out updatedAtMs);
    }

    public virtual bool RemoveValue(WiredVariableKey key)
    {
        if (!TryGetStore(key, out var store, out var storedKey) || store is null)
            return false;

        if (!store.TryGetValue(storedKey, out var previous) || !store.RemoveValue(storedKey))
            return false;

        PublishChange(key, WiredVariableChangeType.Removed, previous, previous);

        return true;
    }

    /// <summary>
    /// Takes the variable from every holder the box itself keeps, present in the room or not.
    /// A holder in the room is told like any other removal; an absent player is not, as the
    /// change event names avatars by room index and they have none. A variable that lives in
    /// the shared room-active stores keeps no list of its own, so there is nothing to walk: zero.
    /// </summary>
    public int RemoveAllValues()
    {
        if (_storage is null)
            return 0;

        var removed = 0;

        // Copied first: each removal changes the store being walked.
        string[] storageKeys = [.. _storage.Store.Keys];

        foreach (var storageKey in storageKeys)
        {
            if (
                !WiredVariableKey.TryFromStorageKey(storageKey, out var storedKey)
                || !CanBind(storedKey)
                || !_storage.TryGetValue(storedKey, out var previous)
                || !_storage.RemoveValue(storedKey)
            )
                continue;

            removed++;

            if (TryGetLiveKey(storedKey, out var key))
                PublishChange(key, WiredVariableChangeType.Removed, previous, previous);
        }

        return removed;
    }

    private async Task<bool> GiveAndNotifyAsync(
        KeyValueStore store,
        WiredVariableKey key,
        WiredVariableKey storedKey,
        WiredVariableValue value,
        bool replace
    )
    {
        var existed = store.TryGetValue(storedKey, out var previous);

        if (!await store.GiveValueAsync(storedKey, value, replace))
            return false;

        PublishChange(
            key,
            existed ? WiredVariableChangeType.Updated : WiredVariableChangeType.Created,
            value,
            existed ? previous : value
        );

        return true;
    }

    private async Task<bool> SetAndNotifyAsync(
        KeyValueStore store,
        IWiredExecutionContext ctx,
        WiredVariableKey key,
        WiredVariableKey storedKey,
        WiredVariableValue value
    )
    {
        store.TryGetValue(storedKey, out var previous);

        if (!await store.SetValueAsync(ctx, storedKey, value))
            return false;

        PublishChange(key, WiredVariableChangeType.Updated, value, previous);

        return true;
    }

    /// <summary>Feeds the "variable changed" trigger. Fire-and-forget: the event is queued.</summary>
    private void PublishChange(
        WiredVariableKey key,
        WiredVariableChangeType changeType,
        WiredVariableValue value,
        WiredVariableValue previous
    ) =>
        _ctx.PublishRoomEventAsync(
                new WiredVariableChangedEvent
                {
                    RoomId = _ctx.RoomId,
                    CausedBy = ActionContext.CreateForWired(_ctx.RoomId),
                    VariableId = key.VariableId,
                    TargetType = key.TargetType,
                    TargetId = key.TargetId,
                    ChangeType = changeType,
                    Value = value,
                    PreviousValue = previous,
                },
                System.Threading.CancellationToken.None
            )
            .LogAndForget(
                _roomGrain._logger,
                "publish an event in room {RoomId}",
                _roomGrain.RoomId
            );

    /// <summary>The labels a text connector addon on the same tile gives the values.</summary>
    public virtual Dictionary<WiredVariableValue, string> GetTextConnectors()
    {
        var connectors = new Dictionary<WiredVariableValue, string>();

        foreach (var item in FurniModule.GetFloorItemsOnTile(_ctx.GetTileIdx()))
        {
            if (item.Logic is not Addons.WiredAddonVariableTextConnector connector)
                continue;

            foreach (var (value, label) in connector.GetConnectors())
                connectors[value] = label;
        }

        return connectors;
    }

    protected override async Task FillInternalDataAsync(CancellationToken ct)
    {
        _varSnapshot = null;

        await base.FillInternalDataAsync(ct);

        var snapshot = GetVarSnapshot();

        if (snapshot.AvailabilityType == WiredAvailabilityType.Persistent)
        {
            if (_storage == null)
            {
                if (
                    _ctx.RoomObject.ExtraData.TryGetSection(
                        ExtraDataSectionType.STORAGE,
                        out var storageElement
                    )
                )
                {
                    _storage = storageElement.Deserialize<KeyValueStore>();
                }
                else
                {
                    _storage = new();
                }

                _storage?.SetAction(() =>
                {
                    _ctx.RoomObject.ExtraData.UpdateSection(
                        ExtraDataSectionType.STORAGE,
                        JsonSerializer.SerializeToNode(_storage, _storage.GetType())
                    );
                    return Task.CompletedTask;
                });
            }
        }
    }

    /// <summary>
    /// Where the value for a key lives, and the key it is kept under there. A permanent user
    /// variable keeps a player's value under their player id: the room index every user
    /// variable is otherwise keyed by is handed out afresh each time they enter, so a value kept
    /// under it was lost when they left and found by whoever got that index next. A pet or bot
    /// has no id of its own that cannot collide with a player's, so its values stay in the
    /// room-active store and last as long as it is in the room.
    /// </summary>
    private bool TryGetStore(
        WiredVariableKey key,
        out KeyValueStore? store,
        out WiredVariableKey storedKey
    )
    {
        storedKey = key;

        if (_storage is not null && key.TargetType != WiredVariableTargetType.User)
        {
            store = _storage;

            return true;
        }

        if (_storage is not null)
        {
            store = null;

            if (!AvatarModule.TryGetAvatar(RoomObjectId.Parse(key.TargetId), out var avatar))
                return false;

            if (avatar is IRoomPlayer player)
            {
                storedKey = key with { TargetId = player.PlayerId.Value };
                store = _storage;

                return true;
            }
        }

        return WiredSystem.TryGetStoreForKey(key, out store);
    }

    /// <summary>
    /// The key a stored value is known by in the room right now: a player's id turned back into
    /// their room index, or false when they are not here.
    /// </summary>
    private bool TryGetLiveKey(WiredVariableKey storedKey, out WiredVariableKey key)
    {
        key = storedKey;

        if (storedKey.TargetType != WiredVariableTargetType.User)
            return true;

        if (!AvatarModule.TryGetPlayer(new PlayerId(storedKey.TargetId), out var player))
            return false;

        key = storedKey with { TargetId = player.ObjectId.Value };

        return true;
    }

    public WiredVariableSnapshot GetVarSnapshot() => _varSnapshot ??= BuildVarSnapshot();

    protected virtual WiredVariableSnapshot BuildVarSnapshot()
    {
        var textConnectors = GetTextConnectors();
        var flags = textConnectors.Count > 0 ? Flags | WiredVariableFlags.HasTextConnector : Flags;
        var variableHash = WiredVariableHashBuilder.HashValues(
            _wiredData.StringParam,
            AvailabilityType,
            TargetType,
            flags,
            textConnectors
        );

        return new()
        {
            VariableId = _variableId,
            VariableName = _wiredData.StringParam,
            VariableType = WiredVariableType.Created,
            VariableHash = variableHash,
            AvailabilityType = AvailabilityType,
            TargetType = TargetType,
            Flags = flags,
            TextConnectors = textConnectors,
        };
    }

    public override async Task OnPickupAsync(ActionContext ctx, CancellationToken ct)
    {
        _ctx.RoomObject.ExtraData.DeleteSection(ExtraDataSectionType.STORAGE);

        await base.OnPickupAsync(ctx, ct);
    }

    protected override Task OnWiredStackChangedAsync(
        ActionContext ctx,
        List<int> ids,
        CancellationToken ct
    ) =>
        _ctx.PublishRoomEventAsync(
            new WiredVariableBoxChangedEvent
            {
                RoomId = _ctx.RoomId,
                CausedBy = ctx,
                BoxIds = [_ctx.ObjectId.Value],
            },
            ct
        );
}
