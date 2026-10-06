# Public site

The public site is the hotel's front door, on its own domain (for example `example.com`), apart
from the admin panel. People:

1. **sign in with Discord.** Anyone with a Discord account can sign in.
2. **choose their name the first time.** The site suggests their Discord name, fitted to Turbo's
   name rules, and checks names as they type. They also pick a gender, and get that gender's
   default look. This makes their player.
3. **press Play.** The client opens inside the page, from its own subdomain (for example
   `play.example.com`), logged in with a fresh login ticket.

It has two parts:

- **`turbo-web`**, a React app built to static files;
- **the public site API** inside the Turbo server (`Turbo:Web`). This is a small web host of its
  own, on its own address (`127.0.0.1:8092` by default), apart from the admin API. The public
  domain can reach nothing of the panel.

## How it works

- **Signing in.** `/api/auth/discord` sends the browser to Discord. The request asks for the
  `identify` scope only (id and username, no email). A random state goes along and is also kept
  in a cookie. Discord sends the person back to `/api/auth/discord/callback`, where the server
  checks that the two match, so no other site can finish a sign-in in someone's browser.
  - **Discord account already linked to a player:** they are signed in.
  - **Otherwise:** they go to `/welcome` to choose a name. They have `SignUpMinutes` to do it.
  - **Something goes wrong:** they go back to the front page with the reason. They may have said
    no on Discord, taken too long, or registration may be closed.
- **A sign-in to the site** is a random token in an `HttpOnly` cookie, kept as a SHA-256 hash in
  `web_sessions`, for `SessionDays`. It signs in to the site only. The game is a separate login.
- **Play** issues a ticket that works once, for `PlayTicketMinutes`, and opens `ClientUrl` with
  it in a frame. Each Play replaces the player's previous ticket, the same one-per-player ticket
  as the panel's **Login ticket** card, so a ticket issued from the panel is replaced too. A
  banned player is told so instead of getting a ticket.
- **Links.** `player_discord_links` ties one Discord account to one player. The player's Discord
  username is refreshed on each sign-in.

A name follows the same rules as the panel's **New player**: 3 to 15 letters, digits and
`- = ? ! @ : . , _`, not starting with `@`, and nobody else's, whatever the capitals. When their
Discord name doesn't fit, the suggestion drops the characters that aren't allowed. If that name is
taken, a number is added to it, and `Player` is the last resort. People can always type their own.

## Setting it up locally

1. Apply the migrations. The server doesn't apply them on startup:

   ```bash
   cd Turbo.Database && dotnet ef database update
   ```

   `AddPublicSite` adds `player_discord_links` and `web_sessions`. The `AddTicketExpiry` migration
   before it adds `security_tickets.expires_at`; without it, every game login fails.
2. **Make a Discord application** at <https://discord.com/developers/applications>. Under
   **OAuth2**:
   - copy the **Client ID** and reset the **Client Secret**;
   - add the redirect `http://localhost:5175/api/auth/discord/callback`.
3. **Configure Turbo** in `appsettings.Development.json`:

   ```json
   "Web": {
       "Enabled": true,
       "ClientUrl": "http://localhost:3000/?sso={ticket}",
       "Discord": { "ClientId": "...", "ClientSecret": "..." }
   }
   ```

   Put it under `Turbo`. Keep the secret out of the repository: use your local settings file or
   the `TURBO__Turbo__Web__Discord__ClientSecret` environment variable.
4. **Run the site:**

   ```bash
   cd turbo-web
   yarn install
   yarn dev        # http://localhost:5175, /api proxied to 127.0.0.1:8092
   ```

## Setting it up in production (Ploi)

The site runs as its own Ploi site on the public domain, next to the Turbo site and the panel
site.

1. **On the Turbo site,** add to the **Environment** tab, then deploy:

   ```
   TURBO_WEB_ENABLED=true
   TURBO_WEB_SITE_URL=https://example.com
   TURBO_WEB_HOTEL_NAME=Turbo
   TURBO_WEB_CLIENT_URL=https://play.example.com/?sso={ticket}
   TURBO_DISCORD_CLIENT_ID=...
   TURBO_DISCORD_CLIENT_SECRET=...
   ```

   The Discord application's redirect must be `https://example.com/api/auth/discord/callback`.
   That is `TURBO_WEB_SITE_URL` plus `/api/auth/discord/callback`, exactly. Apply the migrations
   before the first sign-in.
