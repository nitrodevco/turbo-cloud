using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using Turbo.Primitives.Achievements.Enums;
using Turbo.Primitives.Badges.Enums;
using Turbo.Primitives.Gamedata.Snapshots;
using Turbo.Primitives.Guilds.Enums;
using Turbo.Primitives.Guilds.Forums.Enums;

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

/// <summary>A room in a Public tab category: who owns it, and who put it there when.</summary>
public sealed record NavigatorPublicRoomItem(
    int RoomId,
    string Name,
    int OwnerId,
    string OwnerName,
    DateTime AddedAt,
    int? AddedBy
);

/// <summary>
/// A category of the navigator's Public tab with its rooms in their order. <c>kind</c> is
/// <c>manual</c> (staff put rooms in it) or <c>staffpicks</c> (the staff-picked rooms, newest
/// first); <c>viewMode</c> 0 is a list, 1 thumbnails; <c>pluginKey</c> names the plugin that made it.
/// </summary>
public sealed record NavigatorPublicCategoryItem(
    int Id,
    string Name,
    int OrderNum,
    bool ForceClosed,
    int ViewMode,
    bool Visible,
    string Kind,
    string? PluginKey,
    string SearchCode,
    List<NavigatorPublicRoomItem> Rooms
);

/// <summary>The Public tab's categories, and whether the viewer may change them (<c>content.manage</c> and <c>navigator.public.manage</c>).</summary>
public sealed record NavigatorPublicResponse(
    List<NavigatorPublicCategoryItem> Categories,
    bool CanManage
);

/// <summary>A room as the Public tab lists it now: its users now, its door (0 open, 1 doorbell, 2 password, 3 invisible).</summary>
public sealed record NavigatorPublicPreviewRoom(
    int RoomId,
    string Name,
    string OwnerName,
    int Users,
    int MaxUsers,
    int DoorMode
);

/// <summary>One block of the Public tab as players get it now: its code, title, forced close, view mode (0 list, 1 thumbnails) and rooms in order.</summary>
public sealed record NavigatorPublicPreviewBlock(
    string SearchCode,
    string Text,
    bool ForceClosed,
    int ViewMode,
    List<NavigatorPublicPreviewRoom> Rooms
);

/// <summary>What the navigator's Public tab (<c>official_view</c>) answers now, a plugin's room order included.</summary>
public sealed record NavigatorPublicPreviewResponse(List<NavigatorPublicPreviewBlock> Blocks);

/// <summary>A Public tab category as staff write it; a field left out keeps what it was. <c>kind</c> only on a new one.</summary>
public sealed record NavigatorPublicCategoryRequest(
    string? Name,
    bool? Visible,
    bool? ForceClosed,
    int? ViewMode,
    string? Kind
);

/// <summary>Every category, or every room of a category, once, in the new order.</summary>
public sealed record NavigatorPublicOrderRequest(List<int>? Ids);

/// <summary>A room to put in a category, first (the default) or last.</summary>
public sealed record NavigatorPublicRoomRequest(int? RoomId, bool? First);

/// <summary>The category a room moves to, and where in it (0 = first, the default).</summary>
public sealed record NavigatorPublicMoveRequest(int? ToCategoryId, int? Index);

/// <summary>A group as staff find it: its owner, homeroom and how many members it has.</summary>
public sealed record GroupItem(
    int Id,
    string Name,
    string BadgeCode,
    int OwnerId,
    string OwnerName,
    int RoomId,
    string RoomName,
    GuildType Type,
    int Members,
    DateTime CreatedAt
);

public sealed record GroupSearchResponse(
    List<GroupItem> Groups,
    int Total,
    int PageSize,
    ListOrder Order
);

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
    List<GroupMemberItem> Members,
    GroupForumItem? Forum
);

/// <summary>
/// A group's forum: who may read it, post in it, start threads and moderate it, and how busy it
/// is. A group has one once its owner bought a forum terminal for it.
/// </summary>
public sealed record GroupForumItem(
    GuildForumPermission ReadPermission,
    GuildForumPermission PostMessagePermission,
    GuildForumPermission PostThreadPermission,
    GuildForumPermission ModeratePermission,
    int ThreadCount,
    int MessageCount,
    DateTime? LastMessageAt
);

