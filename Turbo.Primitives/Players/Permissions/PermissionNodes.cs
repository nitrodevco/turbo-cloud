namespace Turbo.Primitives.Players.Permissions;

/// <summary>
/// The permission nodes core gates on. Code names a node through these constants, never as a
/// literal, so a typo is a compile error and "find references" finds every gate. Each node is
/// registered, with what it gates, by <see cref="CorePermissionNodeSource"/>. See
/// <c>docs/permissions.md</c> §7.
/// </summary>
public static class PermissionNodes
{
    /// <summary>Trading, enforced by the server; the client ignores the <c>TRADE</c> perk.</summary>
    public const string TRADE = "trade";

    public static class Room
    {
        public const string CONTROL_ANY = "room.control.any";
        public const string FURNI_STEAL = "room.furni.steal";
        public const string FURNI_PICKUP_ANY = "room.furni.pickup_any";
        public const string ENTER_LOCKED = "room.enter.locked";
        public const string ENTER_FULL = "room.enter.full";
        public const string ENTER_HIDDEN = "room.enter.hidden";
        public const string MODERATE_ANY = "room.moderate.any";
        public const string FLOORPLAN_SAVE_WITHOUT_CLUB = "room.floorplan.save_without_club";
        public const string FLOORPLAN_LARGE = "room.floorplan.large";
        public const string EVENT_EDIT_ANY = "room.event.edit_any";
        public const string FURNI_RENT_CANCEL_ANY = "room.furni.rent_cancel_any";
        public const string FURNI_BRANDING = "room.furni.branding";
        public const string FURNI_CUSTOM_VARIABLES = "room.furni.custom_variables";
        public const string FURNI_YOUTUBE_ANY = "room.furni.youtube_any";
        public const string FURNI_VIMEO_EDIT = "room.furni.vimeo_edit";
    }

    public static class Catalog
    {
        public const string BUILDERS_CLUB_WITHOUT_MEMBERSHIP =
            "catalog.builders_club.without_membership";
        public const string GUILD_ANY_GROUP = "catalog.guild.any_group";
        public const string GIFT_HIDE_SENDER = "catalog.gift.hide_sender";
    }

    public static class Navigator
    {
        public const string CATEGORY_STAFF = "navigator.category.staff";
        public const string STAFF_PICK = "navigator.staff_pick";
    }

    public static class Moderation
    {
        public const string TOOL = "moderation.tool";
    }

    public static class Wired
    {
        public const string MENU = "wired.menu";
    }

    public static class Guild
    {
        public const string DELETE_ANY = "guild.delete_any";
    }

    public static class Chat
    {
        /// <summary>Speaking at all; the default group grants it and a hotel mute denies it.</summary>
        public const string SPEAK = "chat.speak";
        public const string FURNI_CHOOSER = "chat.furni_chooser";
        public const string STYLE_STAFF = "chat.style.staff";
    }

    /// <summary>
    /// What lets a player run a chat command. A core command's node is <c>command.&lt;name&gt;</c>;
    /// a plugin's is <c>&lt;prefix&gt;.command.&lt;name&gt;</c>.
    /// </summary>
    public static class Command
    {
        public const string COMMANDS = "command.commands";
        public const string KICK = "command.kick";
        public const string MUTE = "command.mute";

        /// <summary>Go ahead with a line the server asked to have confirmed.</summary>
        public const string CONFIRM = "command.confirm";

        // Operator commands (docs/commands.md section 4.1). None is granted by default.
        public const string BAN = "command.ban";
        public const string UNBAN = "command.unban";
        public const string SILENCE = "command.silence";
        public const string TRADELOCK = "command.tradelock";
        public const string DISCONNECT = "command.disconnect";
        public const string WARN = "command.warn";
        public const string ALERT = "command.alert";
        public const string ROOMALERT = "command.roomalert";
        public const string HOTELALERT = "command.hotelalert";
        public const string EVENTALERT = "command.eventalert";

