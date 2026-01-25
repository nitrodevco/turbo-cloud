using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Furniture;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Snapshots.Wired.Variables;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Wired.Variables;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Variables;

public abstract class FurnitureWiredVariableLogic : FurnitureWiredLogic, IWiredVariable
{
    public override WiredType WiredType => WiredType.Variable;

    protected readonly WiredVariableId _variableId;
    protected IStorageData _storageData;
    protected WiredVariableSnapshot? _varSnapshot;

    public FurnitureWiredVariableLogic(
        IWiredDataFactory wiredDataFactory,
        IGrainFactory grainFactory,
        IStuffDataFactory stuffDataFactory,
        IRoomFloorItemContext ctx
    )
        : base(wiredDataFactory, grainFactory, stuffDataFactory, ctx)
    {
        _variableId = WiredVariableIdBuilder.CreateFromBoxId(ctx.ObjectId.Value);

        if (
            ctx.RoomObject.ExtraData.TryGetSection(
                ExtraDataSectionType.STORAGE,
                out var storageElement
            )
        )
        {
            _storageData = storageElement.Deserialize<StorageData>()!;
        }
        else
        {
            _storageData = new StorageData();
        }

        _storageData.SetAction(() =>
        {
            var snapshot = GetVarSnapshot();

            if (snapshot.AvailabilityType == WiredAvailabilityType.Persistent)
                _ctx.RoomObject.ExtraData.UpdateSection(
                    ExtraDataSectionType.STORAGE,
                    JsonSerializer.SerializeToNode(_storageData, _storageData.GetType())
                );

            return Task.CompletedTask;
        });
    }

    protected virtual WiredVariableDefinition BuildVariableDefinition() =>
        new()
        {
            VariableId = _variableId,
            VariableName = string.Empty,
            VariableType = WiredVariableType.Created,
            AvailabilityType = WiredAvailabilityType.RoomActive,
            TargetType = WiredVariableTargetType.None,
            Flags = WiredVariableFlags.None,
            TextConnectors = [],
        };

    public virtual bool TryGet(in WiredVariableBinding binding, out int value)
    {
        value = default;

        if (!_storageData.TryGet(binding.ToString(), out var stored))
            return false;

        value = stored;

        return true;
    }

    public virtual Task<bool> GiveValueAsync(
        WiredVariableBinding binding,
        IWiredExecutionContext ctx,
        int value,
        bool replace = false
    )
    {
        var bindingKey = binding.ToString();
        var snapshot = GetVarSnapshot();

        if (
            (binding.TargetType != snapshot.TargetType)
            || !snapshot.CanCreateAndDelete
            || (_storageData.HasKey(bindingKey) && !replace)
        )
            return Task.FromResult(false);

        if (!snapshot.CanWriteValue)
            value = default;

        _storageData.SetValue(bindingKey, value);

        return Task.FromResult(true);
    }

    public virtual Task<bool> SetValueAsync(
        WiredVariableBinding binding,
        IWiredExecutionContext ctx,
        int value
    )
    {
        var bindingKey = binding.ToString();
        var snapshot = GetVarSnapshot();

        if (!_storageData.HasKey(bindingKey) || !snapshot.CanWriteValue)
            return Task.FromResult(false);

        _storageData.SetValue(bindingKey, value);

        return Task.FromResult(true);
    }

    public virtual bool RemoveValue(WiredVariableBinding binding) => false;

    public WiredVariableKey GetVariableKey()
    {
        var snapshot = GetVarSnapshot();

        return new WiredVariableKey(snapshot.TargetType, snapshot.VariableName);
    }

    public WiredVariableSnapshot GetVarSnapshot() =>
        _varSnapshot ??= BuildVariableDefinition().GetSnapshot();

    protected override Task FillInternalDataAsync(CancellationToken ct)
    {
        _varSnapshot = null;

        return base.FillInternalDataAsync(ct);
    }

    public override async Task OnPickupAsync(ActionContext ctx, CancellationToken ct)
    {
        _ctx.RoomObject.ExtraData.DeleteSection(ExtraDataSectionType.STORAGE);

        await base.OnPickupAsync(ctx, ct);
    }

    protected override Task OnWiredStackChangedAsync(
        ActionContext? ctx,
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
