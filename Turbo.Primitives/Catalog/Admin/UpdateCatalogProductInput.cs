using Orleans;
using Turbo.Primitives.Furniture.Enums;

namespace Turbo.Primitives.Catalog.Admin;

[GenerateSerializer, Immutable]
public sealed record UpdateCatalogProductInput
{
    [Id(0)]
    public required ProductType ProductType { get; init; }

    [Id(1)]
    public int? FurnitureDefinitionId { get; init; }

    [Id(2)]
    public string? ExtraParam { get; init; }

    [Id(3)]
    public required int Quantity { get; init; }
}
