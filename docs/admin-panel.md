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

2. **Apply the migrations.** The admin panel adds the `admin_passkeys` table. Turbo migrates its
   own database when it starts (see [database.md](database.md)), so there is nothing to do; to
   do it ahead of time, run `dotnet run --project Turbo.Main -- migrate`.

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

   **Delete Ploi's own `location / { ... }` block**, and its `location ~ \.php$` block if there is
   one. The site must have exactly one `location /`, the one from `nginx.conf`, which sends
   every path that isn't a file (`/login`, `/rooms/12`) to the panel. With Ploi's block left in,
   either nginx refuses the config for a duplicate `location /` and keeps running the old one, or
   Ploi's `try_files ... /index.php` or `=404` wins. Either way, opening `/login` directly gives a
   404.

   After saving, check on the server that the config took:

   ```bash
   sudo nginx -t        # must report ok; if not, nginx is still running the old config
   ```
6. **Deploy** the panel site.

### 3. Behind Cloudflare

If the panel's DNS record is proxied (orange cloud), follow [cloudflare.md](cloudflare.md). It
covers:

- **the visitor's real address.** Without it, nginx sees Cloudflare's address, and every staff
  member shares one sign-in rate limit;
- **refusing anyone who isn't Cloudflare.** The panel then can't be reached at the server's own
  address, around Cloudflare;
- **SSL/TLS mode Full (strict),** and keeping Cloudflare from caching the panel's pages.

Those settings go in `/etc/nginx/ploi/admin.example.com/server/cloudflare.conf` and
`/etc/nginx/conf.d/cloudflare.conf`, not in the site's NGINX configuration in Ploi, so leave the
real-IP lines in `nginx.conf` commented out.

### 4. The first admin

Name the owner in the site's environment, and the hotel does the rest:

```
TURBO_OWNER_DISCORD_ID=123456789012345678   # or TURBO_OWNER_NAME=YourName
```

