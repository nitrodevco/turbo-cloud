namespace Turbo.Players.Configuration;

public class PlayerConfig
{
    public const string SECTION_NAME = "Turbo:Players";

    public required int PlayerPresenceTickMs { get; init; } = 5000;
    public required int MessengerUserFriendLimit { get; init; } = 100;
    public required int MessengerNormalFriendLimit { get; init; } = 100;
    public required int MessengerExtendedFriendLimit { get; init; } = 100;
    public required int MessengerSearchLimit { get; init; } = 25;
    public required int MessengerMaxIgnore { get; init; } = 100;
    public required int MaxSessionMessagesPerConversation { get; init; } = 20;
    public required int WardrobeMaxSlots { get; init; } = 10;
    public required int SettingsFlushMs { get; init; } = 5000;

    /// <summary>How often delivered-message flags are written back to the database.</summary>
    public required int MessengerDeliveredFlushMs { get; init; } = 5000;

    /// <summary>Delivered-message flags buffered between flushes before the oldest are dropped.</summary>
    public required int MessengerMaxPendingDelivered { get; init; } = 500;

    public required int MaxPendingComposers { get; init; } = 500;

    /// <summary>Items per fragment when the furniture inventory is sent to the client.</summary>
    public required int FurnitureInventoryFragmentSize { get; init; } = 100;

    /// <summary>Friends per fragment when the friend list is sent to the client.</summary>
    public required int FriendListFragmentSize { get; init; } = 100;
    public required int NavigatorFlushMs { get; init; } = 5000;

    /// <summary>Distinct rooms kept in memory for a player's visit history.</summary>
    public required int NavigatorHistoryRooms { get; init; } = 200;

    /// <summary>Room visits buffered between flushes before the oldest are dropped.</summary>
    public required int NavigatorMaxPendingVisits { get; init; } = 100;

    /// <summary>Consecutive failed writes before buffered room visits are given up.</summary>
    public required int NavigatorVisitWriteAttempts { get; init; } = 5;
}
