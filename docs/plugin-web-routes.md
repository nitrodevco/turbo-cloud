# Plugin web routes and play policies

A plugin can serve **players** over HTTP: a hotel's site page or a client module asks the plugin
for something (a leaderboard, a player's notes, a shop page's data) at
`/api/plugins/<key>/...` on the hotel's public web host, signed in as the player. The host owns
everything around the plugin's handler: who is calling, permissions, limits, CORS, errors and the
plugin's lifecycle. It is the **registry** kind of `docs/extensions.md` (§3, §4.8).

A plugin can also put a **play policy** in front of the hotel: when a player asks to play (the
site's Play, the client's login), the policy allows, or refuses with a reason and an address to
send them to ("vote for the hotel first", an address check). It is the **pipeline / policy** kind
(§4.2).

The staff side is separate: the admin panel's plugin endpoints (`IPluginAdmin`,
`docs/admin-panel.md`) are on the panel's own host with the panel's sign-in, at
`/api/plugins/<key>/api/...` there. Nothing of the panel is reachable from the public host.

## 1. A route

A public class implementing `IPluginWebRoute` (`Turbo.Contracts.Plugins.Web`) in the plugin's
assembly is a route. It is found as the plugin loads, built with the plugin's services and then the
host's (so its constructor may take `IGrainFactory` beside the plugin's own services), and served
once the plugin has started.

```csharp
/// GET /api/plugins/leaderboard/top: the ten best scores, for anyone (the site shows it too).
public sealed class TopRoute(ScoreBook scores) : IPluginWebRoute
{
    public string Method => "GET";
    public string Pattern => "top";
    public bool Anonymous => true;

    public Task<PluginWebResult> HandleAsync(PluginWebRequest request, CancellationToken ct) =>
        Task.FromResult(PluginWebResult.Ok(scores.Top(10)));
}

/// POST /api/plugins/leaderboard/scores/{game}: the signed-in player's score in one game.
public sealed class SubmitRoute(ScoreBook scores) : IPluginWebRoute
{
    public string Method => "POST";
    public string Pattern => "scores/{game}";

    public Task<PluginWebResult> HandleAsync(PluginWebRequest request, CancellationToken ct)
    {
        var points = request.Body?.GetProperty("points").GetInt32() ?? 0;

        if (points is < 0 or > 1000)
            return Task.FromResult(PluginWebResult.BadRequest("Points are 0 to 1000."));

        scores.Add(request.PlayerId!.Value, request.Route["game"], points);

        return Task.FromResult(PluginWebResult.NoContent());
    }
}

/// DELETE /api/plugins/leaderboard/scores: staff holding the plugin's own node clear the board.
[RequiresPermission("plugin.leaderboard.reset")]
public sealed class ResetRoute(ScoreBook scores) : IPluginWebRoute { ... }
```

