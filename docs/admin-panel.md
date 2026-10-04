# Admin panel

The admin panel lets staff run the hotel from a browser. It has a live dashboard, a room finder,
a permission editor, a console for operator commands, and a page for managing staff passkeys. Staff sign in with a **passkey only**:
there are no passwords.

It comes in two parts:

| Part | Where | What it is |
| --- | --- | --- |
| Admin API | `Turbo.Admin`, in this repository | An HTTP API inside the Turbo server, beside the Orleans silo. It calls grains, the command runner and the session gateway directly. |
| Panel | the `turbo-admin` repository, a submodule at `turbo-admin/` here | A React app (static files) that talks to the API. |

```
browser ──https──▶ admin.example.com (nginx)
                      ├─ /        → turbo-admin's built files
                      └─ /api/*   → Turbo admin API (127.0.0.1:8090)
                                       └─ grains, command runner, database
```

The panel and its API share one address, so the browser makes no cross-origin requests, and
passkeys are bound to the panel's domain.

`turbo-admin/` is a **git submodule**: the panel's own repository
([nitrodevco/turbo-admin](https://github.com/nitrodevco/turbo-admin)), with its own history. This
repository records only which panel commit goes with it.

- Clone both at once with `git clone --recurse-submodules`, or after a plain clone run
  `git submodule update --init`.
- Commit and push panel changes from inside `turbo-admin/`, then commit the new `turbo-admin`
  pointer here, so this repository points at that panel commit. Push the panel first: a pointer to
  a commit that isn't on GitHub can't be fetched.
- `git pull` here doesn't move the panel. Run `git submodule update` after pulling to check out
  the panel commit this repository points at.

The Turbo server's Ploi deploy doesn't need the panel. The panel's own Ploi site deploys from its
own repository.

## Setting it up locally

1. **Turn on the API.** In `appsettings.Development.json`:

   ```json
   "Turbo": {
       "Admin": {
           "Enabled": true
       }
   }
   ```

   The API listens on `http://127.0.0.1:8090`. `PanelUrl` defaults to `http://localhost:5173`,
   where the panel's dev server runs.

2. **Apply the migrations.** The admin panel adds the `admin_passkeys` table:

   ```bash
   cd Turbo.Database
   dotnet ef database update
   ```

3. **Give yourself access.** Start Turbo, then at its console:

   ```
   perm user <your name> set admin.panel
   perm user <your name> set admin.passkeys.reset
   adminsetup <your name>
   ```

   The last command prints a setup link. If you're in the `admin` group, its `*` already covers
   both permissions.

4. **Start the panel.** In `turbo-admin`:

   ```bash
   yarn install
   yarn dev
   ```

   The dev server proxies `/api` to `127.0.0.1:8090`, just like the deployed site does.

5. **Open the setup link.** It points at `http://localhost:5173/setup#token=...`. Create your
   passkey; you are signed in. Passkeys work on `localhost` without HTTPS.

## Setting it up in production (Ploi)

The panel runs as its own Ploi site on a subdomain, for example `admin.example.com`, next to the
Turbo site that runs the server (see [`scripts/ploi/README.md`](../scripts/ploi/README.md)).

### 1. The Turbo site

In the Turbo site's **Environment** tab, add:

```
TURBO_ADMIN_ENABLED=true
TURBO_ADMIN_PANEL_URL=https://admin.example.com
```

Then deploy. The deploy applies the `admin_passkeys` migration, and Turbo starts the API on
`127.0.0.1:8090`.

- `TURBO_ADMIN_PANEL_URL` must be the panel's exact address. Passkeys are bound to its domain,
  setup links point to it, and the API accepts browser requests only from it.
- Leave the API on loopback (`TURBO_ADMIN_URL`, default `http://127.0.0.1:8090`). Only the
  panel's nginx should reach it. Don't open port 8090 in the firewall.

### 2. The panel site

1. **Create the site** in Ploi for `admin.example.com`, installed from the `turbo-admin`
   repository.
2. **Issue a certificate** in the site's SSL tab. Passkeys require HTTPS.
3. **Install Node 22 or newer** on the server (Ploi > Server > Manage > Node.js). Yarn comes
   with Node, through Corepack.
4. **Deploy script** (Site > Deployment):

   ```bash
   cd {SITE_DIRECTORY}
   git pull origin {BRANCH}
   bash scripts/ploi/deploy.sh
   ```

   This builds the panel into `dist/`.
5. **nginx** (Site > Manage > Edit NGINX configuration): keep Ploi's server block, SSL lines and
   includes. Replace its `root` line and `location /` block with the contents of
   `turbo-admin/scripts/ploi/nginx.conf`, and set:
   - `root` to the site's `dist` folder, for example `/home/ploi/admin.example.com/dist`;
   - the `/api/` `proxy_pass` to Turbo's API. Use `http://127.0.0.1:8090` when Turbo runs on the
     same server; otherwise use its private address, and set `TURBO_ADMIN_URL` to listen there.
6. **Deploy** the panel site.

### 3. Behind Cloudflare

If the panel's DNS record is proxied (orange cloud), nginx sees Cloudflare's address instead of
the visitor's. That puts every staff member in one sign-in rate limit. Uncomment the
`real_ip_header` block in the nginx config and list Cloudflare's IP ranges
(<https://www.cloudflare.com/ips/>). Set Cloudflare's SSL/TLS mode to **Full (strict)**.

### 4. The first admin

The first admin needs a setup link, which only the server console can make for someone without
a passkey (or you can make one for yourself in the hotel). Either:

- **In the hotel**, if your account already holds `admin.panel`: type `:adminsetup` and open the
  link it gives you.
- **At the server console.** A Ploi daemon has no terminal, but Supervisor can attach one. SSH in
  as a user with sudo, then:

  ```bash
  sudo supervisorctl status                 # find the Turbo daemon's program name
  sudo supervisorctl fg <program name>      # attach to its console
  ```

  Type:

  ```
  perm user <name> set admin.panel
  perm user <name> set admin.passkeys.reset
  adminsetup <name>
  ```

  Copy the link from the output. Press Ctrl+C to detach; Turbo keeps running.

Open the link on the device you want to sign in with, and create your passkey.

## Signing in

Open the panel and press **Sign in with passkey**. Your device lists the panel passkeys it holds;
pick yours and confirm with your fingerprint, face or PIN. There is no name or password to enter.

A session lasts 12 hours. Signing out, a server restart, or losing `admin.panel` ends it.

## Adding staff

1. Give them access: `perm user <name> set admin.panel`, at the console or with an in-game
   `:perm` command.
2. Make their setup link: on the panel's **Staff** page, enter their name. In the hotel, an admin
   can use `:adminsetup <name>` instead.
3. Send them the link privately. It works **once**, for **24 hours**. Whoever opens it first
   creates the passkey that signs in as them.

Staff should add a **second passkey** (a phone, or a second device) on the **Account** page. Adding
one asks them to confirm with a passkey they already have. With two, losing one device doesn't
lock them out.

## Lost passkeys

There is no "forgot password". When someone loses their only passkey:

1. An admin with `admin.passkeys.reset` makes them a new link on the **Staff** page. The page
   says it's a **reset**.
2. They open it and create a new passkey. **All their old passkeys are removed**, so a lost or
   stolen device can no longer sign in.

If the person has a passkey left, they don't need a reset: they sign in with the one they have
and remove the lost one on the Account page.

## Rooms

Staff with `admin.rooms.view` get a **Rooms** page. It's read-only for now.

- **Search** every room, **invisible ones included**, by part of its name, by the start of its
  owner's name, or by id. The search ignores case. Results come 25 to a page, most recently
  active first, with each room's access mode and its live population if it's loaded.
- **The room page** shows:
  - its settings: door mode, whether it has a password (never the password itself), maximum
    players, trading, pets, walk-through, who can mute, kick and ban, chat flood protection,
    category, tags, staff pick, model, and when it was created and last active;
  - **who is in it right now**, refreshed every 10 seconds;
  - who holds rights, and active bans with their end dates.
- The dashboard's busiest rooms link to their room pages.

Looking at a room never loads it. Settings, rights and bans are read from the database, and
who's inside comes from the room directory, which tracks every loaded room. A room that isn't
loaded has nobody in it.

Editing settings, moderating a room (kick, ban, mute, unload, alert), chat and visit logs,
deleting rooms and changing owners aren't in the panel yet.

## Permission editor

Staff with `admin.permissions.view` get a **Permissions** page: the panel's version of the `perm`
console command (see [permissions.md](permissions.md)). Its tabs:

- **Groups**: every group, heaviest first. A group's page shows its own nodes and meta, what it
  inherits and what inherits it, its members, its history, and the client level a member gets
  (with anything the client will show that the server refuses).
- **Players**: everyone in a group besides `default`, and any player by name. A player's page
  shows their groups (and the ones they reach through inheritance), their own nodes and meta,
  everything it resolves to, their client level, their history, and a **Check a node** box that
  explains why they do or don't hold a node, as `perm check` does.
- **Who has a node**: every group and player given a node, exactly or by wildcard (`perm search`).
- **Log**: recent changes to anyone, searchable by node, key or group (`perm log`).
- **Nodes**: every registered node and meta key, with what it does.

Viewing changes nothing. **Changing** permissions also needs `permissions.manage`, and follows the
rule [permissions.md](permissions.md) sets for any editor outside the console: **you only hand out
what you have, and only to people below you.**

| You may | Only when |
| --- | --- |
| Edit a group (nodes, meta, name, weight, parents) or delete it | Its weight is **below your heaviest group's** |
| Create a group, or change a group's weight | The new weight is below your heaviest group's |
| Make a group inherit another | Both are below your heaviest group |
| Change a player (groups, nodes, meta) | Their heaviest group is **below yours**, so never yourself or your equals |
| Put a player in a group, or take them out | The player and the group are both below you |
| Grant, deny or unset a node | **You hold the node yourself**; for a wildcard, every node it covers |

"Your heaviest group" counts groups you reach through inheritance. So the top group (seeded as
`admin`, weight 100) and its members can only be changed from the server console: nobody outranks
them in the panel. Anything the panel refuses, the console can still do.

Every change is made as you: the permission log records your name, and the player's client and
room learn of it at once, exactly as with `perm`. Durations work as in the console (permanent, or
from an hour to 90 days in the panel); a temporary assignment sits beside a permanent one and wins
while it lasts. **Extend** adds the time to one already running instead of replacing its end.

The panel doesn't reload permissions from the database (`perm reload`) or turn on `verbose`; use
the console for those.

## Who can make links for whom

A setup link gives whoever opens it the account, so it's guarded:

| Who asks | For whom | Allowed? |
| --- | --- | --- |
| Server console | Anyone | Always |
| A player, for themselves | Themselves | Only while they have no passkey yet (first setup) |
| A player, for someone else | Another player | Only with `admin.passkeys.reset`, and only if **every** permission the other player has is one the asker has too |

The last rule stops privilege escalation: a moderator with `admin.passkeys.reset` can reset a
helper, but not the owner, who can do things the moderator can't. Nobody can reset their own
passkey; they add more while signed in instead.

## Permissions

| Node | Allows |
| --- | --- |
| `admin.panel` | Signing in to the panel. Checked on every request, so removing it locks the person out at once. |
| `admin.passkeys.reset` | Making setup and reset links for other players (the Staff page, `:adminsetup <name>`), within the rule above. |
| `admin.rooms.view` | The Rooms page: finding any room and seeing its settings, who is inside, rights and bans. |
| `admin.permissions.view` | The Permissions page: seeing groups, any player's permissions, who has a node, and the permission log. |
| `permissions.manage` | Changing permissions on that page, within the rule in [Permission editor](#permission-editor). The in-game `:group` command needs it too. |

Everything else in the panel uses each command's own permission. The console runs commands
**as you**, with exactly your permissions, rate limits and confirmations, and logs them like
commands typed in the hotel. Room commands (`kick`, `mute`, ...) need a room, so the console
can't run them.

## Commands

| Command | Who | What |
| --- | --- | --- |
| `adminsetup` | A player with `admin.panel` and no passkey yet | Prints a link to make their first passkey. |
| `adminsetup <name>` | Server console, or a player with `admin.passkeys.reset` | Prints a setup or reset link for that player. |

## Configuration

All settings live under `Turbo:Admin`. On Ploi, the `.env` names in the right-hand column set
them.

| Setting | Default | Ploi `.env` | Meaning |
| --- | --- | --- | --- |
| `Enabled` | `false` | `TURBO_ADMIN_ENABLED` | Turns the API on. |
| `Url` | `http://127.0.0.1:8090` | `TURBO_ADMIN_URL` | Where the API listens. Keep it on loopback behind nginx. |
| `PanelUrl` | `http://localhost:5173` | `TURBO_ADMIN_PANEL_URL` | The panel's address: passkey domain, setup links, allowed browser origin. |
| `PasskeyRpId` | the `PanelUrl` host | `TURBO_ADMIN_PASSKEY_RP_ID` | Binds passkeys to a parent domain (`example.com`) instead. Every subdomain could then ask for them; leave it empty unless you need that. |
| `SetupLinkHours` | `24` | | How long a setup link works. |
| `SessionHours` | `12` | | How long a sign-in lasts. |
| `MaxSessionsPerPlayer` | `5` | | Sessions per person; signing in past it ends their oldest. |
| `SignInAttemptsPerMinute` | `10` | | Sign-in and setup requests per address per minute. |
| `CeremonyMinutes` | `5` | | How long the browser has to answer a passkey prompt. |
| `MaxCommandLength` | `1000` | | The longest command line the console accepts. |
| `RoomSearchPageSize` | `25` | | Rooms per page on the Rooms page. |
| `RoomSearchMaxLength` | `64` | | The longest room search text; longer text is cut. |

The panel itself has one build setting, `VITE_API_URL`. Leave it unset, so the panel calls `/api`
on its own address. Set it only if the API is on a different origin; `PanelUrl` must then still
be the panel's address.

## Things to know

- **Changing the panel's domain** invalidates every passkey, since each passkey belongs to one
  domain. After moving, send everyone a setup link.
- **A server restart** signs everyone out and voids every open setup link. Passkeys themselves are
  stored in the database and keep working.
- **Passkey types.** Phones, laptops (Face ID, Touch ID, Windows Hello) and current security keys
  all work. Some very old security keys can't store the kind of passkey the panel uses
  (discoverable passkeys, so no name is needed); use another device for those staff.
- **Logging.** Turbo's log records every passkey setup and every link an admin makes, with who made
  it. Console commands go to the command log like in-game ones.

## Troubleshooting

| Symptom | Cause and fix |
| --- | --- |
| "Cannot reach the admin API" | The API is off or unreachable. Check `TURBO_ADMIN_ENABLED=true`, that Turbo is running, and the nginx `/api/` `proxy_pass` address. |
| The passkey prompt fails with a security or domain error | The panel is opened at an address other than `TURBO_ADMIN_PANEL_URL`, or without HTTPS. Use the exact panel address, over HTTPS (or `localhost`). |
| "You do not have access to the admin panel" | The passkey works, but the player lacks `admin.panel`. Grant it. |
| "That setup link has expired or was already used" | Links work once, for 24 hours, and a restart voids them. Make a new one. |
| "Too many attempts" | The per-address sign-in limit. Wait a minute. Behind Cloudflare, check that nginx restores the real client address. |
| "<name> has permissions you don't, so you can't set up their passkey" | The guard above. Ask someone with at least that player's permissions, or use the server console. |
| Signed out unexpectedly | The session expired (12 hours), the server restarted, or `admin.panel` was removed. |
