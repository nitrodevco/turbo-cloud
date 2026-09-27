namespace Turbo.Players.Configuration;

/// <summary>
/// Player tunables. Every option carries the hotel default it ships with, so a section left out
/// of <c>appsettings.json</c> still starts; the keys in that file are the ones a hotel is
/// expected to change.
/// </summary>
public class PlayerConfig
{
    public const string SECTION_NAME = "Turbo:Players";

    /// <summary>Habbo Club and Builders Club membership tunables.</summary>
    public SubscriptionConfig Subscriptions { get; init; } = new();

    public int PlayerPresenceTickMs { get; init; } = 5000;

    /// <summary>
    /// Players whose id and name the hotel-wide player directory keeps in memory; the least
    /// recently asked-for is dropped past this and read from the database again when needed.
    /// </summary>
    public int DirectoryMaxCachedPlayers { get; init; } = 100_000;
    public int MessengerUserFriendLimit { get; init; } = 100;
    public int MessengerNormalFriendLimit { get; init; } = 100;
    public int MessengerExtendedFriendLimit { get; init; } = 100;
    public int MessengerSearchLimit { get; init; } = 25;

    /// <summary>
    /// Shortest time between two player searches by one player. A search inside it is answered
    /// with no results rather than another query.
    /// </summary>
    public int MessengerSearchMinIntervalMs { get; init; } = 500;

    /// <summary>
    /// How old a messenger's friend rows may be when its owner comes online and they are still
    /// used as loaded. A messenger woken while its owner was offline (someone viewed their
    /// profile) hears no friend updates, so older rows are read again.
    /// </summary>
    public int MessengerFriendRowsFreshSeconds { get; init; } = 30;
    public int MessengerMaxIgnore { get; init; } = 100;
    public int MaxSessionMessagesPerConversation { get; init; } = 20;
    public int WardrobeMaxSlots { get; init; } = 10;

    /// <summary>Respects a player may give per day; resets at UTC midnight.</summary>
    public int MaxRespectPerDay { get; init; } = 3;
    public int MaxPetRespectPerDay { get; init; } = 3;

    /// <summary>Times per day a player may refill their respects (0 disables the option).</summary>
    public int RespectReplenishesPerDay { get; init; } = 0;
    public int SettingsFlushMs { get; init; } = 5000;

    /// <summary>How often delivered-message flags are written back to the database.</summary>
    public int MessengerDeliveredFlushMs { get; init; } = 5000;

    /// <summary>Delivered-message flags buffered between flushes before the oldest are dropped.</summary>
    public int MessengerMaxPendingDelivered { get; init; } = 500;

    public int MaxPendingComposers { get; init; } = 500;

    /// <summary>
    /// How long a forward's pending entry (how a furni said the player will arrive) is honoured.
    /// A teleporter's entry lets the player past the target room's door, so one they never
    /// followed must not wait for them indefinitely.
    /// </summary>
    public int PendingRoomEntryTtlMs { get; init; } = 30000;

    /// <summary>Friends per fragment when the friend list is sent to the client.</summary>
    public int FriendListFragmentSize { get; init; } = 100;

    /// <summary>Badges per fragment when the badge inventory is sent to the client.</summary>
    public int NavigatorFlushMs { get; init; } = 5000;

    /// <summary>Distinct rooms kept in memory for a player's visit history.</summary>
    public int NavigatorHistoryRooms { get; init; } = 200;

    /// <summary>Room visits buffered between flushes before the oldest are dropped.</summary>
    public int NavigatorMaxPendingVisits { get; init; } = 100;

    /// <summary>Consecutive failed writes before buffered room visits are given up.</summary>
    public int NavigatorVisitWriteAttempts { get; init; } = 5;
}