2. **The site** is created in Ploi for `example.com`, from the `turbo-web` repository. Set it up
   like the panel site ([admin-panel.md](admin-panel.md#2-the-panel-site)):
   - a certificate;
   - Node 22;
   - the deploy script `bash scripts/ploi/deploy.sh`;
   - nginx from `turbo-web/scripts/ploi/nginx.conf`, with the `root`, the `/api/` `proxy_pass`
     (`http://127.0.0.1:8092`) and the hosts in its `Content-Security-Policy` set to yours.
   Delete Ploi's own `location /` block here too.
3. **The client's host must allow being framed by the site.** Its nginx sends
   `Content-Security-Policy: frame-ancestors https://example.com` and no
   `X-Frame-Options: DENY`/`SAMEORIGIN`. Otherwise the browser shows an empty frame and the
   console says it "set 'X-Frame-Options' to 'sameorigin'". Ploi's default site config sends that
   header, so in the client site's NGINX configuration replace
   `add_header X-Frame-Options "SAMEORIGIN";` with
   `add_header Content-Security-Policy "frame-ancestors https://example.com" always;`. The client
   must also log in with the ticket from its address (`?sso=`).
4. **Behind Cloudflare,** follow [cloudflare.md](cloudflare.md) for this site as for the panel.
   Without the real visitor address, everyone shares one sign-in rate limit.

## Accounts in the admin panel

Players made on the site are ordinary players. In the panel, staff with `admin.players.view` can:

- find them by Discord username (the **Discord** search mode);
- see a **Discord** badge in the list;
- see on their page which Discord account they sign in with and since when, and how many site
  sign-ins they have.

Staff with `admin.accounts.manage` can also, for themselves or for a player whose every
permission they hold:

- **sign them out of the site everywhere.** This ends their site sign-ins. Their Discord link and
  game sessions stay.
- **unlink Discord.** This also ends their site sign-ins. Signing in with that Discord account
  again then starts a new player, and the player can still log in with a ticket. To stop someone
  playing, ban them: the site then refuses Play.

Creating players and issuing tickets in the panel work as before, for players with or without
Discord. See [admin-panel.md](admin-panel.md#creating-players).

## Configuration

All settings live under `Turbo:Web`.

| Setting | Default | Ploi `.env` | Meaning |
| --- | --- | --- | --- |
| `Enabled` | `false` | `TURBO_WEB_ENABLED` | Turns the API on. |
| `Url` | `http://127.0.0.1:8092` | `TURBO_WEB_URL` | Where the API listens. Keep it on loopback behind nginx. |
| `SiteUrl` | `http://localhost:5175` | `TURBO_WEB_SITE_URL` | The site's address. Discord sends people back to it, sign-ins end on it, and cookies are marked secure when it is https. |
| `HotelName` | `Turbo` | `TURBO_WEB_HOTEL_NAME` | The hotel's name, shown on the site. |
| `ClientUrl` | empty | `TURBO_WEB_CLIENT_URL` | The client's address with `{ticket}` where the ticket goes. Empty, Play says the hotel can't be opened. |
| `AvatarImageUrl` | empty | `TURBO_WEB_AVATAR_IMAGE_URL` | A picture of a player's look, with `{figure}`. Empty shows their initials. |
| `RegistrationOpen` | `true` | `TURBO_WEB_REGISTRATION_OPEN` | Whether a Discord account without a player may make one. Existing players can sign in either way. |
| `SessionDays` | `30` | | How long a sign-in to the site lasts. |
| `PlayTicketMinutes` | `5` | | How long Play's ticket works. Its login uses it up anyway. |
| `SignUpMinutes` | `15` | | How long someone has to choose a name after their first Discord sign-in. |
| `SignInAttemptsPerMinute` | `20` | | Sign-in and sign-up requests per address per minute. |
| `Discord:ClientId` | empty | `TURBO_DISCORD_CLIENT_ID` | The Discord application's client id. |
| `Discord:ClientSecret` | empty | `TURBO_DISCORD_CLIENT_SECRET` | Its secret. Without both, the site says Discord sign-in isn't available. |

## Things to know

- **A sign-up in progress lives in the server's memory.** If the server restarts while someone is
  choosing a name, they sign in with Discord again.
- **One Discord account, one player.** The site never asks for an email or password. Losing
  the Discord account means losing the way in; staff can still issue a ticket from the panel.
- **The API answers only `/api`.** It has no CORS. The site calls it on its own origin through
  nginx, the same as the panel.
