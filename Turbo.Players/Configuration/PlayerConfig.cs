namespace Turbo.Players.Configuration;

/// <summary>
/// Player tunables. Every option carries the hotel default it ships with, so a section left out
/// of <c>appsettings.json</c> still starts; the keys in that file are the ones a hotel is
/// expected to change.
/// </summary>
public class PlayerConfig
{
    public const string SECTION_NAME = "Turbo:Players";

    /// <summary>Maximum wait for a best-effort player notice, in milliseconds.</summary>
    public int NoticeTimeoutMs { get; init; } = 5000;

    /// <summary>Habbo Club and Builders Club membership tunables.</summary>
    public SubscriptionConfig Subscriptions { get; init; } = new();

    /// <summary>Permission audit and expiry tunables.</summary>
    public PermissionConfig Permissions { get; init; } = new();

    /// <summary>
    /// How long a player's grain is kept from being collected while it holds a temporary look,
    /// in minutes. The look lives only in memory, so a collected grain would lose it mid-session;
    /// the session ending clears it and releases the grain sooner.
    /// </summary>
    public int LookOverrideKeepAliveMinutes { get; init; } = 1440;

    public int PlayerPresenceTickMs { get; init; } = 5000;

    /// <summary>Durable presence interval in seconds; a crash credits only committed intervals.</summary>
    public int AchievementOnlineIntervalSeconds { get; init; } = 30;

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

    /// <summary>
    /// Longest console message kept, in characters; longer text is cut before it is stored and
    /// delivered.
    /// </summary>
    public int MessengerMaxMessageLength { get; init; } = 255;

    /// <summary>
    /// Messages returned per console history request (opening or scrolling back a
    /// conversation). 0, the default, answers none, as Habbo did: a conversation opens empty
    /// and only messages that arrived while the player was offline are shown, at login. Any
    /// other value pages through the stored conversation.
    /// </summary>
    public int MessengerHistoryPageSize { get; init; } = 0;

    /// <summary>
    /// Undelivered console messages replayed when their recipient's messenger starts, newest
    /// first cut; older undelivered ones are marked delivered with them and stay in history.
    /// </summary>
    public int MessengerOfflineReplayLimit { get; init; } = 50;

    /// <summary>Longest room invitation text; the client's own input stops at 120.</summary>
    public int MessengerRoomInviteMaxLength { get; init; } = 120;

    /// <summary>Friends one room invitation is sent to at most; the rest are reported as failed.</summary>
    public int MessengerRoomInviteMaxRecipients { get; init; } = 100;

    /// <summary>
    /// Shortest time between two room invitations by one player. The client holds its own
    /// minute between invitations; this keeps a client that does not from flooding friends.
    /// </summary>
    public int MessengerRoomInviteMinIntervalMs { get; init; } = 30000;

    /// <summary>
    /// Lists each of a player's groups in their friend list as a group chat, which every online
    /// member can talk in through the messenger. Group chat lines are not stored.
    /// </summary>
    public bool MessengerGroupChatEnabled { get; init; } = true;

    /// <summary>
    /// How often an online player's messenger joins its group chats again, which is what puts
    /// them back in a group's chat once that group was loaded afresh (a silo restart).
    /// </summary>
    public int MessengerGroupChatRejoinMs { get; init; } = 60000;

    /// <summary>
    /// Console messages, direct and group together, one player may send per
    /// <see cref="MessengerMessageWindowMs"/>; past it a message is refused with the client's
    /// "failed to send" text.
    /// </summary>
    public int MessengerMessagesPerWindow { get; init; } = 10;

    /// <summary>The window <see cref="MessengerMessagesPerWindow"/> counts over.</summary>
    public int MessengerMessageWindowMs { get; init; } = 5000;
    public int WardrobeMaxSlots { get; init; } = 10;

    /// <summary>Respects a player may give per day; resets at UTC midnight.</summary>
    public int MaxRespectPerDay { get; init; } = 3;
    public int MaxPetRespectPerDay { get; init; } = 3;

    /// <summary>Times per day a player may refill their respects (0 disables the option).</summary>
    public int RespectReplenishesPerDay { get; init; } = 0;
    public int SettingsFlushMs { get; init; } = 5000;

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
