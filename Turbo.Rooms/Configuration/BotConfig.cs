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

    /// <summary>Chat bubble style bots speak with.</summary>
    public int ChatStyleId { get; init; } = 2;

    /// <summary>Skills every player-owned bot offers in its menu.</summary>
    public BotSkillType[] Skills { get; init; } =
    [
        BotSkillType.DressUp,
        BotSkillType.SetupChat,
        BotSkillType.RandomWalk,
        BotSkillType.Dance,
        BotSkillType.ChangeName,
    ];
}
