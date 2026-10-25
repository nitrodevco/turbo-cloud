using System.Threading;
using System.Threading.Tasks;

namespace Turbo.Contracts.Plugins.Web;

/// <summary>
/// A player-facing HTTP route of a plugin, served on the hotel's public web host at
/// <c>/api/plugins/&lt;key&gt;/&lt;pattern&gt;</c>. A public class implementing it is found in the
/// plugin's assembly as the plugin loads, built with the plugin's services and then the host's,
/// served once the plugin has started, and answers 404 from the moment the plugin unloads.
/// <para>
/// The host signs the caller in (the site's session or the client's token, as a player id), checks
/// <c>[RequiresPermission]</c> on the class, caps the body, the time and the rate, answers CORS
/// for the hotel's own sites, and turns an exception into a 500 with the plugin's key in the log.
/// A route is for signed-in players unless it says <see cref="Anonymous"/>. See
/// <c>docs/plugin-web-routes.md</c>.
/// </para>
/// </summary>
public interface IPluginWebRoute
{
    /// <summary><c>GET</c>, <c>POST</c>, <c>PUT</c>, <c>PATCH</c> or <c>DELETE</c>.</summary>
    string Method { get; }

    /// <summary>
    /// The address under the plugin's own prefix, such as <c>scores</c> or <c>scores/{game}</c>:
    /// literal segments and <c>{name}</c> values. It cannot leave the prefix (no <c>..</c>, no
    /// leading <c>/api</c>, no scheme).
    /// </summary>
    string Pattern { get; }

    /// <summary>Whether someone who is not signed in may call it. Such a request has no player.</summary>
    bool Anonymous => false;

    /// <summary>
    /// Whether <see cref="PluginWebRequest.Headers"/> keeps the caller's credentials
    /// (<c>authorization</c>, <c>cookie</c>, <c>proxy-authorization</c>), for a route that checks a
    /// service's own scheme. Off by default: the host has signed the caller in, and a route that
    /// never reads them cannot leak a player's session.
    /// </summary>
    bool CredentialHeaders => false;

    Task<PluginWebResult> HandleAsync(PluginWebRequest request, CancellationToken ct);
}
