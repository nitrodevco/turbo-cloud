namespace Turbo.Admin.Configuration;

/// <summary>
/// The admin panel's API (<c>Turbo:Admin</c>). Off unless enabled: it is an HTTP port into the
/// hotel, so a hotel turns it on knowingly, and puts it behind a TLS proxy the way the game
/// socket is.
/// </summary>
public sealed class AdminConfig
{
    public const string SECTION_NAME = "Turbo:Admin";

    public bool Enabled { get; init; }

    /// <summary>Where the API listens. Loopback by default, for a reverse proxy in front.</summary>
    public string Url { get; init; } = "http://127.0.0.1:8090";

    /// <summary>
    /// Where the panel is served. Setup links point here, passkeys are bound to its domain, and
    /// it is the one origin the API accepts browser requests from.
    /// </summary>
    public string PanelUrl { get; init; } = "http://localhost:5173";

    /// <summary>How long a setup link (<c>adminsetup</c>) works. It also works only once.</summary>
    public int SetupLinkHours { get; init; } = 24;

    /// <summary>How long a browser has to answer a passkey prompt.</summary>
    public int CeremonyMinutes { get; init; } = 5;

    /// <summary>How long a panel session lasts before signing in again.</summary>
    public int SessionHours { get; init; } = 12;

    /// <summary>Sessions one player may hold at once; signing in past it ends their oldest.</summary>
    public int MaxSessionsPerPlayer { get; init; } = 5;

    /// <summary>Sign-in and setup requests one address may make a minute.</summary>
    public int SignInAttemptsPerMinute { get; init; } = 10;

    /// <summary>
    /// The domain passkeys are bound to. Empty means the panel's own host (from
    /// <see cref="PanelUrl"/>). A parent domain (<c>example.com</c>) lets them work on every
    /// subdomain, but then any of those sites can ask for them.
    /// </summary>
    public string PasskeyRpId { get; init; } = "";

    /// <summary>The longest command line the console accepts.</summary>
    public int MaxCommandLength { get; init; } = 1000;

    /// <summary>Rooms per page in the panel's room search.</summary>
    public int RoomSearchPageSize { get; init; } = 25;

    /// <summary>The longest text the room search accepts.</summary>
    public int RoomSearchMaxLength { get; init; } = 64;

    /// <summary>Players per page in the panel's player search.</summary>
    public int PlayerSearchPageSize { get; init; } = 25;

    /// <summary>Entries per page in the panel's command log.</summary>
    public int CommandLogPageSize { get; init; } = 50;

    /// <summary>Lines per page in the panel's chat log, and on each side of a line in context.</summary>
    public int ChatlogPageSize { get; init; } = 100;

    /// <summary>
    /// The client's <c>nitro-config.json</c>, such as
    /// <c>https://hotel.example.com/config/nitro-config.json</c>. The panel draws catalog icons and
    /// images, furniture icons and badges from the same addresses the client does. Empty shows
    /// none.
    /// </summary>
    public string ClientConfigUrl { get; init; } = "";

    /// <summary>How long the client's addresses are kept before the config is read again.</summary>
    public int ClientConfigCacheMinutes { get; init; } = 10;

    /// <summary>
    /// The page layouts the catalog editor offers, besides any a page already uses: those the
    /// client ships a window for, and the codes it maps onto one (<c>bots</c> is drawn as
    /// <c>default_3x3</c>). A code the client has no window for leaves the page empty.
    /// </summary>
    public string[] CatalogLayouts { get; init; } =
    [
        "badge_display",
        "bots",
        "builders_club_addons",
        "builders_club_frontpage",
        "builders_club_loyalty",
        "club_buy",
        "club_gifts",
        "default_3x3",
        "default_3x3_color_grouping",
        "default_3x3_extrainfo",
        "frontpage4",
        "frontpage_featured",
        "guild_custom_furni",
        "guild_forum",
        "guild_frontpage",
        "info_duckets",
        "info_loyalty",
        "info_rentables",
        "loyalty_vip_buy",
        "marketplace",
        "marketplace_own_items",
        "monkey",
        "niko",
        "petcustomization",
        "pets",
        "pets2",
        "pets3",
        "pixeleffects",
        "recycler",
        "recycler_info",
        "recycler_prizes",
        "roomads",
        "single_bundle",
        "sold_ltd_items",
        "soundmachine",
        "spaces_new",
        "trophies",
        "vip_buy",
    ];

    /// <summary>
    /// The client's login address with <c>{ticket}</c> where a login ticket goes, such as
    /// <c>https://hotel.example.com/client?sso={ticket}</c>, so the panel can hand out a link to
    /// log in with. Empty shows the ticket alone.
    /// </summary>
    public string ClientLoginUrl { get; init; } = "";

    /// <summary>The longest a login ticket from the panel can work for, in days, short of never.</summary>
    public int TicketMaxLifetimeDays { get; init; } = 365;

    /// <summary>Furniture the catalog editor's item picker lists at once.</summary>
    public int CatalogFurnitureSearchLimit { get; init; } = 25;

    /// <summary>The longest room alert the panel sends, as <c>:roomalert</c>'s limit is in the hotel.</summary>
    public int RoomAlertMaxLength { get; init; } = 500;

    /// <summary>The longest a player can be muted in a room from the panel, in minutes.</summary>
    public int RoomMuteMaxMinutes { get; init; } = 60;

    /// <summary>
    /// How long the live feed gathers changes before sending them, so a busy hotel sends the
    /// panel one message a beat rather than one per player who walks into a room.
    /// </summary>
    public int LiveBatchMs { get; init; } = 1000;

    /// <summary>
    /// How often a quiet live stream sends a keep-alive, and checks the session behind it is still
    /// signed in and allowed the panel. Under the 100 seconds Cloudflare lets a quiet response sit.
    /// </summary>
    public int LiveHeartbeatSeconds { get; init; } = 25;
}
