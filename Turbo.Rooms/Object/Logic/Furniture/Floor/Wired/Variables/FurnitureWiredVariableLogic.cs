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
using Turbo.Rooms.Wired;
using Turbo.Rooms.Wired.Variables;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Variables;

public abstract class FurnitureWiredVariableLogic : FurnitureWiredLogic
{
    public override WiredType WiredType => WiredType.Variable;

    public IStorageData StorageData { get; private set; }
    public WiredAvailabilityType StorageType { get; protected set; } =
        WiredAvailabilityType.Temporary;
    public WiredVariableDefinition? VariableDefinition { get; protected set; }

    protected virtual bool _hasValue { get; set; } = false;

    public FurnitureWiredVariableLogic(
        IWiredDataFactory wiredDataFactory,
        IGrainFactory grainFactory,
        IStuffDataFactory stuffDataFactory,
        IRoomFloorItemContext ctx
    )
        : base(wiredDataFactory, grainFactory, stuffDataFactory, ctx)
    {
        if (ctx.Item.ExtraData.TryGetSection("storage", out var storageElement))
        {
            StorageData = storageElement.Deserialize<StorageData>()!;
        }
        else
        {
            StorageData = new StorageData();
        }

        StorageData.SetAction(async () =>
        {
            _ctx.Item.ExtraData.UpdateSection(
                "storage",
                JsonSerializer.SerializeToNode(StorageData, StorageData.GetType())
            );
        });
    }

    public virtual WiredVariableTargetType GetVariableTargetType() => WiredVariableTargetType.None;

    public virtual WiredVariableFlags GetVariableFlags() => WiredVariableFlags.None;

    public virtual List<WiredVariableRegistration> GetVariableRegistrations()
    {
        var result = new List<WiredVariableRegistration>();

        if (VariableDefinition is not null)
        {
            result.Add(
                new()
                {
                    Definition = VariableDefinition,
                    Getter = (key) =>
                        StorageData.TryGet(key, out var value) ? (true, value) : (false, default),
                    Setter = StorageData.SetValue,
                    Remover = StorageData.Remove,
                }
            );
        }

        return result;
    }

    public virtual Task ApplyAsync(WiredProcessingContext ctx, CancellationToken ct) =>
        Task.CompletedTask;

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
        await base.FillInternalDataAsync(ct);

        try
        {
            var key = WiredData.StringParam;

            if (!string.IsNullOrWhiteSpace(key))
            {
                VariableDefinition = new()
                {
                    Key = key,
                    Name = key,
                    Target = GetVariableTargetType(),
                    ValueKind = WiredValueType.Int,
                    AvailabilityType = StorageType,
                    InputSourceType = GetVariableTargetType() switch
                    {
                        WiredVariableTargetType.User => WiredInputSourceType.PlayerSource,
                        WiredVariableTargetType.Furni => WiredInputSourceType.FurniSource,
                        _ => WiredInputSourceType.MergedSource,
                    },
                    Flags = GetVariableFlags(),
                };
            }
        }
        catch { }
    }
}
