using Orleans;

namespace Turbo.Primitives.Catalog;

/// <summary>
/// An offer bought for someone else, as the catalog's gift dialog sends it: who receives it,
/// the note on the tag, and the wrapping picked (the present's sprite, box style and ribbon;
/// box and ribbon are 0 for the free box).
/// </summary>
[GenerateSerializer, Immutable]
public sealed record CatalogGiftRequest
{
    [Id(0)]
    public required int OfferId { get; init; }

    [Id(1)]
    public required string ExtraParam { get; init; }

    [Id(2)]
    public required string ReceiverName { get; init; }

    [Id(3)]
    public required string Message { get; init; }

    [Id(4)]
    public required int SpriteId { get; init; }

    [Id(5)]
    public required int BoxType { get; init; }

    [Id(6)]
    public required int RibbonType { get; init; }

    /// <summary>Whether the tag shows the buyer's name and face; otherwise the gift is anonymous.</summary>
    [Id(7)]
    public required bool ShowPurchaserName { get; init; }
}