- **`Method`**: `GET`, `POST`, `PUT`, `PATCH` or `DELETE`.
- **`Pattern`**: segments under the plugin's own prefix, literal or `{name}` (letters, digits, `_`).
  Its values arrive in `request.Route`. A pattern that could leave the prefix (`..`, `.`, a
  leading `/api`, a scheme, `?`, `#`, `%`, `\`) fails the plugin's load, as does one mapped twice.
  The prefix is always the plugin's own key: a plugin cannot answer for another, or for a core path.
- **`Anonymous`**: false by default, so a route is for signed-in players and `request.PlayerId` is
  set. An anonymous route is answered for anyone; `PlayerId` is still set when a player is signed in.
- **`[RequiresPermission]`** (`Turbo.Primitives.Players.Permissions`) on the class: the player must
  hold any one of its nodes, as on packet handlers (`docs/permissions.md` §11). Declare the node with
  an `IPermissionNodeSource` (or `IPluginAdmin`'s `Node`). An anonymous route that requires a
  permission fails the load: a permission needs a player.
- **`PluginWebRequest`**: `Method`, `Path`, `Route`, `Query`, `Body` (the JSON body as a
  `JsonElement`, null without one), `PlayerId`, `RemoteAddress`, `Headers` (by lower-case name,
  looked up in any case; a repeated header joined with `", "`) and `RawBody` (the body's bytes as
  sent, empty without one).
- **`CredentialHeaders`**: false by default, so `Headers` leaves out `authorization`, `cookie` and
  `proxy-authorization`: the host has signed the caller in, and a route that never reads them can't
  leak a player's session. A route that checks a service's own scheme (Basic auth on a webhook)
  sets it.

### Verify a webhook

A payment provider or a game server calls an anonymous route and signs what it sends. Work the
signature out over `RawBody`, never over `Body`: parsing drops the sender's spacing, so the same
JSON written again is other bytes.

```csharp
/// POST /api/plugins/shop/webhooks/payments: the provider's word that a payment went through.
public sealed class PaymentHookRoute(ShopSettings settings, Orders orders) : IPluginWebRoute
{
    public string Method => "POST";
    public string Pattern => "webhooks/payments";
    public bool Anonymous => true;

    public async Task<PluginWebResult> HandleAsync(PluginWebRequest request, CancellationToken ct)
    {
        var expected = HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(settings.WebhookSecret),
            request.RawBody.Span
        );

        if (
            !request.Headers.TryGetValue("x-signature", out var sent)
            || !CryptographicOperations.FixedTimeEquals(expected, Convert.FromHexString(sent))
        )
            return PluginWebResult.Forbidden("Bad signature.");

        await orders.MarkPaidAsync(request.Body!.Value.GetProperty("order").GetString()!, ct);

        return PluginWebResult.NoContent();
    }
}
```

A provider that signs with a certificate and a transmission id (PayPal's `paypal-transmission-*`
headers) is checked the same way: every header it names is in `Headers`, and the bytes it signed
are `RawBody`. The body still has to be JSON (400 otherwise) and within `MaxBodyBytes`; the address
and rate limits apply to the provider as to anyone.
- **`PluginWebResult`**: a status with a JSON value (`Ok(value)`), `NoContent()`, or an error
  (`BadRequest`, `Forbidden`, `NotFound`, or `new(status, Error: "...")`) sent as
  `{"error": "..."}`, words the caller shows.

### Who is calling

The host signs the caller in before the route runs:

1. **The client's token**: `Authorization: Bearer <token>`, which nitro-next attaches for a module
   (`api.http`, below). A token that is not (or no longer) one is answered 401, so the client asks
   for a new one.
2. **The site's session**: the `turbo_session` cookie the public site sets on sign-in
   (`docs/public-site.md`), for the site's own pages on the same domain.

Neither: the caller is anonymous, and a route that is not anonymous answers 401.

### What the host does around a route

In order, for each call:

| Check | Answer |
| --- | --- |
| A page of a site that is not the hotel's own calls with anything but `GET` | 403 |
| The address's calls this minute (`RequestsPerMinutePerAddress`) | 429, `Retry-After` |
| The plugin is being reloaded | 503, `Retry-After: 2` |
| No live route of the plugin matches (unknown key, plugin off, unloaded, no such route) | 404 |
| Not signed in on a player route, or a stale token | 401 |
| `[RequiresPermission]` not held | 403 |
| The player's calls this minute (`RequestsPerMinutePerPlayer`, across every plugin) | 429, `Retry-After` |
| The body is larger than `MaxBodyBytes` / not JSON | 413 / 400 |
| The route takes longer than `TimeoutSeconds` (whether or not it watches its token) | 504 |
| The route throws | 500 `{"error":"Something went wrong."}`; the exception is logged with the plugin's key, never sent |

**CORS.** The hotel's own site (`SiteUrl`), its client (`ClientUrl`) and the sites in
`PluginRoutes:AllowedOrigins` may call from a browser: the answer names the page's origin
(`Access-Control-Allow-Origin`), and a preflight (`OPTIONS`) answers the route's methods and allows
the `Authorization` and `Content-Type` headers. Never with credentials: the client sends its token,
and the site's pages are on the site's own origin.

**Lifecycle.** Routes answer once the plugin has started. They stop answering (404) as the plugin
starts to unload, before it is stopped, and while a new version loads the plugin answers 503. Turning
a plugin off, uninstalling it, or a failed load leaves no route behind. What the serializer learned
of a plugin's answer types is let go as it unloads, so a plugin that answered with its own types
unloads too.

## 2. Calling a route from a client module

The module calls its own plugin's routes through `api.http` (nitro-next `docs/client-modules.md`);
the client signs the call in, and the module never sees a token:

```js
const top = await api.http.get('top');            // GET /api/plugins/<key>/top
if (top.ok) show(top.data);

const sent = await api.http.post('scores/darts', { points: 120 });
if (!sent.ok) api.notifications.add('info', sent.error);   // the route's own words
```

`get(path, query?)`, `post(path, body?)`, `put`, `patch` and `delete(path)` resolve
`{ ok, status, data, error }` and never throw for an HTTP error. A path that would leave the plugin's
prefix is refused in the client (`status` 0).

The token comes over the game socket, as the Turbo extension `web.session` (version 1):

| Direction | Header | Packet | Body |
| --- | --- | --- | --- |
| client → server | 30011 | `TurboWebSessionRequestMessage` | empty |
| server → client | 30012 | `TurboWebSessionMessage` | `string baseUrl`, `string token`, `int expiresInSeconds` |

- `baseUrl` is the address before `/api/plugins/` (`PluginRoutes:PublicUrl`, else `SiteUrl`). An
  empty token means the hotel serves no plugin routes (the web host or the routes are off).
- One token per player, kept in the server's memory for `ClientTokenMinutes`; each request replaces
  the player's last. A restart forgets them: the client asks again on the next 401.
- Only a signed-in session is answered (the sign-in gate), and only a client that accepted
  `web.session` is sent the answer (`docs/client-capabilities.md`).

A hotel's own site calls the same routes with `fetch('/api/plugins/leaderboard/top')`, its cookie
attached by the browser.

## 3. A play policy

A public class implementing `IPlayPolicy` (`Turbo.Primitives.Authentication`) in a plugin's assembly
is found as it loads and taken away as it unloads:

```csharp
/// Players vote for the hotel on a listing site before they play, at most once a day.
public sealed class VoteFirst(VoteBook votes) : IPlayPolicy
{
    public int Order => 0;

