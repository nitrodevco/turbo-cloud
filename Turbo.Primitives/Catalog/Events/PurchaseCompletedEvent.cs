using Turbo.Primitives.Catalog.Enums;
using Turbo.Primitives.Events;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Snapshots;

namespace Turbo.Primitives.Catalog.Events;

/// <summary>
/// A catalogue offer was bought and handed over: paid for and in the buyer's inventory, or the
/// receiver's for a gift, or a room ad running in the buyer's room (<see cref="RoomAd"/>). The
/// costs are what the buyer paid in all, after any purchase policy.
/// </summary>
public sealed record PurchaseCompletedEvent : DomainEvent
{
    public required PlayerId PlayerId { get; init; }

    public required CatalogType CatalogType { get; init; }

    public required int OfferId { get; init; }

    /// <summary>The offer's product code (its localization id).</summary>
    public required string ProductCode { get; init; }

    /// <summary>How many of the offer were given, bulk-buy bonus included; 1 for a gift.</summary>
    public required int Units { get; init; }

    public int CostCredits { get; init; }

    public int CostSilver { get; init; }

    public int CostActivityPoints { get; init; }

    /// <summary>The activity point currency of <see cref="CostActivityPoints"/>; null when none.</summary>
    public int? ActivityPointType { get; init; }

    /// <summary>Who it was a gift for; null when bought for oneself.</summary>
    public PlayerId? GiftReceiverId { get; init; }

    /// <summary>The room event a room ad bought (or extended) as it now runs; null for any other offer.</summary>
    public RoomEventSnapshot? RoomAd { get; init; }

    /// <summary>Whether the room ad extended the event already running rather than starting one.</summary>
    public bool RoomAdExtended { get; init; }
}
