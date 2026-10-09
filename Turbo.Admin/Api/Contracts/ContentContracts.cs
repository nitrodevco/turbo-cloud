using System.Collections.Generic;
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

/// <summary>A room category of the navigator, with how many rooms are in it.</summary>
public sealed record NavigatorFlatCategoryItem(
    int Id,
    string Name,
    bool Visible,
    bool StaffOnly,
    int MinRank,
    string? RequiredNode,
    int OrderNum,
    bool Automatic,
    string? AutomaticCategory,
    string? GlobalCategory,
    int Rooms
);

/// <summary>An event category of the navigator, with how many events are in it.</summary>
public sealed record NavigatorEventCategoryItem(int Id, string Name, bool Visible, int Events);

/// <summary>A tab along the navigator's top, by its search code.</summary>
public sealed record NavigatorContextItem(int Id, string SearchCode, bool Visible, int OrderNum);

public sealed record NavigatorContentResponse(
    List<NavigatorFlatCategoryItem> FlatCategories,
    List<NavigatorEventCategoryItem> EventCategories,
    List<NavigatorContextItem> Contexts
);

/// <summary>A room category as staff write it; a field left out keeps what it was.</summary>
public sealed record NavigatorFlatCategoryRequest(
    string? Name,
    bool? Visible,
    bool? StaffOnly,
    int? MinRank,
    string? RequiredNode,
    int? OrderNum,
    bool? Automatic,
    string? AutomaticCategory,
    string? GlobalCategory
);

public sealed record NavigatorEventCategoryRequest(string? Name, bool? Visible);

public sealed record NavigatorContextRequest(string? SearchCode, bool? Visible, int? OrderNum);
