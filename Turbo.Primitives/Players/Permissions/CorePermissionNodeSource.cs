using System.Collections.Generic;
using Turbo.Primitives.Players.Enums;

namespace Turbo.Primitives.Players.Permissions;

/// <summary>
/// Registers every node in <see cref="PermissionNodes"/> and every key in
/// <see cref="PermissionMetaKeys"/>. The client levels are the <c>hasSecurity(n)</c> thresholds
/// the Flash client gates the same feature on (<c>docs/permissions-client-gates.md</c>, and
/// <c>docs/permissions.md</c> §17 for the ones found later), and the perks are the codes it reads
/// (<c>docs/permissions-client-perks.md</c>). The perk refusals are the texts the server sent
/// before permissions existed.
/// </summary>
public sealed class CorePermissionNodeSource : IPermissionNodeSource
{
    public string? Prefix => null;

    public IEnumerable<PermissionNodeDefinition> Nodes { get; } =
    [
        new(
            PermissionNodes.TRADE,
            "Trade with other players.",
            Perk: PlayerPerkFlags.Trade,
            PerkRefusal: "requirement.unfulfilled.citizenship_level_3"
        ),
        new(
            PermissionNodes.Room.CONTROL_ANY,
            "Control every room as its owner.",
            SecurityLevelType.Moderator
        ),
        new(
            PermissionNodes.Room.FURNI_STEAL,
            "Pick up somebody else's furni into their own inventory."
        ),
        new(
            PermissionNodes.Room.FURNI_PICKUP_ANY,
            "Pick up anyone's furni from the info stand.",
            SecurityLevelType.Employee
        ),
        new(
            PermissionNodes.Room.ENTER_LOCKED,
            "Enter a room past its doorbell or password.",
            SecurityLevelType.Moderator
        ),
        new(PermissionNodes.Room.ENTER_FULL, "Enter a room that is at capacity."),
        new(PermissionNodes.Room.ENTER_HIDDEN, "Enter a room hidden by Builders Club."),
        new(
            PermissionNodes.Room.MODERATE_ANY,
            "Kick, mute and ban in any room, whatever its moderation settings.",
            SecurityLevelType.Employee
        ),
        new(
            PermissionNodes.Room.FLOORPLAN_SAVE_WITHOUT_CLUB,
            "Save a floor plan without a Builders Club membership.",
            SecurityLevelType.Employee
        ),
        new(
            PermissionNodes.Room.FLOORPLAN_LARGE,
            "Save floor plans past the standard area limit.",
            Perk: PlayerPerkFlags.BuilderAtWork,
            PerkRefusal: "requirement.unfulfilled.group_membership"
        ),
        new(
            PermissionNodes.Room.EVENT_EDIT_ANY,
            "Edit or end the event of any room.",
            SecurityLevelType.Moderator
        ),
        new(
            PermissionNodes.Room.FURNI_RENT_CANCEL_ANY,
            "Cancel anyone's rent of a rentable space.",
            SecurityLevelType.Moderator
        ),
        new(
            PermissionNodes.Room.FURNI_BRANDING,
            "Save the branding of ad furni.",
            SecurityLevelType.Employee
        ),
        new(
            PermissionNodes.Room.FURNI_CUSTOM_VARIABLES,
            "Edit a furni's custom variables from the info stand.",
            SecurityLevelType.Moderator
        ),
        new(
            PermissionNodes.Room.FURNI_YOUTUBE_ANY,
            "Control a YouTube display the player does not own.",
            SecurityLevelType.Employee
        ),
        new(
            PermissionNodes.Room.FURNI_VIMEO_EDIT,
            "Set the video of a Vimeo display.",
            SecurityLevelType.Moderator
        ),
        new(
            PermissionNodes.Catalog.BUILDERS_CLUB_WITHOUT_MEMBERSHIP,
            "Open the Builders Club catalog without a membership.",
            SecurityLevelType.Moderator
        ),
        new(
            PermissionNodes.Catalog.GUILD_ANY_GROUP,
            "Buy group furni and forums for any group, not only their own.",
            SecurityLevelType.Employee
        ),
        new(
            PermissionNodes.Catalog.GIFT_HIDE_SENDER,
            "Send a gift without their face on it.",
            SecurityLevelType.Moderator
        ),
        new(
            PermissionNodes.Navigator.CATEGORY_STAFF,
            "See and use staff-only navigator categories.",
            SecurityLevelType.Community
        ),
        new(
            PermissionNodes.Navigator.STAFF_PICK,
            "Mark rooms as staff picks.",
            SecurityLevelType.Community
        ),
        new(
            PermissionNodes.Moderation.TOOL,
            "Use the moderation tool.",
            SecurityLevelType.Moderator
        ),
        new(PermissionNodes.Wired.MENU, "Open the wired menu.", SecurityLevelType.Employee),
        new(
            PermissionNodes.Guild.DELETE_ANY,
            "Delete any group, not only their own.",
            SecurityLevelType.Moderator
        ),
        new(PermissionNodes.Chat.SPEAK, "Speak at all. Denied by a hotel mute."),
        new(
            PermissionNodes.Chat.FURNI_CHOOSER,
            "Use the :furni chooser in any room.",
            SecurityLevelType.Partner
        ),
        new(
            PermissionNodes.Chat.STYLE_STAFF,
            "Speak with staff chat bubbles.",
            SecurityLevelType.Employee
        ),
        new(PermissionNodes.Role.AMBASSADOR, "Be an ambassador."),
        new(PermissionNodes.Permissions.MANAGE, "Edit groups and other players' permissions."),
        new(PermissionNodes.Perk.CAMERA, "Use the camera.", Perk: PlayerPerkFlags.Camera),
        new(
            PermissionNodes.Perk.MOUSE_ZOOM,
            "Zoom the room with the mouse wheel.",
            Perk: PlayerPerkFlags.MouseZoom
        ),
        new(
            PermissionNodes.Perk.CITIZEN,
            "Be shown the helper talent track rather than citizenship.",
            Perk: PlayerPerkFlags.Citizen
        ),
        new(
            PermissionNodes.Perk.NAVIGATOR_THUMBNAIL_CAMERA,
            "Take room thumbnails and see thumbnail views in the navigator.",
            Perk: PlayerPerkFlags.NavigatorRoomThumbnailCamera
        ),
        new(
            PermissionNodes.Perk.NAVIGATOR_PHASE_ONE,
            "Run the phase-one 2014 navigator.",
            Perk: PlayerPerkFlags.NavigatorPhaseOne2014
        ),
        new(
            PermissionNodes.Perk.NAVIGATOR_PHASE_TWO,
            "Run the phase-two 2014 navigator.",
            Perk: PlayerPerkFlags.NavigatorPhaseTwo2014
        ),
        new(
            PermissionNodes.Perk.GUIDE_TOOL,
            "Go on duty with the guide tool.",
            Perk: PlayerPerkFlags.UseGuideTool,
            PerkRefusal: "requirement.unfulfilled.helper_level_4"
        ),
        new(
            PermissionNodes.Perk.JUDGE_CHAT_REVIEWS,
            "Take chat review duty in the guide tool.",
            Perk: PlayerPerkFlags.JudgeChatReviews,
            PerkRefusal: "requirement.unfulfilled.helper_level_6"
        ),
        new(
            PermissionNodes.Perk.CALL_ON_HELPERS,
            "Call on helpers.",
            Perk: PlayerPerkFlags.CallOnHelpers
        ),
        new(
            PermissionNodes.Perk.VOTE_IN_COMPETITIONS,
            "Vote in competitions.",
            Perk: PlayerPerkFlags.VoteInCompetitions,
            PerkRefusal: "requirement.unfulfilled.helper_level_2"
        ),
        new(
            PermissionNodes.Perk.HABBO_CLUB_OFFER_BETA,
            "See the Habbo Club offer beta.",
            Perk: PlayerPerkFlags.HabboClubOfferBeta
        ),
        new(
            PermissionNodes.Perk.NO_VIDEO_OFFERS,
            "Not be shown video offers.",
            SecurityLevelType.Celebrity
        ),
    ];

    public IEnumerable<PermissionMetaDefinition> MetaKeys { get; } =
    [
        new(
            PermissionMetaKeys.Client.SECURITY_LEVEL,
            "A minimum security level sent to the client, over the one the player's nodes give.",
            PermissionMetaSelectionType.HighestNumber
        ),
        new(
            PermissionMetaKeys.Limit.FRIENDS,
            "Friends the player may have, in place of the hotel's default.",
            PermissionMetaSelectionType.HighestNumber
        ),
        new(
            PermissionMetaKeys.Limit.ROOMS,
            "Rooms the player may own, in place of the hotel's default.",
            PermissionMetaSelectionType.HighestNumber
        ),
        new(
            PermissionMetaKeys.Limit.FAVOURITE_ROOMS,
            "Favourite rooms the player may keep, in place of the hotel's default.",
            PermissionMetaSelectionType.HighestNumber
        ),
    ];
}
