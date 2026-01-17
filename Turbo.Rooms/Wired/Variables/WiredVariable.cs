using System.Collections.Generic;
using System.Threading.Tasks;
using Turbo.Primitives.Furniture;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Snapshots.Wired;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables;

public abstract class WiredVariable(RoomGrain roomGrain) : IWiredVariable
{
    protected readonly RoomGrain _roomGrain = roomGrain;

    public required int VariableId { get; set; }
    public virtual string VariableName { get; set; } = string.Empty;
    public required IStorageData StorageData { get; set; }

    protected WiredVariableSnapshot? _snapshot;

    public virtual WiredVariableTargetType GetVariableTargetType() => WiredVariableTargetType.None;

    public virtual WiredAvailabilityType GetVariableAvailabilityType() =>
        WiredAvailabilityType.Temporary;

    public virtual WiredInputSourceType GetVariableInputSourceType() =>
        WiredInputSourceType.MergedSource;

    public virtual WiredVariableFlags GetVariableFlags()
    {
        WiredVariableFlags flags = WiredVariableFlags.None;

        return flags;
    }

    public virtual Dictionary<int, string> GetTextConnectors() => [];

    public virtual bool CanBind(in IWiredVariableBinding binding) =>
        GetVariableTargetType() == binding.Target;

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

    public WiredVariableSnapshot GetVarSnapshot() => _snapshot ??= BuildVarSnapshot();

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
