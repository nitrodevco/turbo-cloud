using System;

namespace Turbo.Admin.Api.Contracts;

/// <summary>
/// A daily task definition. <see cref="QuestType"/> is <c>explore</c> (rooms other players own,
/// each once) or <c>find_furni</c> (using one of <see cref="Target"/>'s furni); the reward is
/// <c>points</c> of the point type <see cref="RewardTypeId"/> names (0 is duckets) or a
/// <c>badge</c> by its code. <see cref="Problem"/> says why it can't be given, when it can't;
/// <see cref="Given"/> is how many players have been given it.
/// </summary>
public sealed record DailyTaskItem(
    int Id,
    string Code,
    string QuestType,
    string Target,
    int RequiredRepeats,
    string ImageVersion,
    string CatalogName,
    bool IsBonus,
    bool Enabled,
    string RewardKind,
    string RewardTypeId,
    int RewardAmount,
    string? Problem,
    int Given
);

/// <summary>The daily task definitions, whether the viewer may change them, and the kinds the server counts.</summary>
public sealed record DailyTasksResponse(bool CanManage, string[] Types, DailyTaskItem[] Tasks);

/// <summary>A daily task definition to save; <see cref="RewardKind"/> is <c>points</c> or <c>badge</c>.</summary>
public sealed record DailyTaskRequest(
    string Code,
    string QuestType,
    string? Target,
    int RequiredRepeats,
    string? ImageVersion,
    string? CatalogName,
    bool IsBonus,
    bool Enabled,
    string RewardKind,
    string? RewardTypeId,
    int RewardAmount
);

/// <summary>
/// A quest. <see cref="Counted"/> is whether the server counts its <see cref="Type"/>: a quest of
/// another type can be accepted but never moves. <see cref="Started"/> and
/// <see cref="Completed"/> are how many players took it and finished it.
/// </summary>
public sealed record QuestItem(
    int Id,
    int CampaignId,
    string LocalizationCode,
    string Type,
    string Target,
    int TotalSteps,
    int ActivityPointType,
    int RewardAmount,
    int SortOrder,
    string ImageVersion,
    string CatalogPageName,
    string ChainCode,
    bool Easy,
    bool Counted,
    int Started,
    int Completed
);

/// <summary>
/// A quest campaign with its quests in the order they are done. <see cref="EndsAtUtc"/> set makes
/// it seasonal: the quest window shows its time left, and it is gone after.
/// </summary>
public sealed record QuestCampaignItem(
    int Id,
    string Code,
    int SortOrder,
    bool Enabled,
    DateTime? EndsAtUtc,
    QuestItem[] Quests
);

/// <summary>The campaigns, whether the viewer may change them, and the quest types the server counts.</summary>
public sealed record QuestsResponse(
    bool CanManage,
    string[] CountedTypes,
    QuestCampaignItem[] Campaigns
);

public sealed record QuestCampaignRequest(
    string Code,
    int SortOrder,
    bool Enabled,
    DateTime? EndsAtUtc
);

public sealed record QuestRequest(
    int CampaignId,
    string LocalizationCode,
    string Type,
    string? Target,
    int TotalSteps,
    int ActivityPointType,
    int RewardAmount,
    int SortOrder,
    string? ImageVersion,
    string? CatalogPageName,
    string? ChainCode,
    bool Easy
);
