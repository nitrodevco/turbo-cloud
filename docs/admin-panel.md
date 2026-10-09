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

## Performance

Everyone who can sign in gets a **Performance** page: how this server has been running over the
last hour, six hours or day. It has charts (each with a table view) of:

| Chart | What it is |
| --- | --- |
| CPU | The process's share of every core. |
| Memory | What the process holds, and the .NET heap within it. |
| Players online, Rooms loaded | Open sessions, and rooms running. |
| Room entry time | Median and p95 of entering a room, from asking to being in it, on the server. |
| Room updates reaching players | p95 of a room's update reaching a player's session. |
| Thread pool queue | Work waiting for a thread. A queue that stays up means the server is behind. |
| Garbage collection pauses | Share of the time the process was paused to collect garbage. |

Below them, **Room operations** lists every timed operation (each entry and loading step, chat
commands, update delivery) with how often it ran and its median, p95 and longest time.

The figures are taken inside the server every `PerformanceSampleSeconds` and kept in memory for
`PerformanceHistoryHours`. Nothing is written to the database, and a restart starts them again.
The timings are the hotel's own room and command measurements, which normally only run when
OpenTelemetry export is on; the page listens to them in the process, so they run whenever the
admin API is on. A day at 10 seconds is 8,640 samples, which the server merges into at most
`PerformanceMaxPoints` points per chart. With more than one silo, the page shows the silo that
serves the admin API.

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

Staff with `admin.catalog.view` get a **Catalog** page. Its header has four views: **Editor**,
**Missing furni**, **Duplicates** and **Generate**. The editor is in three columns:

- **The page tree** on the left, as the client's navigator shows it: tabs as section rows, a guide
  line per level, the front page, hidden pages and pages in the Builders Club catalog marked, and
  each page's offer count. **Open every page** and **close every page** sit by the search, which
  lists every page whose title or link key matches, with where it sits.
- **The page** in the middle, drawn roughly as the client draws it: the header with its banner,
  icon, title and line, then what its layout shows (the offer grid and the big preview, the
  featured items, or its words and pictures). It follows the fields as they are typed.
- **The editor** on the right: the page, the offer picked, or the front page's featured items.

With no page open, the middle shows the catalog at a glance: its tabs, whether it has a front page
(with a button that makes one), how many furni it doesn't sell, how many it sells twice, and a way
into generating a catalog.

Staff who also hold `catalog.manage` can change it:

- **Drag a page** by its row to move it: up and down among the others, sideways to put it under
  the page above or take it back out. From the keyboard, Space on its grip picks it up.
- **Drag an offer's tile** to put it elsewhere on its page (the client shows them in that order),
  or onto a page in the tree to move it there.
- **Click a picture or a text** in the page to jump to its field.
- **Ctrl+S** saves what is open; **Duplicate** copies an offer beside it.

A move the server refuses springs back, with a note saying why.

**Pages.** A page has:

- a title, a link key (the name the client opens it by) and an icon, picked from the client's
  `icon_<n>.png`;
- where it is shown: the normal catalog, both, Builders Club only, or hidden;
- a layout, picked from every layout the client draws, each described with the pictures and
  words it takes;
- the layout's pictures and words, each named for where it shows ("Header banner", "Teaser
  image", "Description"...).

Values a page keeps at places its layout doesn't show are listed apart, to clear. **Add a page
under it** (or the + on a tree row) makes a hidden page, so it can be set up before anyone sees
it. Only an empty page can be deleted: move or delete its pages and offers first. The root can't
be moved or deleted.

**Link keys.** The client's own buttons open pages by fixed link keys, so the page that should
open needs that key. The link key field lists them with what opens each, and still takes a key of
your own for links:

| Link key | Opened by |
| --- | --- |
| `hc_membership` | The club shop: the toolbar, the club centre, quests and featured items. Buying a membership is bought from it. |
| `club_gifts` | The club gifts: the club centre and the gift notice. |
| `credits` | Buying credits: the me menu and credit links. |
| `ducket_info`, `loyalty_info` | The purse's duckets and loyalty points. |
| `avatar_effects` | The me menu's effects. |
| `new_additions` | Opened when the catalogue first opens, while that is on. |
| `limited_sold` | Shows the sold limited items (any page whose name contains it). |
| `pet_accessories` | A pet's infostand. |
| `trax_songs` | The jukebox playlist editor. |
| `guild_custom_furni` | A group's details. |
| `gift_shop` | An opened present. |
| `room_bundles`, `room_bundles_mobile` | Featured items. |
| `mobile_subscriptions` | Featured items (it opens `hc_membership`). |
| `habbo_club_desktop`, `horse_styles`, `horse_shoe`, `ecotron_transform`, `quest_shell`, `quest_snowflakes`, `val_quests`, `set_easter` | Named by the client for those pages. |

