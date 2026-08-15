using Orleans;
using Turbo.Primitives.Furniture.Enums;

namespace Turbo.Primitives.Catalog.Admin;

[GenerateSerializer, Immutable]
public sealed record CreateCatalogProductInput
{
    [Id(0)]
    public required int OfferId { get; init; }

    [Id(1)]
    public required ProductType ProductType { get; init; }

    [Id(2)]
    public int? FurnitureDefinitionId { get; init; }

    [Id(3)]
    public string? ExtraParam { get; init; }

    [Id(4)]
    public required int Quantity { get; init; }
}
