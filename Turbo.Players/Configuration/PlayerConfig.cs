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
    public required int AchievementProgressFlushTickMs { get; init; } = 10000;
    public required int MaxDirtyAchievementsPerFlush { get; init; } = 100;
}
