using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Snapshots.Wired;

namespace Turbo.Rooms.Wired.Variables;

public sealed class WiredVariableDefinition
{
    public required string Key { get; init; }
    public required string Name { get; init; }
    public required WiredVariableTargetType Target { get; init; }
    public required WiredValueType ValueKind { get; init; }
    public required WiredAvailabilityType AvailabilityType { get; init; }
    public required WiredInputSourceType InputSourceType { get; init; }
    public required WiredVariableFlags Flags { get; init; }

    public WiredVariableSnapshot GetSnapshot() =>
        new()
        {
            VariableId = Key.GetHashCode(),
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
}
