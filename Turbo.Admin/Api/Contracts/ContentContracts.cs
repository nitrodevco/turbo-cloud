using Turbo.Primitives.Achievements.Enums;
using Turbo.Primitives.Badges.Enums;

namespace Turbo.Admin.Api.Contracts;

/// <summary>An achievement as the panel lists it.</summary>
public sealed record AchievementItem(
    int Id,
    string Key,
    int Revision,
    string Category,
    string SubCategory,
    int Order,
    AchievementState State,
    string Source,
    int Levels,
    string? FirstBadge,
    string? LastBadge
);

/// <summary>
/// An achievement definition to check or publish, as the JSON the catalog keeps (its own field
/// names), and why: a publish is on record with its reason.
/// </summary>
public sealed record AchievementPublishRequest(string? DefinitionJson, string? Reason);

/// <summary>An achievement's state to change to, and why.</summary>
public sealed record AchievementStateRequest(AchievementState? State, string? Reason);

/// <summary>A badge: how many players hold it, and the rarity pinned for it, if any.</summary>
public sealed record BadgeItem(string Code, int Holders, BadgeRarityType? Rarity);

public sealed record BadgeHolderItem(int PlayerId, string Name, int? Slot);

/// <summary>A badge's rarity to pin; null counts it from how many hold it.</summary>
public sealed record BadgeRarityRequest(BadgeRarityType? Rarity);

/// <summary>The player a badge is given to.</summary>
public sealed record BadgeGiveRequest(int? PlayerId);
