using Orleans;
using Turbo.Primitives.Catalog.Snapshots;

namespace Turbo.Primitives.Inventory;

/// <summary>
/// A gift for the receiving inventory to wrap: the furni product bought (with the extra param
/// it was bought with), the present it goes in and how that present is tied, and what the tag
/// says. The purchase grain has checked all of it and taken the money.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record PresentGrantRequest
{
    [Id(0)]
    public required CatalogProductSnapshot Product { get; init; }

    [Id(1)]
    public required string ExtraParam { get; init; }

    [Id(2)]
    public required int PresentDefinitionId { get; init; }

    [Id(3)]
    public required int BoxType { get; init; }

    [Id(4)]
    public required int RibbonType { get; init; }

    [Id(5)]
    public required string Message { get; init; }

    /// <summary>The buyer's name for the tag; null for an anonymous gift.</summary>
    [Id(6)]
    public required string? PurchaserName { get; init; }

    /// <summary>The buyer's figure for the tag; null for an anonymous gift.</summary>
    [Id(7)]
    public required string? PurchaserFigure { get; init; }

    /// <summary>
    /// The buyer's name for what is engraved with it: a gifted trophy names who gave it, not
    /// who received it. Set even for an anonymous gift, whose tag stays blank.
    /// </summary>
    [Id(8)]
    public required string BuyerName { get; init; }
}
