using Orleans;

namespace Turbo.Primitives.Catalog.Admin;

[GenerateSerializer, Immutable]
public sealed record UpdateCatalogOfferInput
{
    [Id(0)]
    public required int PageId { get; init; }

    [Id(1)]
    public required string LocalizationId { get; init; }

    [Id(2)]
    public required int CostCredits { get; init; }

    [Id(3)]
    public required int CostCurrency { get; init; }

    [Id(4)]
    public int? CurrencyTypeId { get; init; }

    [Id(5)]
    public required bool CanGift { get; init; }

    [Id(6)]
    public required bool CanBundle { get; init; }

    [Id(7)]
    public required int ClubLevel { get; init; }

    [Id(8)]
    public required bool Visible { get; init; }
}
