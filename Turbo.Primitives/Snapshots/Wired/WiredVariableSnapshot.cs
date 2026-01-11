using System.Collections.Generic;
using Orleans;
using Turbo.Primitives.Rooms.Enums.Wired;

namespace Turbo.Primitives.Snapshots.Wired;

[GenerateSerializer, Immutable]
public record WiredVariableSnapshot
{
    [Id(0)]
    public required long VariableId { get; init; }

    [Id(1)]
    public required string VariableName { get; init; }

    [Id(2)]
    public required WiredAvailabilityType AvailabilityType { get; init; }

    [Id(3)]
    public required WiredInputSourceType VariableType { get; init; }

    [Id(4)]
    public required bool AlwaysAvailable { get; init; }

    [Id(5)]
    public required bool CanCreateAndDelete { get; init; }

    [Id(6)]
    public required bool HasValue { get; init; }

    [Id(7)]
    public required bool CanWriteValue { get; init; }

    [Id(8)]
    public required bool CanInterceptChanges { get; init; }

    [Id(9)]
    public required bool IsInvisible { get; init; }

    [Id(10)]
    public required bool CanReadCreationTime { get; init; }

    [Id(11)]
    public required bool CanReadLastUpdateTime { get; init; }

    [Id(12)]
    public required bool HasTextConnector { get; init; }

    [Id(13)]
    public required List<string> TextConnector { get; init; }

    [Id(14)]
    public required bool IsStored { get; init; }
}
