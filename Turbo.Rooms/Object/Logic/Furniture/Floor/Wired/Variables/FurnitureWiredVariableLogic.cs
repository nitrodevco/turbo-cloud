using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Snapshots.Wired.Variables;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains.Storage;
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

        if (!CanBind(key) || !TryGetStore(key, out var store) || store is null)
            return false;

        return store.TryGetValue(key, out value);
    }

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
            || !TryGetStore(key, out var store)
            || store is null
            || (store.ContainsKey(key) && !replace)
        )
            return Task.FromResult(false);

        return store.GiveValueAsync(key, value, replace);
    }

    public virtual Task<bool> SetValueAsync(
        IWiredExecutionContext ctx,
        WiredVariableKey key,
        WiredVariableValue value
    )
    {
        if (!TryGetStore(key, out var store) || store is null || !store.ContainsKey(key))
            return Task.FromResult(false);

        return store.SetValueAsync(ctx, key, value);
    }

    public virtual bool RemoveValue(WiredVariableKey key)
    {
        if (!TryGetStore(key, out var store) || store is null)
            return false;

        return store.RemoveValue(key);
    }

    public virtual Dictionary<WiredVariableValue, string> GetTextConnectors() => [];

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

    private bool TryGetStore(WiredVariableKey key, out KeyValueStore? store)
    {
        if (_storage is not null)
        {
            store = _storage;

            return true;
        }

        return _roomGrain.WiredSystem.TryGetStoreForKey(key, out store);
    }

    public WiredVariableSnapshot GetVarSnapshot() => _varSnapshot ??= BuildVarSnapshot();

    protected virtual WiredVariableSnapshot BuildVarSnapshot()
    {
        var textConnectors = GetTextConnectors();
        var variableHash = WiredVariableHashBuilder.HashValues(
            _wiredData.StringParam,
            AvailabilityType,
            TargetType,
            Flags,
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
            Flags = Flags,
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