**Offers.** Clicking a tile opens the offer; the + tile adds one.

- **What it gives:** one thing, or a bundle of several. Each is one of:
  - a floor or wall item, picked by the start of its class name or its id, and how many (a wall
    item can carry its pattern or poster id);
  - a badge, by its code;
  - an effect, by its number;
  - a pet, by its type (or an item whose class name is `pet<type>`): the buyer picks the breed
    and names it, and the name key should be `a0 pet<type>`, which the pet page reads;
  - a bot, with the figure it wears, named by an item's class name if one is picked, else the
    hotel's default bot name;
  - a membership, which is sold on its own (see below).

  An item must exist and be of its kind, or the save is refused; otherwise every purchase would
  fail. An offer gives up to 20 things and one pet at most. A badge, a pet, a bot and a membership
  are given once per purchase, so they come one at a time and such an offer can't be bought in
  bulk; a badge the buyer already has isn't sold to them again.
- **Name key:** the product data the client names it by. Leave it empty to use the first item's
  class name, as the hotel's own offers do; a pet with no item gets `a0 pet<type>`, a bot with no
  item `bot`.
- **Price:** credits, and an amount of an activity-point currency (duckets, diamonds, ...).
- **Rules:** who may buy it (anyone, club, VIP), whether it can be gifted or bought in bulk, and
  whether it is shown.
- **Page:** it can be moved to any page, from its field or by dragging its tile onto the tree.

**Featured items.** A `frontpage4` or `frontpage_featured` page shows the featured items: up to
four, the first big and the rest beside it. Each has a promo image (a path under the client's
`image.library.url`), a title, what clicking it opens (a page by its link key, an offer, or a
product code), and an optional end time, which the client counts down to; once it has passed the
item is no longer sent. They're edited on the page's **Featured** tab, dragged into order, and go
live on publish. Other pages are sent none, and the client keeps the ones it was last sent.

What other things depend on is protected:

- An offer that sells a **limited series** keeps its one item (its price can change), stays in the
  normal catalog, and can't be deleted. Hide it instead. What is left of the series is never
  touched by the editor.
- A **club gift** offer, and one that **Builders Club furni** was placed from, can't be deleted
  either; hide them.

**Publishing.** Edits are saved straight away but players don't see them yet. **Publish** reloads
both catalogs (the same as `:reload catalog`) and tells every client online the catalog changed:
it drops what it has and shows "the catalog has been updated". The bar along the top of the
editor shows how many edits are waiting and the last of them. A `:reload catalog` also puts them
live, and a restart loads the catalog fresh.

**Undo, redo and discard.** Every edit since the last publish can be undone and done again:
**Undo** and **Redo** in the bar (or Ctrl+Z and Ctrl+Shift+Z outside a text field), or **History**,
which lists the steps and undoes or redoes back to any of them. An undo puts the rows back exactly,
ids and order included. A page build, a bulk add or a generated catalog is one step, however many
edits it made. **Discard** undoes every step since the last publish, so the saved catalog is the
one players have; the steps can still be redone. An undo is refused, with why, when a row it would
put back was changed since, or when it would take away an offer that now sells a limited series or
that Builders Club furni was placed from. Limited series themselves are not undone: the raffle
sells from them at once. The history is kept in memory: a publish or a restart starts it afresh,
and the 500 newest steps are kept.

**Missing furni.** The floor and wall items the catalog doesn't sell: in no offer (**Not sold**), or
only in hidden offers or on pages players can't reach (**Only hidden**). Patterns, posters, songs
and pets are left out, since their builders sell them. The list narrows by name, furni line and
furnidata category. Pick tiles, choose a page and a price, and **Add**: one offer each, one step to
undo. **Add furni** on an open page does the same for that page.