    public async ValueTask<PlayDecision> CheckAsync(PlayRequest request, CancellationToken ct) =>
        await votes.VotedTodayAsync(request.PlayerId, ct)
            ? PlayDecision.Allow
            : PlayDecision.Refuse("Vote for the hotel first.", "https://listing.example.com/vote/hotel");
}
```

- **Where it runs.** `PlayRequest.Source` says which:
  - `Site`: the site's Play (`POST /api/play`), after the sign-in and ban checks and before a ticket
    is issued. A refusal answers 403 `{"message": "...", "redirectUrl": "..."}` and issues no
    ticket; the site shows the message and offers the address.
  - `Login`: the client's login with a ticket (`SSOTicketMessage`), after the ban and maintenance
    checks and before the session becomes the player's. A refusal sends Habbo's notification popup
    (`NotificationDialog`, type `turbo.play.refused`, its `message` and, with an address, `linkUrl`),
    then closes the connection. A hotel styles it with the external variable
    `notification.turbo.play.refused` as any notification type.
  `RemoteAddress` is the caller's address on the site (empty at login).
- **Order.** Policies run by `Order` (lower first), then by plugin key, then as registered; the
  first refusal is the answer and the rest are not asked. With none, everyone plays: the default is
  core's behaviour, unchanged.
- **Errors.** A policy that throws is logged with its plugin and passed over: it allows. Keep a
  policy fast; it runs on every Play and every login.

## 4. Settings (`Turbo:Web:PluginRoutes`)

| Key | Default | What |
| --- | --- | --- |
| `Enabled` | `true` | Serves plugin routes on the public web host (which itself needs `Turbo:Web:Enabled`). Off, `/api/plugins/` answers nothing and clients are given no token. |
| `PublicUrl` | empty | The address clients call before `/api/plugins/`, such as `https://example.com`. Empty uses `SiteUrl`, whose `/api` the site's reverse proxy sends to this host. |
| `MaxBodyBytes` | `65536` | The largest request body a route is sent. |
| `TimeoutSeconds` | `10` | How long a route has to answer. |
| `RequestsPerMinutePerPlayer` | `120` | Calls one signed-in player may make a minute, across every plugin. |
| `RequestsPerMinutePerAddress` | `300` | Calls one address may make a minute, signed in or not. |
| `AllowedOrigins` | `[]` | Sites besides the hotel's own site and client whose pages may call the routes (origins, such as `https://fansite.example.com`). |
| `ClientTokenMinutes` | `60` | How long the token a client is given lasts. |

The limits count at the web host: behind the site's reverse proxy the address is the one in
`X-Forwarded-For` (`docs/public-site.md`). A hotel whose client is on another domain than its site
sets `PublicUrl` to an address that reaches this host and is listed by nginx's `/api/` location.

## 5. Example

`docs/examples/template-patches/purse-skills` serves `GET top` (anonymous: the hotel's best players)
and `GET me` (the player's own skill), and its window shows the best level through `api.http`.
`Turbo.Tests` compiles and loads it (`TemplatePatchExampleTests`); `PluginWebRoutesTests` drives a
compiled plugin's routes over real HTTP (anonymous and player routes, token and cookie, permissions,
the caps, CORS, refused patterns, unload and reload, a signed webhook checked over its headers and
raw body), and `LoginAdmissionTests` /
`PublicSiteTests` a play policy refusing with an address.

## 6. What it changed in the server

- `Turbo.Contracts`: `Plugins.Web.IPluginWebRoute`, `PluginWebRequest`, `PluginWebResult`,
  `IPluginWebRoutes` and `PluginWebEndpoint`.
- `Turbo.Primitives`: `Authentication.IPlayPolicy`, `PlayRequest`, `PlaySource`, `PlayDecision`,
  `IPlayPolicies`; `Web.IPlayerWebTokens`, `PlayerWebToken`; `ClientCapabilities.WEB_SESSION`,
  `TurboWebSessionRequestMessage`, `TurboWebSessionMessage`.
- `Turbo.Plugins`: `PluginWebRoutes` (the scan, the pattern rules, live/reloading/off with the
  plugin), `SerializerCaches` (lets a plugin's answer types go as it unloads).
- `Turbo.Web`: `PluginWebEndpoints` (the host's checks), `PlayerWebTokens`, `PluginRoutesConfig`;
  Play asks the play policies (`PlayRefusedResponse`).
- `Turbo.Authentication`: `PlayPolicies` and its scan (`PlayPolicyFeatureProcessor`).
- `Turbo.PacketHandlers`: `TurboWebSessionRequestMessageHandler`; `SSOTicketMessageHandler` asks
  the play policies.
