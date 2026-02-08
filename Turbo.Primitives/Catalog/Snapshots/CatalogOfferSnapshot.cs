using System.Collections.Immutable;
using Orleans;

namespace Turbo.Primitives.Catalog.Snapshots;

[GenerateSerializer, Immutable]
public sealed record CatalogOfferSnapshot
{
    [Id(0)]
    public required int Id { get; init; }

    [Id(1)]
    public required int PageId { get; init; }

    [Id(2)]
    public required string LocalizationId { get; init; }

    [Id(3)]
    public required bool Rentable { get; init; }

    [Id(4)]
    public required int CostCredits { get; init; }

    [Id(5)]
    public required int CostCurrency { get; init; }

    [Id(6)]
    public required int? CurrencyTypeId { get; init; }

    [Id(7)]
    public required int CostSilver { get; init; }

    [Id(8)]
    public required bool CanGift { get; init; }

    [Id(9)]
    public required bool CanBundle { get; init; }

    [Id(10)]
    public required int ClubLevel { get; init; }

    [Id(11)]
    public required bool Visible { get; init; }

    [Id(12)]
    public required ImmutableArray<int> ProductIds { get; init; }

    [Id(13)]
    public required ImmutableArray<CatalogProductSnapshot> Products { get; init; }
}
