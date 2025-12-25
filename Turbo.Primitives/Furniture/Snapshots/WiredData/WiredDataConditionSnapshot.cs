using System.Collections.Generic;
using Orleans;

namespace Turbo.Primitives.Furniture.Snapshots.WiredData;

[GenerateSerializer, Immutable]
public record WiredDataConditionSnapshot : WiredDataSnapshot
{
    [Id(0)]
    public required int QuantifierCode { get; init; }

    [Id(1)]
    public required int QuantifierType { get; init; }

    [Id(2)]
    public required bool IsInvert { get; init; }
}
