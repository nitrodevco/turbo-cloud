using System.Collections.Generic;

namespace Turbo.Import.Morningstar;

/// <summary>
/// The target's own limits, from its configuration, that what an import copies is held to: a
/// copied room, message, wired box or present keeps the rules one made on this hotel keeps.
/// </summary>
public sealed record ImportLimits
{
    /// <summary><c>RoomConfig.RoomNameMaxLength</c>.</summary>
    public required int RoomNameMaxLength { get; init; }

    /// <summary><c>RoomConfig.RoomDescriptionMaxLength</c>.</summary>
    public required int RoomDescriptionMaxLength { get; init; }

    /// <summary><c>RoomConfig.MaxPlayersLimit</c>: the most visitors a room may allow.</summary>
    public required int RoomPlayersMax { get; init; }

    /// <summary><c>RoomConfig.RoomTagsMax</c>.</summary>
    public required int RoomTagsMax { get; init; }

    /// <summary><c>RoomConfig.RoomTagMaxLength</c>.</summary>
    public required int RoomTagMaxLength { get; init; }

    /// <summary><c>PlayerConfig.MottoMaxLength</c>.</summary>
    public required int MottoMaxLength { get; init; }

    /// <summary><c>PlayerConfig.MessengerMaxMessageLength</c>.</summary>
    public required int MessengerMessageMaxLength { get; init; }

    /// <summary><c>WiredConfig.SelectedItemsLimit</c>: the furni a box may pick.</summary>
    public required int WiredSelectedItemsMax { get; init; }

    /// <summary><c>WiredConfig.ActionDelayMaxPulses</c>.</summary>
    public required int WiredActionDelayMaxPulses { get; init; }

    /// <summary><c>InventoryConfig.MaxActiveBadges</c>: the badge slots a player wears.</summary>
    public required int ActiveBadgesMax { get; init; }

    /// <summary><c>GiftWrappingConfig.BoxTypes</c>: the boxes a present may be wrapped in.</summary>
    public required IReadOnlySet<int> GiftBoxTypes { get; init; }

    /// <summary><c>GiftWrappingConfig.RibbonTypes</c>.</summary>
    public required IReadOnlySet<int> GiftRibbonTypes { get; init; }
}
