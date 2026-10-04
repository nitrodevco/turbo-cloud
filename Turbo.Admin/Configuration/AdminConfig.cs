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
}