        /// <summary>Aim an alert or a warning at <c>@room</c> or <c>@online</c>.</summary>
        public const string ALERT_MASS = "command.alert.mass";
        public const string WHOIS = "command.whois";
        public const string FOLLOW = "command.follow";
        public const string SUMMON = "command.summon";
        public const string GIVE = "command.give";
        public const string GIVEBADGE = "command.givebadge";
        public const string TAKEBADGE = "command.takebadge";
        public const string GIVEITEM = "command.giveitem";

        /// <summary>Aim a currency, badge or item at <c>@room</c> or <c>@online</c>.</summary>
        public const string GIVE_MASS = "command.give.mass";
        public const string GROUP = "command.group";
        public const string PERM = "command.perm";
        public const string STATUS = "command.status";
        public const string ONLINE = "command.online";

        /// <summary>See who is online by name, not just how many.</summary>
        public const string ONLINE_LIST = "command.online.list";
        public const string MAINTENANCE = "command.maintenance";
        public const string SHUTDOWN = "command.shutdown";
        public const string RELOAD = "command.reload";
        public const string ACHIEVEMENTS = "command.achievements";
        public const string UNLOADROOM = "command.unloadroom";
        public const string ROOMKICKALL = "command.roomkickall";
        public const string ROOMMUTE = "command.roommute";
        public const string ROOMUNMUTE = "command.roomunmute";

        /// <summary>Not a command: holding it has every use of a command by this player logged.</summary>
        public const string LOG = "command.log";
    }

    public static class Hotel
    {
        /// <summary>Log in, and stay logged in, while the hotel is in maintenance.</summary>
        public const string MAINTENANCE_BYPASS = "hotel.maintenance.bypass";
    }

    public static class Role
    {
        public const string AMBASSADOR = "role.ambassador";
    }

    public static class Permissions
    {
        public const string MANAGE = "permissions.manage";
    }

    public static class Admin
    {
        /// <summary>Sign in to the admin panel. What can be done there is checked node by node.</summary>
        public const string PANEL = "admin.panel";

        /// <summary>
        /// Give another player a link to make their admin panel passkey, which replaces any they
        /// have. Only for a player whose every node the issuer holds too: a link is the account.
        /// </summary>
        public const string PASSKEYS_RESET = "admin.passkeys.reset";

        /// <summary>Find any room in the panel and see its settings, who is in it, rights and bans.</summary>
        public const string ROOMS_VIEW = "admin.rooms.view";

        /// <summary>
        /// See every group, any player's permissions, who is given a node, and the permission log
        /// in the panel. Changing them needs <see cref="Permissions.MANAGE"/> as well.
        /// </summary>
        public const string PERMISSIONS_VIEW = "admin.permissions.view";

        /// <summary>
        /// Find any player in the panel and see their profile, wallet, rooms and sanctions. Read
        /// only: changing a player needs the node of whatever is changed.
        /// </summary>
        public const string PLAYERS_VIEW = "admin.players.view";
    }

    /// <summary>
    /// Nodes that exist only to be projected into <c>PerkAllowances</c>; the server gates
    /// nothing on them.
    /// </summary>
    public static class Perk
    {
        public const string CAMERA = "perk.camera";
        public const string MOUSE_ZOOM = "perk.mouse_zoom";
        public const string CITIZEN = "perk.citizen";
        public const string NAVIGATOR_THUMBNAIL_CAMERA = "perk.navigator.thumbnail_camera";
        public const string NAVIGATOR_PHASE_ONE = "perk.navigator.phase_one";
        public const string NAVIGATOR_PHASE_TWO = "perk.navigator.phase_two";
        public const string GUIDE_TOOL = "perk.guide_tool";
        public const string JUDGE_CHAT_REVIEWS = "perk.judge_chat_reviews";
        public const string CALL_ON_HELPERS = "perk.call_on_helpers";
        public const string VOTE_IN_COMPETITIONS = "perk.vote_in_competitions";
        public const string HABBO_CLUB_OFFER_BETA = "perk.habbo_club_offer_beta";

        /// <summary>Not a perk code: the client turns video offers off at security level 1.</summary>
        public const string NO_VIDEO_OFFERS = "perk.no_video_offers";
    }
}
