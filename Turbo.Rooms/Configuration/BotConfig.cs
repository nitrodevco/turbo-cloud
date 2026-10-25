using Turbo.Primitives.Bots.Enums;

namespace Turbo.Rooms.Configuration;

/// <summary>Tunables of rentable bots standing in rooms.</summary>
public class BotConfig
{
    public const string SECTION_NAME = "Turbo:Bots";

    public int MaxBotsPerRoom { get; init; } = 10;
    public int NameMinLength { get; init; } = 1;
    public int NameMaxLength { get; init; } = 32;
    public int ChatTextMaxLength { get; init; } = 2000;
    public int MaxChatLines { get; init; } = 30;
    public int ChatDelayMinSeconds { get; init; } = 5;
    public int ChatDelayMaxSeconds { get; init; } = 300;

    /// <summary>How many tiles behind the avatar it follows a bot stays.</summary>
    public int FollowDistance { get; init; } = 1;
    public int FreeRoamMinIntervalMs { get; init; } = 5000;
    public int FreeRoamMaxIntervalMs { get; init; } = 15000;
    public int FreeRoamMaxDistance { get; init; } = 6;

    /// <summary>
    /// Chat bubble style bots speak with: 31 on Habbo, for every bot Chat, Shout and Whisper and
    /// the placement greeting (official client capture 2026-10-24, W8 labs, 100+ packets).
    /// </summary>
    public int ChatStyleId { get; init; } = 31;

    /// <summary>
    /// Skills every player-owned bot lists in <c>Users</c>, in Habbo's order: the five its menu
    /// offers, then 19-23 (official client capture: all 133 recorded player-bot entries, e.g.
    /// 2026-10-13T14-17-23-room-wait).
    /// </summary>
    public BotSkillType[] Skills { get; init; } =
    [
        BotSkillType.DressUp,
        BotSkillType.SetupChat,
        BotSkillType.RandomWalk,
        BotSkillType.Dance,
        BotSkillType.ChangeName,
        BotSkillType.Unnamed19,
        BotSkillType.Unnamed20,
        BotSkillType.Unnamed21,
        BotSkillType.Unnamed22,
        BotSkillType.Unnamed23,
    ];
}
