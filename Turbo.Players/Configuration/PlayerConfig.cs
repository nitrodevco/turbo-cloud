namespace Turbo.Players.Configuration;

/// <summary>
/// Player tunables. Every option carries the hotel default it ships with, so a section left out
/// of <c>appsettings.json</c> still starts; the keys in that file are the ones a hotel is
/// expected to change.
/// </summary>
public class PlayerConfig
{
    public const string SECTION_NAME = "Turbo:Players";

    public int PlayerPresenceTickMs { get; init; } = 5000;
    public int MessengerUserFriendLimit { get; init; } = 100;
    public int MessengerNormalFriendLimit { get; init; } = 100;
    public int MessengerExtendedFriendLimit { get; init; } = 100;
    public int MessengerSearchLimit { get; init; } = 25;
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

    /// <summary>Items per fragment when the furniture inventory is sent to the client.</summary>
    public int FurnitureInventoryFragmentSize { get; init; } = 100;

    /// <summary>Friends per fragment when the friend list is sent to the client.</summary>
    public int FriendListFragmentSize { get; init; } = 100;

    /// <summary>Pets per fragment when the pet inventory is sent to the client.</summary>
    public int PetInventoryFragmentSize { get; init; } = 100;

    /// <summary>Badges per fragment when the badge inventory is sent to the client.</summary>
    public int BadgeInventoryFragmentSize { get; init; } = 500;
    public int NavigatorFlushMs { get; init; } = 5000;

    /// <summary>Distinct rooms kept in memory for a player's visit history.</summary>
    public int NavigatorHistoryRooms { get; init; } = 200;

    /// <summary>Room visits buffered between flushes before the oldest are dropped.</summary>
    public int NavigatorMaxPendingVisits { get; init; } = 100;

    /// <summary>Consecutive failed writes before buffered room visits are given up.</summary>
    public int NavigatorVisitWriteAttempts { get; init; } = 5;
}
