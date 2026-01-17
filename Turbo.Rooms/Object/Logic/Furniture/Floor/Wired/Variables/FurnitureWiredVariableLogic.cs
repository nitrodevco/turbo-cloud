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
using Turbo.Rooms.Wired;
using Turbo.Rooms.Wired.Variables;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Variables;

public abstract class FurnitureWiredVariableLogic : FurnitureWiredLogic, IWiredVariable
{
    public override WiredType WiredType => WiredType.Variable;

    public int VariableId { get; set; }
    public string VariableName { get; protected set; } = string.Empty;
    public IStorageData StorageData { get; private set; }

    protected virtual bool _hasValue { get; set; } = false;
    protected WiredVariableSnapshot? _varSnapshot;

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

        StorageData.SetAction(() =>
        {
            _ctx.Item.ExtraData.UpdateSection(
                "storage",
                JsonSerializer.SerializeToNode(StorageData, StorageData.GetType())
            );
            return Task.CompletedTask;
        });
    }

    public virtual WiredVariableTargetType GetVariableTargetType() => WiredVariableTargetType.None;

    public virtual WiredAvailabilityType GetVariableAvailabilityType() =>
        WiredAvailabilityType.Temporary;

    public virtual WiredInputSourceType GetVariableInputSourceType() =>
        WiredInputSourceType.MergedSource;

    public virtual WiredVariableFlags GetVariableFlags()
    {
        WiredVariableFlags flags = WiredVariableFlags.None;

        if (_hasValue)
            flags = flags.Add(WiredVariableFlags.HasValue);

        return flags;
    }

    public virtual Dictionary<int, string> GetTextConnectors() => [];

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
        _varSnapshot = null;

        try
        {
            VariableName = WiredData.StringParam;
        }
        catch { }

        await base.FillInternalDataAsync(ct);
    }

    public WiredVariableSnapshot GetVarSnapshot() => _varSnapshot ??= BuildVarSnapshot();

    private WiredVariableSnapshot BuildVarSnapshot()
    {
        var key = new WiredVariableKey(GetVariableTargetType(), VariableName).GetHashCode();
        var flags = GetVariableFlags();

        return new()
        {
            VariableId = key,
            VariableName = VariableName,
            AvailabilityType = GetVariableAvailabilityType(),
            InputSourceType = GetVariableInputSourceType(),
            AlwaysAvailable = flags.Has(WiredVariableFlags.AlwaysAvailable),
            CanCreateAndDelete = flags.Has(WiredVariableFlags.CanCreateAndDelete),
            HasValue = flags.Has(WiredVariableFlags.HasValue),
            CanWriteValue = flags.Has(WiredVariableFlags.CanWriteValue),
            CanInterceptChanges = flags.Has(WiredVariableFlags.CanInterceptChanges),
            IsInvisible = flags.Has(WiredVariableFlags.IsInvisible),
            CanReadCreationTime = flags.Has(WiredVariableFlags.CanReadCreationTime),
            CanReadLastUpdateTime = flags.Has(WiredVariableFlags.CanReadLastUpdateTime),
            HasTextConnector = flags.Has(WiredVariableFlags.HasTextConnector),
            TextConnectors = GetTextConnectors(),
            IsStored = flags.Has(WiredVariableFlags.IsStored),
            VariableHash = BuildHash(),
        };
    }

    private long BuildHash()
    {
        ulong hashCode = 0;

        hashCode = CombineXor(hashCode, Fnv1a64(VariableName));
        hashCode = CombineXor(hashCode, (ulong)GetVariableTargetType());
        hashCode = CombineXor(hashCode, (ulong)GetVariableAvailabilityType());
        hashCode = CombineXor(hashCode, (ulong)GetVariableInputSourceType());
        hashCode = CombineXor(hashCode, (ulong)GetVariableFlags());

        var textConnectors = GetTextConnectors();

        if (textConnectors is not null)
        {
            foreach (var s in textConnectors.Values)
                hashCode = CombineXor(hashCode, Fnv1a64(s ?? ""));
        }

        return unchecked((long)hashCode);
    }

    private static ulong Fnv1a64(string s)
    {
        const ulong offset = 14695981039346656037UL;
        const ulong prime = 1099511628211UL;

        ulong hash = offset;

        foreach (var ch in s)
        {
            hash ^= (byte)ch;
            hash *= prime;
            hash ^= (byte)(ch >> 8);
            hash *= prime;
        }

        return hash;
    }

    private static ulong CombineXor(ulong hash, ulong value)
    {
        value ^= value >> 33;
        value *= 0xff51afd7ed558ccdUL;
        value ^= value >> 33;
        value *= 0xc4ceb9fe1a85ec53UL;
        value ^= value >> 33;

        return hash ^ value;
    }
}
