using System.Collections.Generic;
using Orleans;

namespace Turbo.Primitives.Snapshots.NewNavigator;

[GenerateSerializer, Immutable]
public record NavigatorTopLevelContextsSnapshot
{
    [Id(0)]
    public required string SearchCode { get; init; }

    [Id(1)]
    public required List<NavigatorQuickLinkSnapshot> QuickLinks { get; init; }
}