**Duplicates.** Furni sold alone by more than one offer (club gifts and bundles aren't counted),
each with where its offers are, what they cost, and whether players see them. Open an offer in the
editor, delete it, **keep only this** one, or **keep one of each**: the first players see.

**Front page.** **Make a front page** adds a `frontpage4` tab, first among the tabs, with the
voucher box's line, and opens its featured items. **Feature** on an offer puts it among the
featured items (four at most); give it a promo image there.

**Generate.** A whole catalog from the hotel's own furniture, pets, effects and songs. Its tabs, in
this order: Front Page; Habbo Club (`hc_membership`) with Club gifts; Furni (spaces, posters,
trophies and badge displays built by their builders, then the furni lines on shelves by theme:
Seasonal, Classic lines, Around the world, Fantasy & sci-fi, Rooms & places, Nature & animals,
Music & parties, Games & sports, Cute & colourful, Collectibles, Hotel specials and More lines,
with lines of one family, such as every `xmas…`, in one folder, a shelf of more than 24 split by
letter (*Classic lines A–H*), a line over three pages or more in a folder of its own, and furni in
no line by category under More furni); Wired (triggers,
effects, conditions, add-ons, selectors, variables); Pets (a page per pet, accessories, pet care);
Extras (effects, trax songs, bots); Rares (rare lines, limited and sold-out limited items); Groups;
and Builders Club (its lines shown only in its catalog). Each page gets the layout and the icon
that suit it, and a line too long for one page (100 offers, or what you set) is spread over
numbered pages. **Plan it** shows the tree first: rename a page, give it another icon, or untick
it to leave it out. Then:

- **Replace this catalog** moves the tabs there now, with everything under them, into one hidden
  tab, *Old catalog*. Nothing is deleted. Offers already on sale move to their new page with
  their prices (untick **Move the offers there now** to make every offer new), memberships, club
  gifts, bots and limited offers move to their pages, and the link keys the new pages take are
  taken off the old ones.
- **Build beside it** adds the new tabs hidden after the old ones, every offer made new.

New offers cost what you set. Rares made new start on hidden pages, to be priced first. It is one
step to undo, and nothing goes live until you publish.

Behind these: `GET /api/catalog/history`, `POST /api/catalog/undo`, `/redo` and `/discard`;
`GET /api/catalog/audit/unoffered` (`scope` = `missing` or `hidden`, `q`, `line`, `category`,
`page`, `size`) and `GET /api/catalog/audit/duplicates`; `POST /api/catalog/pages/{id}/furni`
(definition ids and one price) and `POST /api/catalog/offers/delete`; `POST /api/catalog/frontpage`;
and `POST /api/catalog/generate/preview` (changes nothing) and `POST /api/catalog/generate`.
Reading needs `admin.catalog.view`; changing needs `catalog.manage` as well.

**Hidden means hidden.** A hidden offer isn't on its page and can't be bought or raffled, even by
a client that still knows its id. Before this, it was still sold. A hidden *page* only leaves the
navigator: its offers stay on sale. That's on purpose, because the club window sells the
memberships and a hotel often keeps them on a page of their own out of the navigator.

**Habbo Club memberships.** An offer can give days of Habbo Club or Builders Club instead of an
item. Players buy Habbo Club from the club window: the page with the link key `hc_membership`,
which the client's club buttons open, with the `club_buy` layout, which draws the window. The
window lists every shown membership offer in the normal catalog, wherever it sits, by length.
Renewals and the club centre sell them too. A page with the `club_buy` layout under another link
key is never opened by those buttons. So, to sell memberships:

1. Have a shown page with the link key `hc_membership` and the `club_buy` layout. If there are
   memberships but no such page, the Catalog page says so, with a button that adds one.
2. Add a membership offer for each length. On a `club_buy` page, **New offer** starts as a
   membership.
   - **Days:** 31 is a month. The client sells it by its days, whatever its name key says.
   - **Name key:** left empty, it is named by length, as the hotel's own are
     (`habbo_club_3_months`). Typing a length name key (`habbo_club_3_months`,
     `habbo_club_45_days`) sets the days to match. When the two disagree, the editor says so, with
     a button to set the days, and the offer list marks it "name says ...".
   - **Price:** credits and an activity-point currency.
3. Publish.

A membership can't ask for club (non-members couldn't buy it), is bought one at a time, can't be
gifted yet (there is no gift path for memberships), and only lives in the normal catalog. Builders
Club days work the same way, from a Builders Club page.

**Club gifts.** Any furni offer in the normal catalog can be a club gift: members claim it for free.
Each member earns one gift per month of club used up, and each gift can ask for a number of club
days used up before it can be picked. Gifts are listed on the page with the link key `club_gifts`
(which the client opens) and the `club_gifts` layout, which lists every shown gift wherever it
sits; there is a warning and a button when there is none.
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

