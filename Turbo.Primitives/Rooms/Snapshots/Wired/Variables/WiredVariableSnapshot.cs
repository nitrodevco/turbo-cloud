using System.Collections.Generic;
using Orleans;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;

namespace Turbo.Primitives.Rooms.Snapshots.Wired.Variables;

[GenerateSerializer, Immutable]
public record WiredVariableSnapshot
{
    [Id(0)]
    public required WiredVariableId VariableId { get; init; }

    [Id(1)]
    public required string VariableName { get; init; }

    [Id(2)]
    public required WiredVariableType VariableType { get; init; }

    [Id(3)]
    public required WiredVariableHash VariableHash { get; init; }

    [Id(4)]
    public required WiredAvailabilityType AvailabilityType { get; init; }

    [Id(5)]
    public required WiredVariableTargetType TargetType { get; init; }

    [Id(6)]
    public required WiredVariableFlags Flags { get; init; }

    [Id(7)]
    public required Dictionary<WiredVariableValue, string> TextConnectors { get; init; }
}
