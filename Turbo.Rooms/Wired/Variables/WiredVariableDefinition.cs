using System.Collections.Generic;
using Turbo.Primitives.Furniture;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Snapshots.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;

namespace Turbo.Rooms.Wired.Variables;

public readonly record struct WiredVariableDefinition : IWiredVariableDefinition
{
    public required int VariableId { get; init; }
    public required string VariableName { get; init; }
    public required WiredAvailabilityType AvailabilityType { get; init; }
    public required WiredVariableTargetType TargetType { get; init; }
    public required WiredVariableFlags Flags { get; init; }
    public required Dictionary<int, string> TextConnectors { get; init; }

    public WiredVariableSnapshot GetSnapshot() =>
        new()
        {
            VariableId = VariableId,
            VariableName = VariableName,
            VariableHash = WiredVariableHashBuilder.HashVariableDefinition(this),
            AvailabilityType = AvailabilityType,
            TargetType = TargetType,
            AlwaysAvailable = Flags.Has(WiredVariableFlags.AlwaysAvailable),
            CanCreateAndDelete = Flags.Has(WiredVariableFlags.CanCreateAndDelete),
            HasValue = Flags.Has(WiredVariableFlags.HasValue),
            CanWriteValue = Flags.Has(WiredVariableFlags.CanWriteValue),
            CanInterceptChanges = Flags.Has(WiredVariableFlags.CanInterceptChanges),
            IsInvisible = Flags.Has(WiredVariableFlags.IsInvisible),
            CanReadCreationTime = Flags.Has(WiredVariableFlags.CanReadCreationTime),
            CanReadLastUpdateTime = Flags.Has(WiredVariableFlags.CanReadLastUpdateTime),
            HasTextConnector = Flags.Has(WiredVariableFlags.HasTextConnector),
            TextConnectors = TextConnectors,
            IsStored = Flags.Has(WiredVariableFlags.IsStored),
        };
}