Sign up as that Discord account (or name). Turbo puts you in the `admin` group, gives you
`permissions.superuser` (so you can make other admins, see [Permission editor](#permission-editor)),
and prints your setup link in the server log, with the Ploi daemon's log or `journalctl`:

```
Admin panel setup for the owner, YourName: open https://admin.example.com/setup#token=… on the
device you will sign in with. It works once, until 2026-10-08 12:00 UTC. …
```

Open it on the device you sign in with and create your passkey. If you signed up before setting
the variable, restart Turbo: it finds you at startup. It never makes anyone the owner for being
first on a live hotel. A name can be taken by whoever signs up first, so on a public hotel use the
Discord id, or set `TURBO_WEB_REGISTRATION_OPEN=false` until you have signed up.

Locally, in the Development environment with no owner named, the first player created becomes the
owner (`Turbo:Owner:FirstPlayerInDevelopment`, on by default).

Without naming an owner, or to rescue a hotel, the setup link can be made by hand. Either:

- **In the hotel**, if your account already holds `admin.panel`: type `:adminsetup` and click the
  link in the pop-up it opens.
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

## Hotel controls

The dashboard has a **Hotel controls** card for acting on the whole hotel. Each tab only appears
if you hold the node behind it:

| Tab | Node | What it does |
| --- | --- | --- |
| Hotel alert | `command.hotelalert` | A pop-up for everyone online. |
| Maintenance | `command.maintenance` | Counts down (now, 5, 10 or 30 minutes), then sends home everyone without the bypass and keeps them out. **End maintenance** appears while it is scheduled or on. |
| Shutdown | `command.shutdown` | Counts down, sends everyone home and stops the server. It does not start again by itself. **Call it off** appears while one is scheduled. |
| Welcome message | `admin.welcome.manage` | The message every player is shown when they log in, as the message of the day. Saving it empty turns it off. |

Like the player and room actions, each of the first three is the hotel's own command
(`hotelalert`, `maintenance`, `shutdown`) run as you, so the same permissions, confirmations and
command log apply. If the command asks to be confirmed, the card shows a **Confirm** button. The
reason you type is shown to players and is flattened to one line.

The welcome message is kept in the `hotel_settings` table (the `AddHotelSettings` migration) and
read from memory at each login, so a change applies from the next login on; players already
online are not shown it. It keeps its line breaks, and can be up to 4000 characters.

## Rooms

Staff with `admin.rooms.view` get a **Rooms** page for finding and inspecting rooms. Staff who
hold the room permissions below can also edit and moderate them there.

- **Search** every room, **invisible ones included**, by part of its name, by the start of its
  owner's name, or by id. The search ignores case. Results come 25 to a page, most recently
  active first, with each room's access mode and its live population if it's loaded.
- **The room page** shows:
  - its settings: door mode, whether it has a password (never the password itself), maximum
    players, trading, pets, walk-through, who can mute, kick and ban, chat flood protection,
    category, tags, staff pick, model, and when it was created and last active;
  - **who is in it right now**, updated live as people come and go;
  - who holds rights, and active bans with their end dates.
- The dashboard's busiest rooms link to their room pages.

Looking at a room never loads it. Settings, rights and bans are read from the database, and
who's inside comes from the room directory, which tracks every loaded room. A room that isn't
loaded has nobody in it.

### Editing and moderating a room

The panel asks for the permissions the hotel asks for, so staff can do from the browser exactly
what they could do standing in the room, and the room applies its own rules (who outranks whom,
group rooms' rights, the settings' limits). Buttons only appear for what you may do.

| Action | Needs | Room must be loaded |
| --- | --- | --- |
| **Settings** (a tab on the room page): name, description, category, tags, door and password, maximum players, trading, pets, walk-through, walls, who can mute, kick and ban, chat flood protection, idle players | Owning the room, or `room.control.any` | No |
| Remove one player's rights, or everyone's | Owning the room, or `room.control.any` | No |
| Kick, mute (up to an hour) or ban (an hour, a day or for good) a player; lift a ban | Owning the room, or `room.moderate.any` (staff with it act on anyone who lacks it) | Kick and mute: yes. Ban and lift: no |
| Staff pick | `navigator.staff_pick` | No |
| Mute the whole room | `command.roommute` or `command.roomunmute` | Yes |
| Kick everyone (its owner and room moderators stay) | `command.roomkickall` | Yes |
| Unload (sends everyone out) | `command.unloadroom` | Yes |
| Room alert (a pop-up for everyone inside, up to 500 characters) | `command.roomalert` | Yes |

The settings are saved through the room itself, the same way the owner's settings dialog saves
them: values are kept within the hotel's limits, the navigator is updated, and everyone inside
sees the change at once. Staff may put a room in any category, staff-only ones included. A
password door keeps its password when you leave the password box empty; the panel never shows it.

Actions on the people inside a room need it loaded, and the panel refuses them for a room that
isn't. Editing the settings, rights or bans of an unloaded room loads it lightly (its details,
rights and bans, not its furniture), as visiting its settings in the hotel would.

Every action is written to Turbo's log as `Admin panel: <name> ...`, with who did it.

Chat and visit logs, deleting rooms and changing owners aren't in the panel yet.

## Players

Staff with `admin.players.view` get a **Players** page, for finding and looking at players, and
acting on them with the matching command permissions.

- **Search** every player by part of their name (case ignored), by id, or by part of the
  Discord username they sign in to the [public site](public-site.md) with. Players with a
  Discord account have a **Discord** badge. Results come 25 to a page, most recently logged in first; players who have never logged in come last. **Online
  only** shows just the players connected right now. The page also says how many are online.
- **A player's page** shows:
  - whether they're online, and if so which room they're in;
  - their balances (credits, duckets, diamonds, ...) and respect;
  - their sanctions, newest first: active, ended or lifted, why, by whom, and until when;
  - the rooms they own, most recently active first, with a link to all of them on the Rooms page;
  - their profile: motto, last login, when they joined, gender and figure;
  - a link to their permissions, for staff with `admin.permissions.view`.
- People in a room link to their player pages.

Looking at a player starts nothing for them: who is online comes from the open sessions, and only
an online player is asked which room they're in. Everything else is read from the database.

### Acting on a player

A player's page has an **Actions** card. Each action is the hotel's own operator command, run as
you, exactly as if you typed it in the hotel: it needs that command's permission, the command's
own guards apply (you can't ban or silence someone who outranks you), the player gets the same
notice, and it's written to the command log under your name. Only what you may do is shown.

| Action | Command | Needs |
| --- | --- | --- |
| Moderator warning, or a pop-up | `:warn`, `:alert` | `command.warn`, `command.alert` |
| Ban from the hotel (1 hour, 1 day, 7 days, for good, with a reason), lift a ban | `:ban`, `:unban` | `command.ban`, `command.unban` |
| Silence everywhere (10 min to 7 days), unsilence | `:silence`, `:unsilence` | `command.silence` |
| Trade lock (1 day to for good), unlock | `:tradelock`, `:untradelock` | `command.tradelock` |
| Give or take credits, duckets, diamonds, ... | `:give` | `command.give` |
| Disconnect (online players only) | `:disconnect` | `command.disconnect` |

The command's answer is shown under the card. A command that asks to be confirmed gets a
**Confirm** button. Messages need the player online, as in the hotel. A player whose name starts
with `@` or has a space can't be acted on from the panel, because the commands would misread it.
Use the console for them.

## Catalog

Staff with `admin.catalog.view` get a **Catalog** page: the catalog's page tree, with the normal
catalog and the Builders Club catalog on two tabs, and for each page its offers and its settings.
Staff who also hold `catalog.manage` can change it.

**Pages.** A page has a title, a name (the key the client opens it by, such as from a link), an
icon (the number of a catalogue `icon_<n>.png`), a layout, the layout's images and texts in
order, and whether it is shown. Pages move up and down among their siblings or under another page
of the same catalog. **Add a page under it** makes a hidden page, so it can be set up before anyone
sees it. Only an empty page can be deleted: move or delete its pages and offers first. The root
can't be moved or deleted.

**Offers.** Each offer opens in place:

- **What it gives:** a floor item, a wall item (picked by the start of its class name, or its id),
  a badge (its code), or a membership (see below), and how many.
  - An offer that gives something else (a pet, a bot, a membership, several things) keeps that
    as it is, and its other fields can still be changed.
  - The item must exist and be of the type chosen, or the save is refused. Otherwise every
    purchase would fail.
- **Name key:** the text key the client shows. Leave it empty to use the item's class name, as
  the hotel's own offers do.
- **Price:** credits, and an amount of an activity-point currency (duckets, diamonds, ...).
- **Rules:** who may buy it (anyone, club, VIP), whether it can be gifted or bought in bulk, and
  whether it is shown.
- **Page:** it can be moved to any page of the same catalog.

What other things depend on is protected:

- An offer that sells a **limited series** keeps its item (its price can change), and can't be
  deleted. Hide it instead. What is left of the series is never touched by the editor.
- A **club gift** offer, and one that **Builders Club furni** was placed from, can't be deleted
  either; hide them.

**Publishing.** Edits are saved straight away but players don't see them yet. **Publish** reloads
both catalogs (the same as `:reload catalog`) and tells every client online the catalog changed:
it drops what it has and shows "the catalog has been updated". The button shows how many edits
are waiting. A `:reload catalog` also puts them live, and a restart loads the catalog fresh.

**Hidden means hidden.** A hidden offer isn't on its page and can't be bought or raffled, even by
a client that still knows its id. Before this, it was still sold. A hidden *page* only leaves the
navigator: its offers stay on sale. That's on purpose, because the club window sells the
memberships and a hotel often keeps them on a page of their own out of the navigator.

**Habbo Club memberships.** An offer can give days of Habbo Club or Builders Club instead of an
item. Players buy Habbo Club from the club window, a page with the `club_buy` layout. The window
lists every shown membership offer in the normal catalog, wherever it sits, by length. Renewals
and the club centre sell them too. So, to sell memberships:

1. Have a shown page with the `club_buy` layout. If there are memberships but no such page, the
   Catalog page says so, with a button that adds one.
2. Add a membership offer for each length. On a `club_buy` page, **New offer** starts as a
   membership.
   - **Days:** 31 is a month.
   - **Name key:** left empty, it is named by length, as the hotel's own are
     (`habbo_club_3_months`).
   - **Price:** credits and an activity-point currency.
3. Publish.

A membership can't ask for club (non-members couldn't buy it), is bought one at a time, can't be
gifted yet (there is no gift path for memberships), and only lives in the normal catalog. Builders
Club days work the same way, from a Builders Club page.

**Club gifts.** Any furni offer in the normal catalog can be a club gift: members claim it for free.
Each member earns one gift per month of club used up, and each gift can ask for a number of club
days used up before it can be picked. Gifts are listed on a page with the `club_gifts` layout,
which lists every shown gift wherever it sits; there is a warning and a button when there is none.
A gift's name key is how members claim it, so no two gifts can share one. A gift can't be deleted,
since members may have claimed it; hide it, or turn **Club gift** off first.

**Limited series.** A saved offer of one floor or wall item in the normal catalog can be made
limited:

- **How many:** the size of the series.
- **Raffle window:** how long the opening raffle gathers buyers before it draws. 0 sells first
  come, first served.
- **On sale from / until:** a later start shows as the next limited item.
- **On sale:** a switch to stop selling it.

The editor never counts what is left; the hotel does. A new total moves what is left by as much,
and can't drop below what is sold. A series can only come off an offer while none of it is sold or
raffled; after that, switch it off. A club gift can't be limited. Saving tells an open raffle to
read the series again, so switching it off stops it at once. Players see the new numbers when you
publish.

**Currencies.** An offer's second price names a row of `currency_types`, and the hotel charges
that row's activity-point type. Duckets are row 4 but type 0, so earlier code that charged the row
number charged the wrong currency. The editor only offers activity-point currencies that are
enabled.

Page icons and images, furniture icons and badges load from the addresses in the client's own
`nitro-config.json` (`ClientConfigUrl`): `catalog.icons.url`, `asset.urls.catalog`,
`asset.urls.icons.furni` and `badge.asset.url`, resolved as the client resolves them. The server
reads the config (so its host needn't allow the panel's origin) and reads it again every
`ClientConfigCacheMinutes`, so a redeployed client is picked up without a restart. Without one
set, the editor shows no pictures. The layouts on offer are those the client has a window for (from
the Flash client's layouts, as nitro-next mirrors them); `CatalogLayouts` changes the list.

### Creating players

Staff with `admin.players.create` get a **New player** button on the Players page. A new player
needs:

- **A name:** 3 to 15 letters, digits and `- = ? ! @ : . , _`, with no spaces and not starting
  with `@` (commands read `@` as a group of players). It can't be anyone else's name, whatever the
  capitals.
- **A motto** of up to 38 characters, and a **gender**.
- **A figure.** Leave it empty for the gender's default look.

A player row is all a login needs: their wallet, settings and permissions are made the first time
they're used, and the default group is everyone's. Their page opens straight after, so you can
give them a login ticket.

### Login tickets

A login ticket is what the client logs in with (the SSO ticket). Staff with `admin.tickets.issue`
get a **Login ticket** card on a player's page:

- **Works for:** 15 minutes, 1 hour, 1 day or 7 days, or **never** runs out.
- **Can be used more than once:** off, the first login uses it up. On, it logs in every time
  until it runs out or is taken away.
- **Issue:** the ticket is shown once, with a copy button. If `ClientLoginUrl` is set, a ready
  login link is shown too. Nothing shows the ticket again; the card only says whether there is
  one and until when.
- **One per player:** a new ticket replaces the old one, which stops working at once.
- **Take it away:** the ticket stops working at once.

A ticket logs in *as* the player, so it is the account. You can issue one for yourself, or for a
player whose every permission you hold, the same rule as passkey setup links. A reusable ticket
that never runs out is a standing login; the card warns you, so keep it like a password.

Tickets are 32 random bytes and aren't tied to an address. Tickets written by something else (a
CMS, the load-test bots) work as before: without an end time, a plain one is used up by its first
login and a reusable ("locked") one never is. A plain ticket can only ever log in once, even if two
logins race with it.

### Discord accounts

Players who signed up on the [public site](public-site.md) have a **Discord** card on their page:
the Discord account they sign in with, since when, and how many site sign-ins they have now.
Staff with `admin.accounts.manage` can, for themselves or for a player whose every permission
they hold:

- **Sign out of the site everywhere:** ends their site sign-ins. Their Discord link stays, and so
  do their game sessions.
- **Unlink Discord:** removes the link and ends their site sign-ins. Signing in with that Discord
  account again then makes a new player. To keep someone out, ban them; the site then won't
  open the hotel for them.

These players are ordinary players otherwise. Creating players and login tickets work the same
for them.

## Command log

Staff with `admin.commandlog.view` get a **Command log** page: every logged command, newest first,
with when, who ran it, where (the room, or where it came from), what they typed after the command,
and how it went. A player's page links to their own commands.

Narrow it to one player (exact name or id), one command (`ban`, `:ban` and `Ban` all work), one
outcome (done, not allowed, failed, ...), or where it came from:

| Where from | Means |
| --- | --- |
| In game | An operator command typed in the hotel (`:ban` and the like) |
| Room chat | A room command typed in a room's chat |
| Admin panel | An action from the panel, or a line run in its console |
| Server console | Typed at the server's own console |

What gets logged is the hotel's rule, not the panel's: a command is logged when whoever ran it
holds `command.log`, and a command aimed at a group (`@room`, `@online`) always is. Room chat
commands are written in batches, so they can take a few seconds to appear; the page asks again
every 30 seconds.

The log is indexed by player and by command (migration `IndexCommandLogs`). The
server migrates itself when it starts ([database.md](database.md)), which adds them; the
page works without them, just more slowly on a large log.

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

**Superuser.** A player who holds both `permissions.manage` and `permissions.superuser` is bound by
none of the rows above: any group, any other player and any node. They are still audited. It is
how a hotel with no console to hand (a hosted one) gives the top group to a second admin. No
wildcard grants the node, not even `*`: it is given by naming it, from the console
(`perm user <player> set permissions.superuser true`), by another superuser, or by naming the
hotel's owner (see the first admin, above). A group that gives it, directly or through a parent,
can only be changed or joined by a superuser.

**You can't lock yourself out.** A superuser's own change is refused when it would leave *them*
without `permissions.superuser` and `permissions.manage`: deleting the group that gives it,
unsetting or denying it there, removing a parent it comes through, taking themselves out of that
group, or joining one that denies it. Whoever edits is always still a superuser afterwards, so the
panel and `:group` can't leave the hotel with none. Another superuser can still take it from you
(make someone else one first, then ask them), and the server console, which these rules don't
bind, is the way back.

The in-game `:group` command follows the same rule (it goes through the same
`IPermissionEditService`), and players are told when the panel puts them in a group or takes them
out, as `:group` tells them.

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
| `admin.players.view` | The Players page: finding any player and seeing their profile, wallet, rooms and sanctions. |
| `admin.commandlog.view` | The Command log page: every logged command, who ran it, where from, and how it went. |
| `admin.catalog.view` | The Catalog page: the catalog's pages, offers and prices. |
| `admin.players.create` | Creating new players on the Players page. |
| `admin.tickets.issue` | Issuing a player a login ticket, and taking it away, for players whose every permission they hold. |
| `admin.accounts.manage` | Unlinking a player's Discord and signing them out of the public site, for players whose every permission they hold. |
| `admin.welcome.manage` | The Welcome message tab in Hotel controls: seeing and changing the message every player is shown when they log in. |
| `catalog.manage` | Changing the catalog on that page, and publishing it to players. |
| `admin.permissions.view` | The Permissions page: seeing groups, any player's permissions, who has a node, and the permission log. |
| `permissions.manage` | Changing permissions on that page, within the rule in [Permission editor](#permission-editor). The in-game `:group` command needs it too, and follows the same rule. |
| `permissions.superuser` | With `permissions.manage`: lifts the weight and held-node limits of the permission editor (see [Permission editor](#permission-editor)). No wildcard grants it. |

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
| `PlayerSearchPageSize` | `25` | | Players per page on the Players page. |
| `CommandLogPageSize` | `50` | | Entries per page on the Command log page. |
| `ClientConfigUrl` | empty | `TURBO_ADMIN_CLIENT_CONFIG_URL` | The client's `nitro-config.json`, e.g. `https://play.example.com/config/nitro-config.json`. Catalog icons and images, furniture icons and badges load from its addresses. Empty shows none. |
| `ClientConfigCacheMinutes` | `10` | | How long those addresses are kept before the config is read again. |
| `CatalogLayouts` | the client's layouts | | The page layouts the catalog editor offers, besides any a page already uses. |
| `CatalogFurnitureSearchLimit` | `25` | | Items the catalog editor's item picker lists at once. |
| `ClientLoginUrl` | empty | | The client's login address with `{ticket}` where the ticket goes, e.g. `https://hotel.example.com/client?sso={ticket}`. Set, a new ticket also comes as a login link. |
| `TicketMaxLifetimeDays` | `365` | | The longest a ticket from the panel can work for, short of never. |
| `RoomAlertMaxLength` | `500` | | The longest room alert the panel sends. |
| `RoomMuteMaxMinutes` | `60` | | The longest the panel mutes a player in a room. |
| `LiveBatchMs` | `1000` | | How long the live stream gathers changes before sending them. |
| `LiveHeartbeatSeconds` | `25` | | How often a quiet live stream sends a keep-alive and checks the session. Keep it under your proxy's read timeout (nginx 60s, Cloudflare 100s). |

The panel itself has one build setting, `VITE_API_URL`. Leave it unset, so the panel calls `/api`
on its own address. Set it only if the API is on a different origin; `PanelUrl` must then still
be the panel's address.

## Live updates

The panel keeps one stream open to the API (`/api/live`, server-sent events) and fetches a page's
data again when the hotel says it changed, instead of asking every few seconds:

| What happens in the hotel | What the panel fetches again |
| --- | --- |
| A player logs in or their connection goes | The dashboard, that player's page, any player list showing them |
| A room loads, unloads or changes its settings | The dashboard, that room's page, any room list showing it |
| A player enters or leaves a room | The same, and that player's page |
| A player is banned or their ban is lifted | That player's page, and any player list showing them |
| Maintenance or a shutdown is scheduled, starts or is called off | The dashboard |
| What a player holds changes | Their permissions page, and the staff list |

Changes are gathered for a second (`LiveBatchMs`), so a busy hotel sends one message a beat rather
than one per footstep. Staff are only told the room and player ids they may see: someone without
`admin.rooms.view` hears that the dashboard moved, not which room. While the stream is up the
dashboard shows **LIVE**, and pages still ask once a minute for what no change is sent about (memory,
uptime, room bans, a ban running out). If the stream drops, the panel goes back to asking every 10 to 15
seconds and reconnects on its own. When it is back, it fetches what is on screen once to catch up.

A stream is checked at every keep-alive (`LiveHeartbeatSeconds`): signing out, the session
expiring or losing `admin.panel` ends it.

The stream turns nginx's buffering off itself (`X-Accel-Buffering: no`), so the `/api/` block
needs nothing extra. Its keep-alive comes well inside nginx's 60-second `proxy_read_timeout` and
Cloudflare's 100-second limit. The hotel runs as one server today; the stream only hears about
players connected to the server the panel talks to.

## Things to know

- **The database is migrated when Turbo starts** (`Turbo:Database:Migrate`, `Auto` by default; see
  [database.md](database.md)), so new code never meets an old schema. With `Migrate` set to
  `Check` or `Off`, apply the migrations first (`Turbo.Main migrate`, or your deploy step): for
  example `AddTicketExpiry` adds the column login reads, and without it **every game login
  fails** with "Unknown column 'expires_at'".

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
| Opening `/login` (or any page but `/`) directly gives a 404 | Ploi's own `location /` block is still in the site's nginx config, or `nginx -t` fails and nginx is running the old config. See step 5 of the Ploi setup. Behind Cloudflare, a 404 cached from earlier: purge it ([cloudflare.md](cloudflare.md)). |
| The panel can still be reached at the server's own address | The site doesn't include the Cloudflare-only rule, or the reload failed. See [cloudflare.md](cloudflare.md). |
| "Cannot reach the admin API" | The API is off or unreachable. Check `TURBO_ADMIN_ENABLED=true`, that Turbo is running, and the nginx `/api/` `proxy_pass` address. If Turbo's log says **"Admin API could not listen on ..."**, something else holds that port: the hotel kept running without the panel. Free the port or change `TURBO_ADMIN_URL`, then restart Turbo. |
| The passkey prompt fails with a security or domain error | The panel is opened at an address other than `TURBO_ADMIN_PANEL_URL`, or without HTTPS. Use the exact panel address, over HTTPS (or `localhost`). |
| "You do not have access to the admin panel" | The passkey works, but the player lacks `admin.panel`. Grant it. |
| "That setup link has expired or was already used" | Links work once, for 24 hours, and a restart voids them. Make a new one. |
| "Too many attempts" | The per-address sign-in limit. Wait a minute. Behind Cloudflare, check that nginx restores the real client address. |
| "<name> has permissions you don't, so you can't set up their passkey" | The guard above. Ask someone with at least that player's permissions, or use the server console. |
| Signed out unexpectedly | The session expired (12 hours), the server restarted, or `admin.panel` was removed. |
