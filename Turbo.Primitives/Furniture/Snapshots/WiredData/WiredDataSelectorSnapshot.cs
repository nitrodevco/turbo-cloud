using System.Collections.Generic;
using Orleans;

namespace Turbo.Primitives.Furniture.Snapshots.WiredData;

[GenerateSerializer, Immutable]
public record WiredDataSelectorSnapshot : WiredDataSnapshot
{
    [Id(0)]
    public required bool IsFilter { get; init; }

    [Id(1)]
    public required bool IsInvert { get; init; }
}
