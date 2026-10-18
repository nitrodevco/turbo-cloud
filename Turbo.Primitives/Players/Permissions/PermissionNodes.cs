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

        /// <summary>
        /// Change the catalog in the admin panel (pages, offers, prices) and publish it to
        /// players. Seeing it there needs <see cref="Admin.CATALOG_VIEW"/> as well.
        /// </summary>
        public const string MANAGE = "catalog.manage";
    }

    public static class Gamedata
    {
        /// <summary>
        /// Change the hotel's gamedata in the admin panel: take in Habbo's updates, edit furniture
        /// definitions, rebuild the files the client loads and roll changes back. Seeing it there
        /// needs <see cref="Admin.GAMEDATA_VIEW"/> as well.
        /// </summary>
        public const string MANAGE = "gamedata.manage";
    }

    public static class Content
    {
        /// <summary>
        /// Change the game's content in the panel: publish achievements, pin badges' rarity and
        /// give or take them, edit the navigator's categories, fix and remove groups, and edit pet
        /// breeds, pet speech and bots. Seeing it there needs <see cref="Admin.CONTENT_VIEW"/>.
        /// </summary>
        public const string MANAGE = "content.manage";
    }

    public static class Settings
    {
        /// <summary>
        /// Change the server's settings in the admin panel: override what <c>appsettings.json</c>
        /// says, replace a secret, and put a setting back. Seeing them there needs
        /// <see cref="Admin.SETTINGS_VIEW"/> as well. A setting is the whole server's, so this is
        /// for those who run it.
        /// </summary>
        public const string MANAGE = "settings.manage";
    }

    /// <summary>
    /// Memberships held by permission rather than bought: the player counts as a member for as
    /// long as they hold the node, whatever their subscription rows say. Nothing is written, so
    /// taking the node away leaves them with exactly the membership they bought.
    /// </summary>
    public static class Club
    {
        public const string HABBO_CLUB_UNLIMITED = "club.habbo_club.unlimited";
        public const string BUILDERS_CLUB_UNLIMITED = "club.builders_club.unlimited";
    }

    public static class Figure
    {
        /// <summary>
        /// Wear any clothing and colour the figure data has, whatever it says of club, sale or
        /// selection: staff dressing for an event, or testing clothing not yet released.
        /// </summary>
        public const string ANY = "figure.any";
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

        // The furni commands the Flash client sends as chat after its own confirm
        // (SessionDataManager.pickAllFurniture, pickAllBuilderFurniture, ejectAllFurniture).
        public const string PICKALL = "command.pickall";
        public const string PICKALLBC = "command.pickallbc";
        public const string EJECTALL = "command.ejectall";

        // The other lines the Flash client sends as chat on purpose (ChatInputWidgetHandler,
        // SessionDataManager.resetScores and ejectPets).
        public const string EJECTPETS = "command.ejectpets";
        public const string RESETSCORES = "command.resetscores";
        public const string MUTEPETS = "command.mutepets";

        /// <summary>
        /// <c>:mutebots</c> ("Ignore bot chats", habbopages): the Flash client has no case for
        /// it, so it reaches the server as chat like any other line.
        /// </summary>
        public const string MUTEBOTS = "command.mutebots";

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
        public const string GIFT = "command.gift";

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
        public const string ROOMEFFECT = "command.roomeffect";
        public const string WORDQUIZ = "command.wordquiz";

        /// <summary>Offer rooms as the new user flow's starter rooms, and take their pictures.</summary>
        public const string STARTERROOM = "command.starterroom";

        /// <summary>Make rooms new user lobbies (door mode 4), with the command or the panel.</summary>
        public const string NOOBLOBBY = "command.nooblobby";

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

        /// <summary>
        /// Lifts the limits on whoever edits permissions: any group, any player and any node,
        /// themselves included, and the groups that carry this node. Needs
        /// <see cref="MANAGE"/> as well. Explicit only: no wildcard grants it, so it is given by
        /// naming it, by the console or by a holder.
        /// </summary>
        public const string SUPERUSER = "permissions.superuser";
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

        /// <summary>
        /// Read the command log in the panel: every logged command, who ran it, where from, with
        /// what and how it went.
        /// </summary>
        public const string COMMAND_LOG_VIEW = "admin.commandlog.view";

        /// <summary>
        /// Read the room chat log in the panel: what players said in rooms, whispers too, who to
        /// and when.
        /// </summary>
        public const string CHATLOG_VIEW = "admin.chatlog.view";

        /// <summary>
        /// See the catalog in the panel: its pages, offers and prices. Changing it needs
        /// <see cref="Catalog.MANAGE"/> as well.
        /// </summary>
        public const string CATALOG_VIEW = "admin.catalog.view";

        /// <summary>
        /// Take Habbo's Builders Club catalogue in from a capture in the panel: see what it would
        /// add and change, and make it. Needs <see cref="CATALOG_VIEW"/> and
        /// <see cref="Catalog.MANAGE"/> as well.
        /// </summary>
        public const string CATALOG_IMPORT = "admin.catalog.import";

        /// <summary>
        /// Manage the reward tracks in the panel: change, add and remove tracks, their tasks and
        /// prizes, take Habbo's in from a capture and read them again. Every change is logged.
        /// </summary>
        public const string REWARD_TRACKS_MANAGE = "admin.rewardtracks.manage";

        /// <summary>
        /// See the hotel's gamedata in the panel: Habbo's releases, what an update would change,
        /// the files the client loads and the history of changes. Changing it needs
        /// <see cref="Gamedata.MANAGE"/> as well.
        /// </summary>
        public const string GAMEDATA_VIEW = "admin.gamedata.view";

        /// <summary>Create new players in the panel. Seeing players needs <see cref="PLAYERS_VIEW"/>.</summary>
        public const string PLAYERS_CREATE = "admin.players.create";

        /// <summary>
        /// Issue a player a login ticket in the panel, and take it away. A ticket logs in as the
        /// player, so it is only for a player whose every node the issuer holds too, as a setup
        /// link is.
        /// </summary>
        public const string TICKETS_ISSUE = "admin.tickets.issue";

        /// <summary>
        /// Manage how a player signs in to the public site: take their Discord account off them, and
        /// end their sign-ins there. Only for a player whose every node the staff member holds too.
        /// </summary>
        public const string ACCOUNTS_MANAGE = "admin.accounts.manage";

        /// <summary>
        /// See and change the welcome message every player is shown when they log in, in the
        /// panel's hotel controls.
        /// </summary>
        public const string WELCOME_MESSAGE_MANAGE = "admin.welcome.manage";

        /// <summary>
        /// See the server's settings in the panel: each value, where it comes from and whether a
        /// restart is waiting on it. Secrets are never shown. Changing them needs
        /// <see cref="Settings.MANAGE"/> as well.
        /// </summary>
        public const string SETTINGS_VIEW = "admin.settings.view";

        /// <summary>
        /// Restart the server from the panel after a countdown the hotel is warned of: everyone is
        /// sent home, what the grains hold is saved, and the process ends for its host to start it
        /// again, so settings waiting on a restart apply. Every request is in the command log.
        /// </summary>
        public const string SERVER_RESTART = "admin.server.restart";

        /// <summary>
        /// See the game's content in the panel: achievements, badges, the navigator's categories,
        /// groups, pets and bots. Changing it needs <see cref="Content.MANAGE"/> as well.
        /// </summary>
        public const string CONTENT_VIEW = "admin.content.view";

        /// <summary>
        /// See the server's plugins in the panel: each one's manifest, whether it loaded and why
        /// not, and the capabilities, packet headers and migrations it added.
        /// </summary>
        public const string PLUGINS_VIEW = "admin.plugins.view";

        /// <summary>
        /// Reload a plugin, scan the plugin folders again and turn a plugin off or on (kept across
        /// restarts) in the panel. Seeing them needs <see cref="PLUGINS_VIEW"/> as well.
        /// </summary>
        public const string PLUGINS_MANAGE = "admin.plugins.manage";

        /// <summary>
        /// Install a plugin package from the panel, when the server allows it
        /// (<c>Turbo:Plugin:UploadEnabled</c>). A package is server code, so the node is explicit
        /// only: no wildcard grants it. Needs <see cref="PLUGINS_MANAGE"/> as well.
        /// </summary>
        public const string PLUGINS_INSTALL = "admin.plugins.install";
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
