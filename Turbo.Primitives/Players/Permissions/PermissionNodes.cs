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
    }

    public static class Catalog
    {
        public const string BUILDERS_CLUB_WITHOUT_MEMBERSHIP =
            "catalog.builders_club.without_membership";
    }

    public static class Navigator
    {
        public const string CATEGORY_STAFF = "navigator.category.staff";
    }

    public static class Moderation
    {
        public const string TOOL = "moderation.tool";
    }

    public static class Wired
    {
        public const string MENU = "wired.menu";
    }

    public static class Chat
    {
        public const string FURNI_CHOOSER = "chat.furni_chooser";
        public const string STYLE_STAFF = "chat.style.staff";
    }

    public static class Role
    {
        public const string AMBASSADOR = "role.ambassador";
    }

    public static class Permissions
    {
        public const string MANAGE = "permissions.manage";
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
    }
}
