using System;
using System.Collections.Generic;
using Turbo.Primitives.Achievements.Enums;
using Turbo.Primitives.Badges.Enums;
using Turbo.Primitives.Guilds.Enums;
using Turbo.Primitives.Rooms.Enums;

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

/// <summary>A group as staff find it: its owner, homeroom and how many members it has.</summary>
public sealed record GroupItem(
    int Id,
    string Name,
    string BadgeCode,
    int OwnerId,
    string OwnerName,
    int RoomId,
    int Members,
    DateTime CreatedAt
);

public sealed record GroupSearchResponse(List<GroupItem> Groups, int Total, int PageSize);

/// <summary>A member, admin, request or block of a group, by its rank.</summary>
public sealed record GroupMemberItem(int PlayerId, string Name, GuildMemberRank Rank);

public sealed record GroupDetailResponse(
    int Id,
    string Name,
    string Description,
    string BadgeCode,
    GuildType Type,
    int OwnerId,
    string OwnerName,
    int RoomId,
    string RoomName,
    DateTime CreatedAt,
    List<GroupMemberItem> Members
);

/// <summary>A group's new name and description, as staff typed them.</summary>
public sealed record GroupRenameRequest(string? Name, string? Description);

/// <summary>A badge part group badges are built from: its kind, the id badge codes carry, and its files.</summary>
public sealed record GroupBadgePartItem(
    int Id,
    GuildBadgePartType PartType,
    int PartId,
    string FileName,
    string MaskFileName
);

public sealed record GroupColorItem(int Id, GuildColorSlotType Slot, int ColorId, string Color);

public sealed record GroupEditorResponse(
    List<GroupBadgePartItem> Parts,
    List<GroupColorItem> Colors
);

/// <summary>A badge part to add (its kind) or change (its files).</summary>
public sealed record GroupBadgePartRequest(
    GuildBadgePartType? PartType,
    string? FileName,
    string? MaskFileName
);

/// <summary>A colour to add (its slot) or change (its hex).</summary>
public sealed record GroupColorRequest(GuildColorSlotType? Slot, string? Color);

/// <summary>One palette of a pet type: the body it has, how rare it is, whether the catalog sells it.</summary>
public sealed record PetBreedItem(
    int Id,
    int TypeId,
    int PaletteId,
    int BreedId,
    int RarityLevel,
    bool Sellable,
    bool Rare,
    int ColorTag
);

/// <summary>A line a pet type says; no type for the lines every type without its own says.</summary>
public sealed record PetSpeechItem(int Id, int? TypeId, string Line);

public sealed record PetContentResponse(List<PetBreedItem> Breeds, List<PetSpeechItem> Speech);

/// <summary>A palette to add (its type and palette id) or change; a field left out keeps what it was.</summary>
public sealed record PetBreedRequest(
    int? TypeId,
    int? PaletteId,
    int? BreedId,
    int? RarityLevel,
    bool? Sellable,
    bool? Rare,
    int? ColorTag
);

public sealed record PetSpeechRequest(int? TypeId, string? Line);

/// <summary>A bot, where it is and how it is set.</summary>
public sealed record BotItem(
    int Id,
    string Name,
    string Motto,
    string Figure,
    AvatarGenderType Gender,
    int OwnerId,
    string OwnerName,
    int? RoomId,
    string? RoomName,
    string ChatText,
    bool AutoChat,
    int ChatDelaySeconds,
    bool MixSentences,
    bool FreeRoam,
    AvatarDanceType Dance
);

public sealed record BotSearchResponse(List<BotItem> Bots, int Total, int PageSize);

/// <summary>A placed bot as staff set it.</summary>
public sealed record BotStaffEditRequest(
    string? Name,
    string? Motto,
    string? Figure,
    AvatarGenderType? Gender,
    string? ChatText,
    bool? AutoChat,
    int? ChatDelaySeconds,
    bool? MixSentences,
    bool? FreeRoam,
    AvatarDanceType? Dance
);
