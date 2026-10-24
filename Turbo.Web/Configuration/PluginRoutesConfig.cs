using Turbo.Primitives.Settings;

namespace Turbo.Web.Configuration;

/// <summary>
/// Plugins' player-facing web routes (<c>Turbo:Web:PluginRoutes</c>), served at
/// <c>/api/plugins/&lt;key&gt;/...</c> on this host: how much a call may send and take, how often a
/// player and an address may call, which sites' pages may call, and how long the token the client
/// gets for them lasts. See <c>docs/plugin-web-routes.md</c>.
/// </summary>
public sealed class PluginRoutesConfig
{
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// The address clients call the routes at, the part before <c>/api/plugins/</c>, such as
    /// <c>https://example.com</c>. Empty uses <see cref="WebConfig.SiteUrl"/>, whose <c>/api</c> the
    /// site's reverse proxy sends here.
    /// </summary>
    [UrlSetting]
    public string PublicUrl { get; init; } = "";

    /// <summary>The largest request body a plugin is sent, in bytes.</summary>
    public int MaxBodyBytes { get; init; } = 64 * 1024;

    /// <summary>How long a plugin has to answer, in seconds, before the caller is told it timed out (504).</summary>
    public int TimeoutSeconds { get; init; } = 10;

    /// <summary>Calls one signed-in player may make a minute, across every plugin; more are refused (429).</summary>
    public int RequestsPerMinutePerPlayer { get; init; } = 120;

    /// <summary>Calls one address may make a minute, signed in or not; more are refused (429).</summary>
    public int RequestsPerMinutePerAddress { get; init; } = 300;

    /// <summary>
    /// Sites whose pages may call the routes from a browser (CORS), besides the hotel's own site
    /// (<see cref="WebConfig.SiteUrl"/>) and client (<see cref="WebConfig.ClientUrl"/>), which
    /// always may: their origins, such as <c>https://fansite.example.com</c>.
    /// </summary>
    public string[] AllowedOrigins { get; init; } = [];

    /// <summary>How long the token a client is given for the routes lasts, in minutes; it asks for another as it runs out.</summary>
    public int ClientTokenMinutes { get; init; } = 60;
}