Page icons and images, furniture icons, badges and promo images load from the addresses in the
client's external variables (**Gamedata > Variables**, see `docs/gamedata.md`): `catalog.icons.url`,
`asset.urls.catalog`, `asset.urls.icons.furni`, `badge.asset.url` and `image.library.url`,
resolved as the client resolves them. An address that names no host is under the client's page,
`ClientLoginUrl`; without that set, such an address shows no picture. A change to the variables
reaches the editor at once. The layout picker describes every layout the Flash client has
a template for (as nitro-next mirrors them); codes in `CatalogLayouts`, or used by a page, that it
doesn't describe are listed under "Other", and any code can be typed.

### Page builders

A page builder fills a page with the offers a kind of page sells, named the way its layout reads
them. **Build**, beside Publish, opens it: onto the page open in the editor, or onto **a new
page** - titled, with an icon, under any page and shown where you pick - made with the builder's
layout (the pet builder's page gets `pets2`, its pets their own pages under it). Page and offers
are one step to undo. Sending `newPageTitle` (and `newPageIcon`) with the build does the same: the
page in the address is then the one the new page goes under, and the answer's `pageId` is the new
page. `POST /api/catalog/pages/{id}/build/preview` (`admin.catalog.view`) returns the plan and
changes nothing; `POST /api/catalog/pages/{id}/build` (`catalog.manage`) works the plan out again
on the server and makes only the items whose keys are sent, each through the same checks as an
edit by hand. An item that is refused is listed with why, and the rest go on. Each new offer gets
the price, club level, gift and shown flags sent, and is bought in bulk where it can be.

- **Trophies** (`trophies`): every floor item with the `trophy` logic. A family's `*1`, `*2` and
  `*3` are named `a0 <base>_g`, `_s` and `_b` when the product data has that code (else the same
  without `a0 `), which the trophies window groups as one trophy in gold, silver and bronze. Any
  other trophy is an offer of its own.
- **Pets** (`pets`, under the page): every pet type with a palette a buyer can choose gets a page
  of its own (titled by `pet.type.<n>`, else its product name without "and starter food"), with
  one offer named `a0 pet<n>`; the pet window reads the type from those digits. The pages are
  hidden unless a display is sent.
- **Colours** (`default_3x3_color_grouping`): a family, `<base>*1` on, in number order.
- **Furni line** (`default_3x3`): a furni line, or the class names that start with a prefix (3
  characters at least). `GET /api/catalog/builders/furni-lines` lists the lines.
- **Pet customisation** (`petcustomization`): shampoos, parts and saddles, by the pet type their
  custom parameters start with, optionally one type.
- **Effects** (`pixeleffects`): every effect with a name in the product data (`avatar_effect<n>`)
  or the texts (`fx_<n>`) that a player may own; the inventory's reserved ids and ids past
  `MaxEffectId` are left out.
- **Sold out limited** (`sold_ltd_items`): limited offers on other pages with nothing left of
  their series, moved onto the page.
- **Spaces** (`spaces_new`): every room paper pattern the product data names
  (`floor_single_<p>`, `wallpaper_single_<p>`, `landscape_single_<p>`), floors, then wallpapers,
  then landscapes, each by pattern (`1.2` before `1.10`). Each is an offer of the one `floor`,
  `wallpaper` or `landscape` item with the pattern as its extra parameter, named by its code
  (see `docs/room-decorations.md`). The page shows its room preview only once it sells all
  three, so the plan warns when a group is empty or its item is missing.
- **Posters** (`default_3x3`): every `poster <id>` code, by id, as the `poster` item with the id
  as its extra parameter.
- **Badge displays** (`badge_display`): floor items with the `badge_display` logic, or, until
  `MapBadgeDisplayLogic` has run, those named `badge_display...`; named `a0 <name>` when the
  product data has it.

- **Song discs** (`soundmachine`): a disk of every official song (every song, with a warning,
  while none is marked official), by name: the song disk item (category 8, `song_disk` preferred)
  with the song's id as its extra parameter (see `docs/sound.md`). The page shows the product
  data name of the offer's name key, not the song's, so a song whose code Habbo's product data
  has is named `SONG <code>`; any other is named `song_disk`.

The furni line and colours builders leave out room papers, posters and song discs, with a
warning: one item is every pattern, poster or song, and an offer of it without one is refused at
purchase.

An item is marked as already offered when an offer anywhere sells the same item on its own, the
same pattern, poster or song, the same pet type or the same effect. **Set layout** also gives the page the builder's layout (not for
pets, whose pages carry it). A preview lists up to `CatalogBuilderItemLimit` items and says when
there are more. Nothing goes live until you publish.

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

