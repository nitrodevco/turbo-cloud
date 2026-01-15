using System;
using System.Collections.Generic;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Primitives.Snapshots.Wired;

namespace Turbo.Rooms.Wired.Variables;

public sealed class WiredVariableDefinition : IWiredVariableDefinition
{
    public required string Key { get; init; }
    public required string Name { get; init; }
    public required WiredVariableTargetType Target { get; init; }
    public required WiredAvailabilityType AvailabilityType { get; init; }
    public required WiredInputSourceType InputSourceType { get; init; }
    public required WiredVariableFlags Flags { get; init; }
    public List<string> TextConnectors { get; init; } = [];

    public override int GetHashCode()
    {
        long hashValue = 0;

        hashValue ^= HashString(Key);
        hashValue ^= HashEnum(InputSourceType);
        hashValue ^= HashString(Name);
        hashValue ^= HashEnum(AvailabilityType);
        hashValue ^= HashEnum(Target);

        hashValue ^= HashInt(Flags.Has(WiredVariableFlags.AlwaysAvailable) ? 1 : 0);
        hashValue ^= HashInt(Flags.Has(WiredVariableFlags.CanCreateAndDelete) ? 1 : 0);
        hashValue ^= HashInt(Flags.Has(WiredVariableFlags.HasValue) ? 1 : 0);
        hashValue ^= HashInt(Flags.Has(WiredVariableFlags.CanWriteValue) ? 1 : 0);
        hashValue ^= HashInt(Flags.Has(WiredVariableFlags.CanInterceptChanges) ? 1 : 0);
        hashValue ^= HashInt(Flags.Has(WiredVariableFlags.IsInvisible) ? 1 : 0);
        hashValue ^= HashInt(Flags.Has(WiredVariableFlags.CanReadCreationTime) ? 1 : 0);
        hashValue ^= HashInt(Flags.Has(WiredVariableFlags.CanReadLastUpdateTime) ? 1 : 0);
        hashValue ^= HashInt(Flags.Has(WiredVariableFlags.HasTextConnector) ? 1 : 0);
        hashValue ^= HashInt(TextConnectors.GetHashCode());

        return (int)hashValue;
    }

    public WiredVariableSnapshot GetSnapshot() =>
        new()
        {
            HashCode = GetHashCode(),
            VariableId = 0,
            VariableName = Name,
            AvailabilityType = AvailabilityType,
            VariableType = InputSourceType,
            AlwaysAvailable = Flags.Has(WiredVariableFlags.AlwaysAvailable),
            CanCreateAndDelete = Flags.Has(WiredVariableFlags.CanCreateAndDelete),
            HasValue = Flags.Has(WiredVariableFlags.HasValue),
            CanWriteValue = Flags.Has(WiredVariableFlags.CanWriteValue),
            CanInterceptChanges = Flags.Has(WiredVariableFlags.CanInterceptChanges),
            IsInvisible = Flags.Has(WiredVariableFlags.IsInvisible),
            CanReadCreationTime = Flags.Has(WiredVariableFlags.CanReadCreationTime),
            CanReadLastUpdateTime = Flags.Has(WiredVariableFlags.CanReadLastUpdateTime),
            HasTextConnector = Flags.Has(WiredVariableFlags.HasTextConnector),
            TextConnector = Flags.Has(WiredVariableFlags.HasTextConnector) ? ["default"] : [],
            IsStored = Flags.HasFlag(WiredVariableFlags.IsStored),
        };

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
