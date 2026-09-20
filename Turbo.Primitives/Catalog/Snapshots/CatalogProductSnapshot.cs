using Orleans;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Players.Enums;

namespace Turbo.Primitives.Catalog.Snapshots;

[GenerateSerializer, Immutable]
public sealed record CatalogProductSnapshot
{
    [Id(0)]
    public required int Id { get; init; }

    [Id(1)]
    public required int OfferId { get; init; }

    [Id(2)]
    public required ProductType ProductType { get; init; }

    [Id(3)]
    public required int FurniDefinitionId { get; init; }

    [Id(4)]
    public required int SpriteId { get; init; }

    [Id(5)]
    public required string? ExtraParam { get; init; }

    [Id(6)]
    public required int Quantity { get; init; }

    [Id(7)]
    public required int UniqueSize { get; init; }

    [Id(8)]
    public required int UniqueRemaining { get; init; }

    [Id(9)]
    public int? LtdSeriesId { get; init; }

    [Id(10)]
    public required string? ClassName { get; init; }

    /// <summary>The subscription this product grants, or null when it grants furniture.</summary>
    [Id(11)]
    public SubscriptionType? SubscriptionType { get; init; }

    /// <summary>Days of membership the product grants; zero unless it grants a subscription.</summary>
    [Id(12)]
    public int SubscriptionDays { get; init; }
}
