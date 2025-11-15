using System.Collections.Immutable;
using Orleans;
using Turbo.Contracts.Enums.Navigator;

namespace Turbo.Primitives.Orleans.Snapshots.Navigator;

[GenerateSerializer, Immutable]
public record NavigatorSearchResultBlockSnapshot
{
    [Id(0)]
    public required string SearchCode { get; init; }

    [Id(1)]
    public required string Text { get; init; }

    [Id(2)]
    public required NavigatorActionAllowedType ActionAllowed { get; init; }

    [Id(3)]
    public required string Localization { get; init; }

    [Id(4)]
    public required bool ForceClosed { get; init; }

    [Id(5)]
    public required NavigatorViewModeType ViewMode { get; init; }

    [Id(6)]
    public required ImmutableArray<NavigatorSearchResultSnapshot> SearchResults { get; init; }
}
