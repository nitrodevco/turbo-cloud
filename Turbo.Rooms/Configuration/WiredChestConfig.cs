namespace Turbo.Rooms.Configuration;

/// <summary>
/// Wired chests: how much they hold, what buying more costs, and the bounds of the windows that
/// list them. The capacities and costs ship as the client's own defaults (its config keys
/// <c>wired.furni_chest.*</c>, <c>wired.coins_chest.*</c>, <c>wired.chests.*</c>); a hotel that
/// changes one changes the other, or the client shows numbers the server does not keep.
/// </summary>
public class WiredChestConfig
{
    public const string SECTION_NAME = "Turbo:WiredChests";

    /// <summary>What a furni chest holds before any upgrade, in items.</summary>
    public int FurniInitialCapacity { get; init; } = 1000;

    /// <summary>Items each capacity upgrade adds to a furni chest.</summary>
    public int FurniUpgradeCapacity { get; init; } = 1000;

    public int FurniMaxUpgrades { get; init; } = 9;

    /// <summary>What a starter furni chest holds; a starter chest is never upgraded.</summary>
    public int FurniStarterCapacity { get; init; } = 100;

    /// <summary>What a credit chest holds before any upgrade, in credits.</summary>
    public int CoinsInitialCapacity { get; init; } = 5000;

    public int CoinsUpgradeCapacity { get; init; } = 5000;

    public int CoinsMaxUpgrades { get; init; } = 19;

    public int CoinsStarterCapacity { get; init; } = 500;

    /// <summary>The part of a chest's furni name that marks it a starter chest.</summary>
    public string StarterInfix { get; init; } = "_starter";

    /// <summary>Credits one capacity upgrade costs.</summary>
    public int UpgradeCostCredits { get; init; } = 10;

    /// <summary>Diamonds one capacity upgrade costs.</summary>
    public int UpgradeCostDiamonds { get; init; } = 10;

    /// <summary>The activity point type diamonds are kept under.</summary>
    public int DiamondsActivityPointType { get; init; } = 5;

    /// <summary>
    /// Whether chest settings whose name or description the word filter would change are refused
    /// with Habbo's <c>wiredfurni.error.unacceptable_chest_name</c> / <c>unacceptable_chest_desc</c>
    /// (when the hotel sends them is inferred from the texts). Off saves them filtered.
    /// </summary>
    public bool RefuseUnacceptableText { get; init; } = true;

    public int NameMaxLength { get; init; } = 30;
    public int DescriptionMaxLength { get; init; } = 200;

    /// <summary>The most items a chest can show above itself when open.</summary>
    public int MaxPreviewItems { get; init; } = 4;

    /// <summary>Items per contents packet when a furni chest is opened.</summary>
    public int ContentsFragmentSize { get; init; } = 500;

    /// <summary>Players one chest keeps up to date while they look inside; more are refused.</summary>
    public int MaxViewersPerChest { get; init; } = 50;

    /// <summary>Items one deposit may offer.</summary>
    public int MaxItemsPerDeposit { get; init; } = 500;

    /// <summary>
    /// Whether an item a deposit's offer would take past the chest's free capacity is refused
    /// while offering, with Habbo's <c>wired_transactions.notification.trade_error.9</c>. Off,
    /// the offer takes it and the deposit fails only when it is confirmed.
    /// </summary>
    public bool RefuseOfferOverCapacity { get; init; } = true;

    /// <summary>The most transactions one page of the logs window lists.</summary>
    public int MaxLogPageSize { get; init; } = 50;

    /// <summary>
    /// Transactions one chest's log keeps; each new one past it drops the oldest. The hotel's
    /// <c>wired.chests_max_logs</c>, which the client does not read. Zero or less keeps them all.
    /// </summary>
    public int MaxLogsPerChest { get; init; } = 2000;

    /// <summary>Item types one log details window lists before it says there are more.</summary>
    public int MaxLogDetailItemTypes { get; init; } = 100;

    /// <summary>
    /// Whether a chest's owner gets the notifications they ticked in its notification settings
    /// (<c>wired_chests.*</c> bubbles, kept for them while offline). Off, the settings are only stored.
    /// </summary>
    public bool NotificationsEnabled { get; init; } = true;

    /// <summary>
    /// The link a chest notification carries, <c>{0}</c> the chest's room id: clicking the bubble
    /// goes to the chest's room (<c>wiredchests.notification_settings.notification_info.desc</c>).
    /// The client strips <c>event:</c> and opens the rest as a client link
    /// (HabboNotifications.showNotification); the room link is the one the client's own room
    /// links use (<c>navigator/goto/&lt;id&gt;</c>, FurnitureRoomLinkHandler). Empty sends none.
    /// </summary>
    public string NotificationLink { get; init; } = "event:navigator/goto/{0}";

    // A contract, as its editor bounds it.

    /// <summary>Payment options a contract may offer.</summary>
    public int MaxContractRules { get; init; } = 3;

    /// <summary>Elements in one option, or in what a contract gives.</summary>
    public int MaxContractNodes { get; init; } = 5;

    public int MaxContractCoins { get; init; } = 100000;
    public int MaxContractFurni { get; init; } = 500;
    public int ContractReceiveTextMaxLength { get; init; } = 60;
    public int ContractRewardTextMaxLength { get; init; } = 200;

    /// <summary>The most times one transaction may repeat its contract.</summary>
    public int MaxTransactionMultiplier { get; init; } = 500;

    /// <summary>
    /// How long a trade wired cancelled stays open in case the same stack opens a new one, which
    /// then replaces it without the window closing.
    /// </summary>
    public int CancelGraceMs { get; init; } = 500;

    /// <summary>
    /// How long an open trade with wired keeps the player's trade grain loaded past its last
    /// action (at least to a contract's timeout). It is memory only, so a grain collected for
    /// idleness while the window is open would forget it.
    /// </summary>
    public int TradeKeepAliveMinutes { get; init; } = 30;

    /// <summary>
    /// Whether a final confirm sent before accepting ends the trade as invalid (nothing moves),
    /// as official does; otherwise it is ignored and the trade stays open.
    /// </summary>
    public bool CancelUnacceptedConfirm { get; init; } = true;
}
