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
using Turbo.Primitives.Rooms.Snapshots.Wired;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Wired.Variables;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Variables;

public abstract class FurnitureWiredVariableLogic : FurnitureWiredLogic, IWiredVariable
{
    public override WiredType WiredType => WiredType.Variable;

    protected readonly int _variableId;
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
        _variableId = _roomGrain._state.NextVariableId++;

        if (ctx.Item.ExtraData.TryGetSection("storage", out var storageElement))
        {
            _storageData = storageElement.Deserialize<StorageData>()!;
        }
        else
        {
            _storageData = new StorageData();
        }

        _storageData.SetAction(() =>
        {
            _ctx.Item.ExtraData.UpdateSection(
                "storage",
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
            AvailabilityType = WiredAvailabilityType.Temporary,
            TargetType = WiredVariableTargetType.None,
            Flags = WiredVariableFlags.None,
            TextConnectors = [],
        };

    public virtual bool CanBind(in IWiredVariableBinding binding) => false;

    public virtual bool TryGet(in IWiredVariableBinding binding, out int value)
    {
        value = 0;

        return false;
    }

    public virtual Task<bool> SetValueAsync(
        IWiredVariableBinding binding,
        IWiredExecutionContext ctx,
        int value
    ) => Task.FromResult(false);

    public virtual bool RemoveValue(string key) => false;

    protected override Task OnWiredChangedAsync(
        ActionContext? ctx,
        List<int> ids,
        CancellationToken ct
    ) =>
        _ctx.PublishRoomEventAsync(
            new WiredVariableBoxChangedEvent
            {
                RoomId = _ctx.RoomId,
                CausedBy = ctx,
                BoxIds = [_ctx.ObjectId.Value, .. ids],
            },
            ct
        );

    protected override async Task FillInternalDataAsync(CancellationToken ct)
    {
        _varSnapshot = null;

        await base.FillInternalDataAsync(ct);
    }

    public WiredVariableKey GetVariableKey()
    {
        var snapshot = GetVarSnapshot();

        return new WiredVariableKey(snapshot.TargetType, snapshot.VariableName);
    }

    public WiredVariableSnapshot GetVarSnapshot() =>
        _varSnapshot ??= BuildVariableDefinition().GetSnapshot();
}
