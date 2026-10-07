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
            SecurityLevelType.Moderator
        ),
        new(PermissionNodes.Room.ENTER_LOCKED, "Enter a room past its doorbell or password."),
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
            PermissionNodes.Command.COMMANDS,
            "Use :commands to list the chat commands you may use.",
            GrantedByDefault: true
        ),
        new(
            PermissionNodes.Command.KICK,
            "Use :kick. The room's own rules still decide who may kick whom.",
            GrantedByDefault: true
        ),
        new(
            PermissionNodes.Command.LOG,
            "Have every chat command you use written to the command log."
        ),
        new(
            PermissionNodes.Command.MUTE,
            "Use :mute. The room's own rules still decide who may mute whom.",
            GrantedByDefault: true
        ),
        new(
            PermissionNodes.Command.CONFIRM,
            "Use :confirm to go ahead with a command that asked to be confirmed.",
            GrantedByDefault: true
        ),
        new(
            PermissionNodes.Command.BAN,
            "Use :ban to keep a player out of the hotel for a time, or for good."
        ),
        new(PermissionNodes.Command.UNBAN, "Use :unban to lift a hotel ban."),
        new(
            PermissionNodes.Command.SILENCE,
            "Use :silence to stop a player speaking anywhere in the hotel for a time."
        ),
        new(
            PermissionNodes.Command.TRADELOCK,
            "Use :tradelock to stop a player trading for a time."
        ),
        new(
            PermissionNodes.Command.DISCONNECT,
            "Use :disconnect to close a player's connection to the hotel."
        ),
        new(PermissionNodes.Command.WARN, "Use :warn to send a player a moderator's message."),
        new(PermissionNodes.Command.ALERT, "Use :alert to send a player a pop-up message."),
        new(
            PermissionNodes.Command.ALERT_MASS,
            "Aim :alert and :warn at @room or @online. Always logged."
        ),
        new(
            PermissionNodes.Command.ROOMALERT,
            "Use :roomalert to send everyone in the room a pop-up message."
        ),
        new(
            PermissionNodes.Command.HOTELALERT,
            "Use :hotelalert to send everyone online a pop-up message."
        ),
        new(
            PermissionNodes.Command.EVENTALERT,
            "Use :eventalert to tell the hotel an event is on in this room."
        ),
        new(
            PermissionNodes.Command.WHOIS,
            "Use :whois to look up a player: where they are, their groups and balances."
        ),
        new(PermissionNodes.Command.FOLLOW, "Use :follow to go to the room a player is in."),
        new(
            PermissionNodes.Command.SUMMON,
            "Use :summon to bring a player to the room you are in."
        ),
        new(PermissionNodes.Command.GIVE, "Use :give to add to, or take from, a player's balance."),
        new(PermissionNodes.Command.GIVEBADGE, "Use :givebadge to give a player a badge."),
        new(PermissionNodes.Command.TAKEBADGE, "Use :takebadge to take a badge from a player."),
        new(
            PermissionNodes.Command.GIVEITEM,
            "Use :giveitem to put furniture in a player's inventory."
        ),
        new(
            PermissionNodes.Command.GIVE_MASS,
            "Aim :give, :givebadge, :takebadge and :giveitem at @room or @online. Always logged."
        ),
        new(
            PermissionNodes.Command.GROUP,
            "Use :group to add a player to a permission group, or remove them."
        ),
        new(
            PermissionNodes.Command.PERM,
            "Use :perm to check why a player does or does not hold a permission."
        ),
        new(PermissionNodes.Command.STATUS, "Use :status to see how the hotel is running."),
        new(PermissionNodes.Command.ONLINE, "Use :online to see how many players are online."),
        new(PermissionNodes.Command.ONLINE_LIST, "Have :online list who is online by name."),
        new(
            PermissionNodes.Command.MAINTENANCE,
            "Use :maintenance to put the hotel into maintenance, or end it."
        ),
        new(
            PermissionNodes.Command.SHUTDOWN,
            "Use :shutdown to close the hotel down after a countdown."
        ),
        new(
            PermissionNodes.Command.RELOAD,
            "Use :reload to re-read the catalog, texts, furniture and the like from the database."
        ),
        new(
            PermissionNodes.Command.ACHIEVEMENTS,
            "Inspect, advance, reconcile and retry achievements without revoking awards."
        ),
        new(
            PermissionNodes.Command.UNLOADROOM,
            "Use :unloadroom to unload this room so it is read from the database again."
        ),
        new(
            PermissionNodes.Command.ROOMKICKALL,
            "Use :roomkickall to clear this room of everyone but its owner and staff."
        ),
        new(PermissionNodes.Command.ROOMMUTE, "Use :roommute to silence this room's visitors."),
        new(
            PermissionNodes.Command.ROOMUNMUTE,
            "Use :roomunmute to let this room's visitors speak again."
        ),
        new(
            PermissionNodes.Hotel.MAINTENANCE_BYPASS,
            "Log in, and stay logged in, while the hotel is in maintenance."
        ),
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
        new(
            PermissionNodes.Permissions.SUPERUSER,
            "Edit any group, player or node, without the weight and held-node limits of permissions.manage.",
            ExplicitOnly: true
        ),
        new(PermissionNodes.Admin.PANEL, "Sign in to the admin panel with a passkey."),
        new(
            PermissionNodes.Admin.PASSKEYS_RESET,
            "Give another player a link to set up or replace their admin panel passkey."
        ),
        new(
            PermissionNodes.Admin.ROOMS_VIEW,
            "Find any room in the admin panel and see its settings, occupants, rights and bans."
        ),
        new(
            PermissionNodes.Admin.PLAYERS_VIEW,
            "Find any player in the admin panel and see their profile, wallet, rooms and sanctions."
        ),
        new(
            PermissionNodes.Admin.CATALOG_VIEW,
            "See the catalog in the admin panel: its pages, offers and prices."
        ),
        new(
            PermissionNodes.Catalog.MANAGE,
            "Change the catalog in the admin panel and publish it to players."
        ),
        new(PermissionNodes.Admin.PLAYERS_CREATE, "Create new players in the admin panel."),
        new(
            PermissionNodes.Admin.TICKETS_ISSUE,
            "Issue a player a login ticket in the admin panel, for players whose every permission they hold too."
        ),
        new(
            PermissionNodes.Admin.ACCOUNTS_MANAGE,
            "Unlink a player's Discord account and end their public site sign-ins, for players whose every permission they hold too."
        ),
        new(
            PermissionNodes.Admin.WELCOME_MESSAGE_MANAGE,
            "See and change the welcome message every player is shown when they log in, in the admin panel."
        ),
        new(
            PermissionNodes.Admin.COMMAND_LOG_VIEW,
            "Read the command log in the admin panel: who ran what, where from, and how it went."
        ),
        new(
            PermissionNodes.Admin.CHATLOG_VIEW,
            "Read the room chat log in the admin panel: what players said in rooms, whispers included."
        ),
        new(
            PermissionNodes.Admin.PERMISSIONS_VIEW,
            "See groups, players' permissions and the permission log in the admin panel."
        ),
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