/// <summary>A forum's settings as staff set them, each a <c>GuildForumPermission</c>.</summary>
public sealed record GroupForumRequest(
    int ReadPermission,
    int PostMessagePermission,
    int PostThreadPermission,
    int ModeratePermission
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

/// <summary>
/// A currency type: its name (as <c>:give</c> takes it), its type (<c>credits</c>, <c>silver</c>,
/// <c>emeralds</c>, <c>activity_points</c>) and activity point number, whether it is on, and what
/// uses it: players holding it, catalog offers priced in it, vouchers giving it.
/// </summary>
public sealed record CurrencyItem(
    int Id,
    string Name,
    string Type,
    int? ActivityPointType,
    bool Enabled,
    int Holders,
    int Offers,
    int Vouchers
);

public sealed record CurrencyListResponse(List<CurrencyItem> Items);

/// <summary>A currency to add or change; leaving <c>Enabled</c> out keeps it as it was (on, for a new one).</summary>
public sealed record CurrencyRequest(
    string? Name,
    string? Type,
    int? ActivityPointType,
    bool? Enabled
);

/// <summary>
/// A chat bubble style whole: the id the client draws it by and its asset id, who may speak with
/// it, how the client draws it (its reg points and bitmaps, as chat-style-definitions.json has
/// them), and how many own it and have it as their bubble. <c>Drawable</c> says whether the
/// bundle carries it: it needs reg points and its three bitmaps. <c>FromClient</c> says its look is
/// Habbo's HTML client's, <c>ChangedHere</c> that it was changed since it was taken from it.
/// </summary>
public sealed record ChatStyleItem(
    int Id,
    int ClientStyleId,
    string AssetId,
    bool ClubOnly,
    bool AmbassadorOnly,
    bool StaffOnly,
    bool Purchasable,
    bool System,
    bool StaffOverrideable,
    bool AllowHtml,
    bool Notification,
    JsonObject? RegPoints,
    string[] Bitmaps,
    int SortOrder,
    int Owners,
    int Preferring,
    bool Drawable,
    bool FromClient,
    bool ChangedHere
)
{
    public static ChatStyleItem From(ChatStyleDefinitionSnapshot style) =>
        new(
            style.Id,
            style.ClientStyleId,
            style.AssetId,
            style.ClubOnly,
            style.AmbassadorOnly,
            style.StaffOnly,
            style.Purchasable,
            style.System,
            style.StaffOverrideable,
            style.AllowHtml,
            style.Notification,
            style.RegPoints is null ? null : JsonNode.Parse(style.RegPoints) as JsonObject,
            [.. style.Bitmaps],
            style.SortOrder,
            style.Owners,
            style.Preferring,
            style.Drawable,
            style.FromClient,
            style.ChangedHere
        );
}

/// <summary>Every chat style in the client's order, the id a new one takes, and the bundle built from them.</summary>
public sealed record ChatStyleListResponse(
    List<ChatStyleItem> Items,
    int NextId,
    AssetBundleResponse? Bundle,
    int LibraryBitmaps
);

/// <summary>
/// A chat style to add or change; anything left out keeps what it was (off, for a new one).
/// <c>Files</c> sets bitmaps by file as base64 PNG, or removes one with null; a new style starts
/// with the bitmaps of <c>CopyFrom</c> (a style's id) that it doesn't set.
/// </summary>
public sealed record ChatStyleRequest(
    int? ClientStyleId,
    string? AssetId,
    bool? ClubOnly,
    bool? AmbassadorOnly,
    bool? StaffOnly,
    bool? Purchasable,
    bool? System,
    bool? StaffOverrideable,
    bool? AllowHtml,
    bool? Notification,
    JsonObject? RegPoints,
    Dictionary<string, string?>? Files,
    int? CopyFrom
)
{
    /// <summary>The request as the service takes it; a file that isn't base64 is refused.</summary>
    public ChatStyleEdit ToEdit() =>
        new()
        {
            ClientStyleId = ClientStyleId,
            AssetId = AssetId,
            ClubOnly = ClubOnly,
            AmbassadorOnly = AmbassadorOnly,
            StaffOnly = StaffOnly,
            Purchasable = Purchasable,
            System = System,
            StaffOverrideable = StaffOverrideable,
            AllowHtml = AllowHtml,
            Notification = Notification,
            RegPoints = RegPoints?.ToJsonString(),
            Files = Files?.ToDictionary(
                x => x.Key,
                x => x.Value is null ? null : Decode(x.Key, x.Value)
            ),
            CopyFrom = CopyFrom,
        };

    private static byte[] Decode(string file, string base64)
    {
        try
        {
            return Convert.FromBase64String(base64);
        }
        catch (FormatException ex)
        {
            throw new ArgumentException($"{file} is not base64.", ex);
        }
    }
}
