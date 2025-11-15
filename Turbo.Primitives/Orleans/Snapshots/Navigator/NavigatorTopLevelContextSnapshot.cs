using System.Collections.Generic;
using System.Collections.Immutable;
using Orleans;

namespace Turbo.Primitives.Orleans.Snapshots.Navigator;

[GenerateSerializer, Immutable]
public record NavigatorTopLevelContextSnapshot
{
    [Id(0)]
    public required string SearchCode { get; init; }

    [Id(1)]
    public required ImmutableArray<NavigatorQuickLinkSnapshot> QuickLinks { get; init; }
}
