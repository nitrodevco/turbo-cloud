using System;
using Turbo.Primitives.Furniture;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables;

public abstract class WiredVariable(RoomGrain roomGrain) : IWiredVariable
{
    protected readonly RoomGrain _roomGrain = roomGrain;

    public abstract IWiredVariableDefinition VarDefinition { get; }
    public required IStorageData StorageData { get; init; }

    public virtual bool CanBind(in IWiredVariableBinding binding) => false;

    public virtual bool TryGet(
        in IWiredVariableBinding binding,
        IWiredExecutionContext ctx,
        out int value
    )
    {
        value = 0;

        return false;
    }

    public virtual bool SetValue(
        in IWiredVariableBinding binding,
        IWiredExecutionContext ctx,
        int value
    ) => false;

    public virtual bool RemoveValue(string key) => false;

    public override int GetHashCode()
    {
        long hashValue = 0;

        hashValue ^= HashString(VarDefinition.Key);
        hashValue ^= HashEnum(VarDefinition.InputSourceType);
        hashValue ^= HashString(VarDefinition.Name);
        hashValue ^= HashEnum(VarDefinition.AvailabilityType);
        hashValue ^= HashEnum(VarDefinition.Target);

        hashValue ^= HashInt(VarDefinition.Flags.Has(WiredVariableFlags.AlwaysAvailable) ? 1 : 0);
        hashValue ^= HashInt(
            VarDefinition.Flags.Has(WiredVariableFlags.CanCreateAndDelete) ? 1 : 0
        );
        hashValue ^= HashInt(VarDefinition.Flags.Has(WiredVariableFlags.HasValue) ? 1 : 0);
        hashValue ^= HashInt(VarDefinition.Flags.Has(WiredVariableFlags.CanWriteValue) ? 1 : 0);
        hashValue ^= HashInt(
            VarDefinition.Flags.Has(WiredVariableFlags.CanInterceptChanges) ? 1 : 0
        );
        hashValue ^= HashInt(VarDefinition.Flags.Has(WiredVariableFlags.IsInvisible) ? 1 : 0);
        hashValue ^= HashInt(
            VarDefinition.Flags.Has(WiredVariableFlags.CanReadCreationTime) ? 1 : 0
        );
        hashValue ^= HashInt(
            VarDefinition.Flags.Has(WiredVariableFlags.CanReadLastUpdateTime) ? 1 : 0
        );
        hashValue ^= HashInt(VarDefinition.Flags.Has(WiredVariableFlags.HasTextConnector) ? 1 : 0);
        hashValue ^= HashInt(VarDefinition.TextConnectors.GetHashCode());

        return (int)hashValue;
    }

    private static int HashInt(int value)
    {
        unchecked
        {
            return value * 397;
        }
    }

    private static int HashString(string? value) => value?.GetHashCode() ?? 0;

    private static int HashEnum<T>(T value)
        where T : Enum => value.GetHashCode();
}
