using Orleans;

namespace Turbo.Primitives.Catalog;

[GenerateSerializer, Immutable]
public sealed record CatalogBalanceFailure
{
    [Id(0)]
    public required bool NotEnoughCredits { get; init; }

    [Id(1)]
    public required bool NotEnoughActivityPoints { get; init; }

    [Id(2)]
    public required int ActivityPointType { get; init; }
}