## Chat log

Staff with `admin.chatlog.view` get a **Chat log** page: what players said in rooms, newest first,
with when, who said it and in which room. Whispers are in it too, marked, with who they were to.
A player's page and a room's page each link to their own chat.

Narrow it to one player (exact name or id: what they said and what was whispered to them), one
room (by id), or words in the line (case is ignored). Any line's **Show in context** button shows
what was said in its room just before and after it, with the line marked.

The page goes **Older** and **Newer** a page at a time and shows no total. The chat log grows with
every line said, and counting it for each page would read all of it. Paging follows the line id,
which the room and player indexes already keep in order, so no migration is needed.

What is logged is the hotel's rule: lines players type into a room's chat, when
`Turbo:Rooms:ChatlogEnabled` is on (the default), cut to 100 characters. Bots, pets, wired messages
and room notices are not chat and are not logged. Rooms write chat in batches, so a line can take
a few seconds to appear; the newest page asks again every 30 seconds.

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
| `admin.chatlog.view` | The Chat log page: what players said in rooms, whispers included, and each line in context. |
| `admin.catalog.view` | The Catalog page: the catalog's pages, offers and prices. |
| `admin.players.create` | Creating new players on the Players page. |
| `admin.tickets.issue` | Issuing a player a login ticket, and taking it away, for players whose every permission they hold. |
| `admin.accounts.manage` | Unlinking a player's Discord and signing them out of the public site, for players whose every permission they hold. |
| `admin.welcome.manage` | The Welcome message tab in Hotel controls: seeing and changing the message every player is shown when they log in. |
| `catalog.manage` | Changing the catalog on that page, and publishing it to players. |
| `admin.permissions.view` | The Permissions page: seeing groups, any player's permissions, who has a node, and the permission log. |
| `permissions.manage` | Changing permissions on that page, within the rule in [Permission editor](#permission-editor). The in-game `:group` command needs it too, and follows the same rule. |
| `permissions.superuser` | With `permissions.manage`: lifts the weight and held-node limits of the permission editor (see [Permission editor](#permission-editor)). No wildcard grants it. |
| `admin.settings.view` | The Settings page: every server setting, where its value comes from and what waits on a restart. Secrets are never shown. |
| `settings.manage` | Changing settings on that page: overriding appsettings.json, replacing a secret, putting a setting back (see `docs/settings.md`). |
| `admin.content.view` | The Content page: achievements, badges, the navigator's categories, groups, pets and bots. |
| `content.manage` | Changing the content on that page: publishing achievements, pinning badges' rarity and giving or taking them, and the rest of its tabs. |

Everything else in the panel uses each command's own permission. The console runs commands
**as you**, with exactly your permissions, rate limits and confirmations, and logs them like
commands typed in the hotel. Room commands (`kick`, `mute`, ...) need a room, so the console
can't run them.

## Content

The **Content** page is the game's content, each change made at once and on record:

- **Achievements**, from the achievement catalog, by category. An edit (a level's requirement,
  badge, score or reward, the state, the dates, or the whole definition as JSON) is **checked** as
  the catalog checks every definition, then **published** as the achievement's next revision with
  a reason, kept in `achievement_audit`, and live at once. **Copy as a new achievement** starts one
  from another. Retiring keeps what players earned and gives nothing more.
- **Badges**: every code players hold or that has a pinned rarity, most held first. Pin a rarity
  (it reaches the hotel within the badge directory's `OwnerCountRefreshMs`), see who holds a badge,
  take it from them, or give it to a player. A badge no one holds yet is found by typing its code.
  Staff who may see the gamedata also edit its name and description (`badge_name_<code>`,
  `badge_desc_<code>`), saved to the external texts.
- **Navigator**: the room categories (name, order, shown, staff only, lowest rank, a permission
  node they need), the event categories and the tabs along the navigator's top. A change reloads
  the navigator's categories and drops the listings kept under a renamed or removed category, so
  players see it the next time they open the navigator. A category rooms are in, or an event
  category events are in, can't be removed. Staff picks stay on each room's page.
- **Groups**: found by name, owner, group or room id. Staff rename a group (as typed, not
  word-filtered), put its badge back to the default, take any member, admin, request or block out,
  or delete it, whatever its size or `Turbo:Guilds:DeletionEnabled`. Each goes through the group's
  grain (`StaffRenameAsync`, ...) as the owner's own change would, the staff member named in the
  log. Below, the badge parts and colours group badges are built from: added under the next id of
  their kind (ids are written into badge codes, so they are never reused), their files or hex
  changed, and read again by the group directory at once.
- **Pets**: each pet type's palettes (the body the info stand names, the rarity breeding weighs,
  whether the catalogue sells it) and the lines each type says, or every type without lines of its
  own. A change reloads the pet providers, so pets in rooms use it at once.
- **Bots**: found by name, owner, bot or room id. A bot standing in a room is set as its owner's
  skills could (name, motto, look, lines, roaming, dance; held to `Turbo:Bots`' lengths, not
  word-filtered) through its room grain (`StaffUpdateBotAsync`), so the room sees it, or taken back
  to its owner's inventory. A bot in an inventory can only be deleted.

It needs `admin.content.view` to look and `content.manage` to change anything.

## Hotel view

The **Hotel view** page edits the reception players land in: its backgrounds and their timed sets,
the five widget slots, the promos their schedules show and the words on them, with a preview at any
time. It needs `admin.gamedata.view` to look and `gamedata.manage` to save; a save is one change set
in the gamedata history. `docs/gamedata.md` has the details.

## Server settings

The **Settings** page lists every option of every config section: its value, where that comes
from (its default, `appsettings.json`, the panel, or the environment) and whether a restart is
waiting on it. Staff with `settings.manage` override a value there. It applies after a restart; the
environment always wins over the panel; secrets can be replaced but are never shown; what the panel
stands on (its own address, the database) is changed only in the files and the environment.
`docs/settings.md` has the details.

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
| `ChatlogPageSize` | `100` | | Lines per page on the Chat log page; a line in context shows half as many on each side. |
| `PerformanceSampleSeconds` | `10` | | How often the Performance page's figures are taken. |
| `PerformanceHistoryHours` | `24` | | How many hours of figures are kept, in memory. |
| `PerformanceMaxPoints` | `360` | | The most points a Performance chart is sent; longer ranges are merged into this many. |
| `CatalogLayouts` | the client's layouts | | The page layouts the catalog editor offers, besides any a page already uses. |
| `CatalogFurnitureSearchLimit` | `25` | | Items the catalog editor's item picker lists at once. |
| `CatalogBuilderItemLimit` | `500` | | Items a catalog page builder's preview lists at once. |
| `ClientLoginUrl` | empty | `TURBO_ADMIN_CLIENT_LOGIN_URL` | The client's login address with `{ticket}` where the ticket goes, e.g. `https://hotel.example.com/client?sso={ticket}`. Set, a new ticket also comes as a login link, and an image address in the external variables that names no host is read under it. |
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

## Songs

The trax songs that song disks play (see [sound.md](sound.md)) are the hotel's own data: there is
no stock list, so a hotel adds the songs it sells. The API is under `/api/songs`. Reading needs
`admin.catalog.view`; adding, changing and removing need `catalog.manage` as well, since songs are
what the catalog's song disk offers sell.

| Request | Answer |
| --- | --- |
| `GET /api/songs` | `{ "songs": [ { "id", "name", "author", "length", "official", "discs", "code" } ] }`, by id |
| `GET /api/songs/{id}` | one song, the same fields and `"track"`; 404 when there is none |
| `POST /api/songs` | adds a song from `{ "name", "author", "track", "length", "official", "code" }`; answers it as `GET /api/songs/{id}` does |
| `PUT /api/songs/{id}` | replaces a song's fields with the same body; 404 when there is none |
| `DELETE /api/songs/{id}` | 204; refused (400) while any disk carries the song |

- `length` is in seconds. The client is sent milliseconds, and a jukebox moves on to its next
  disk when a song's length is up, so it has to match the track.
- `track` is the trax track in the client's own format (what the client's sound machine plays);
  the server stores and sends it and never reads it.
- `discs` is how many song disks carry the song, placed, in an inventory or in a jukebox.
- `code` is optional: an official song's catalog code, unique, which may not begin with a digit
  (the catalog's song disk page reads a product parameter beginning with one as a song id).
  `null` when the song has none.
- A refusal is `400` with `{ "message" }` saying why (no name, no track, a length of zero, a taken
  code); missing rights are `403`.

**Selling a song.** A song disk offer gives the `song_disk` furni (floor, category 8, "trax
song"), with the song's id as the product's extra parameter; buying one writes that id onto the
disk. The song disk page layout (`soundmachine`) plays a preview from the same parameter. A code
in the parameter instead of an id works for that preview only: the disk itself needs the id.

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
