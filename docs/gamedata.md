# Gamedata

The hotel keeps its gamedata in the database and builds the files the client loads from it:
FurnitureData, the external texts, the product data, the figure data, and the client's
configuration (its external variables). Habbo's releases are checked for updates and taken in after review,
without losing the hotel's own changes. Every change is recorded and can be rolled back.

It lives in `Turbo.Gamedata`, with its admin API in `Turbo.Admin/Api/GamedataEndpoints.cs` and its
page under **Gamedata** in the panel.

## How it works

- **Habbo's releases.** Every `ReleaseCheckMinutes`, and when staff press **Check now**, Turbo reads
  `www.habbo.<HabboDomain>/gamedata/external_variables/0` for the revision (the
  `flash-assets-<revision>` of `flash.client.url`). It then downloads `furnidata_json/0`. New
  furniture data is kept in `habbo_releases`, gzipped, by its SHA-1. Nothing is taken in until
  staff do it.
- **The definitions are the source.** `furniture_definitions` holds every furnidata field. Some
  are the columns the server already used (`xdim` is `width`, `canstandon` is `can_walk`,
  `height` is `stack_height`, `specialtype` is `category`). The rest are columns only the client's
  furnidata has: `public_name`, `description`, `part_colors`, `revision`, `client_category`,
  `furni_line` and so on.
- **Taking in an update** matches Habbo's items to definitions by kind and classname. Each field
  is compared three ways: Habbo's new value, Habbo's value when last taken in (kept in
  `habbo_furniture`), and the definition's.
  - A field still at Habbo's old value takes the new one.
  - A field the hotel changed keeps the hotel's value. When Habbo changed it too, the review
    reports it as kept.
  - An item the hotel has no definition of is added under Habbo's sprite id. If one of the
    hotel's own definitions holds that id, it gets the next free one.
  - Definitions Habbo dropped are left alone.

  The first import of a hotel that already had definitions has nothing to compare with. The
  client-only columns take Habbo's values; the server's columns keep the hotel's, and are
  reported. From then on Habbo's updates reach them too.
- **Furniture files.** Taking an update in runs in the background. It first reads the asset file
  of each furniture it needs (`FurnitureFileUrl`, `hof_furni/<revision>/<name>.swf`), once per
  asset and revision; a furniture's colours share one file. What a file says is kept in
  `habbo_furniture_assets`: its **states** (which set `total_states`), Habbo's logic and
  visualization, its size, directions, colours and layers. A file that can't be fetched or read
  keeps why, and is tried again by the next import.
  - States count the animations Habbo numbers 0-99 that are not transitions: one past the highest,
    so a lamp (off, then animation 1) has 2. Transitions (from 100, or marked `transitionTo` /
    `transitionFrom`) and special animations (below 0, a dice rolling) are not states.
  - `states` merges like any other field. A hotel's own `total_states` stays when Habbo's file
    changes, and **Use Habbo's values** puts the file's back.
  - A first import reads every file Habbo has, about 18,000. The panel shows its progress.
- **Using Habbo's values.** On **Furniture**, staff choose fields (`canputstuffon`, `recyclable`,
  `states`, ...) and replace the hotel's values with those of Habbo's newest release, taken in or
  not, for every furniture Habbo has. Each field shows how many definitions differ first. It is one change set (`habbo values`)
  that rolls back as one. The hotel's own furniture and the fields not chosen are left alone.
  The editor also offers **Use Habbo's** beside each field of one definition that differs.
- **History.** Every import, edit and rollback is a change set in `gamedata_change_sets` and
  `gamedata_changes`. Each change records the fields before and after.
  - Rolling a set back restores each field unless it has changed again since.
  - It removes each definition the set made, unless furniture, an offer or a Builders Club
    placement uses it.
  - It restores Habbo's items as last taken in, so the next import sees Habbo's changes again.
  - What it leaves alone is reported. A rollback can't itself be rolled back.
- **The offers** are stamped whenever the file is built, from the catalogs players see:
  - `offerid` is an offer in the normal catalog; `bcofferid` one in the Builders Club catalog.
  - A page shown in both catalogs (`display` `both`) gives both fields the same offer.
  - An offer counts when it is visible, it is listed on a page that catalog shows, and that page
    and every page above it are visible. Invisible pages and everything under them give no ids.
  - An offer of several furniture (a bundle) is not the furniture's offer. A single item wins over
    a pack, then the oldest offer.
  - `buyout` and `bc` say whether there is an offer, as in Habbo's file. Nothing is rented
    (`rentofferid` is `-1`).

  A catalog edited but not yet published stamps as it was.
- **The file** is written as Habbo writes it: `roomitemtypes` and `wallitemtypes`, each item's
  fields in Habbo's order for its kind, and no `partcolors` on an item without colours.
  Definitions are ordered by sprite id. It is built again when the definitions change or a
  published catalog replaces a catalog snapshot.

## Product data

Product data gives the name and description the client shows for a catalog offer. The client
looks it up by the offer's name key (`catalog_offers.localization_id`). With no product of that
code, the client shows the furniture's own name.

Each check of Habbo also downloads its `productdata_json`, and keeps a new version in
`habbo_product_versions`.
- **Taking in:** **Gamedata → Products** reviews a version and takes it in, in the background.
  Each product's name and description are compared three ways, as a text is. The hotel keeps a
  field it changed, and a product it removed. A product Habbo drops stays.
- **Editing:** staff search products by code, name or description, edit them, add the hotel's
  own, and remove them. Every change is in the history and rolls back.
- **In the catalog:** the offer editor suggests codes as the name key is typed, and shows what
  the client will show for it. That lookup (`/api/product-data`) is open to anyone who sees the
  catalog.
- **The file** is `{"productdata":{"product":[{code,name,description}]}}` by code, served at
  `/gamedata/productdata_json/0`; point the client's `productdata.url` there.

Codes are case-sensitive: Habbo has codes that differ only in case. Habbo's one numeric code
(`25`) is kept as text, which is how the client keys it anyway.

## External texts

Each check of Habbo also downloads its `external_flash_texts`, and keeps a new version in
`habbo_text_versions`. **Gamedata → Texts** reviews one and takes it in, in the background. Each
key is compared three ways, as a furniture field is:
- Habbo's new value, Habbo's value when last taken in (`habbo_texts`), and the hotel's
  (`gamedata_texts`).
- The hotel keeps the texts it changed or removed.
- A key Habbo drops stays.

Staff search texts by key or value, edit them (a line break is written `\n`, as in the file), add
the hotel's own, and remove them. Every import and edit is a change set that rolls back. The file
is built from `gamedata_texts` by key and served like FurnitureData. The external variables point
the client's `gamedata.urls.externalTexts` at it; the client reads the `key=value` file as it
reads Habbo's.

The server reads the texts too, where it sends the client words rather than keys: command replies
and help, ban and maintenance messages, notices, and the names wired variables show beside their
values. Nothing is loaded at boot. `IHotelTextProvider` reads only the keys asked for, from
`gamedata_texts`, and keeps what it read (a key with no text included) for `TextCacheSeconds`, up
to `TextCacheSize` keys. A text that is only another's key (`${widget.memenu.dance1}`) is followed,
up to `TextKeyDepth` steps.
- A wired variable's names are read by family (`fx_`, `handitem`, the editor's dance and sign
  keys), once per room, and only the keys the hotel has come back.
- An import, edit or rollback forgets what was kept, and so does `:reload texts`. Another silo sees
  the change within `TextCacheSeconds`.

## Figure data

The figure data is the colours and clothing avatars are drawn from, and who may wear each piece.
Each check of Habbo also downloads its `figuredata` (XML), and keeps a new version in
`habbo_figure_versions`.
- **Records:** each colour (keyed `palette/id`), kind of clothing (`hr`, `ch`, ...) and piece (by
  its id) is one record in `gamedata_figure_records`. Its fields are JSON, by the names Habbo's
  file gives them.
- **Taking in:** **Gamedata → Figures** reviews a version and takes it in, in the background. Each
  field is compared three ways, as a product's are; a piece's parts and hidden layers are each one
  field.
- **Editing:** staff browse clothing by kind, colours and kinds. They change who may wear a piece
  (everyone, club), whether it is offered, sold or given to new looks, and a colour's hex and
  club level. **All fields** edits the whole record, parts included. Staff may add the hotel's own
  records and remove any. Every change is in the history and rolls back.
- **The file** is the client's FigureData.json (`palettes` and `setTypes`), served at
  `/gamedata/figuredata_json/0` and listed in the hashes as `figurepartlist_json`. Point the
  client's `figuredata.url` there. It was checked against the studio's converter on
  Habbo's own file: the same 3,311 pieces and every colour, field for field.

### What a player may wear

Every figure a player puts on is fitted to the figure data: in the avatar editor, from a mannequin,
in a changing booth. The same happens at login, so club clothing comes off once the club has run
out. The rules are the ones the client's avatar editor offers clothing by:
- A piece must be offered (`selectable`), for the wearer's gender (`U` is anyone's), and within
  their club level. A member wears at the level the client is told: VIP.
- A piece **sold** (`sellable`) is worn only by players who own it (`player_figure_sets`). The
  client is sent what a player owns at login and whenever it changes.
- A colour must be offered and within their club level. A piece takes no more colours than its
  parts have layers, and none when it takes no colour.
- Each kind is worn at most once. A kind the figure data makes mandatory for the wearer's gender
  and club (`mand_m_0`, `mand_f_1`, ...) is always worn.

A figure is fitted, not refused:
- A piece the player may not wear comes off. If its kind is mandatory, the first one they may
  wear goes on instead.
- A colour they may not use becomes the first one they may.
- Anything the figure data doesn't know is left out.

The client is told the look that was kept. Players with `figure.any` wear anything as they give
it. Until Habbo's figure data is taken in, figures are not checked.

Owned pieces are given and taken under **Gamedata → Figures → Clothing for sale**. Taking a piece
takes it off the player at once. The client has no way yet to redeem a clothing furni into owned
pieces.

## Reading asset files (`Turbo.Assets`)

`Turbo.Assets` reads Habbo's asset libraries in all three containers:

| Format | Reader | What it is |
| --- | --- | --- |
| `.swf` | `SwfLibrary` | Habbo's SWF libraries: `FWS` and zlib `CWS`. LZMA `ZWS` is refused for now. |
| `.hab` | `HabLibrary` | The HTML client's libraries: a zlib'd JSON manifest and its entries. |
| `.nitro` | `NitroBundle` | The client's bundles: a zip of the asset data, the sheet and its frames. |

`FurnitureAssetReader.Read` tells them apart by their first bytes and reads a furniture's
`FurnitureAssetInfo` from any of them. Each file is checked against `AssetLimits` before it is
unpacked.

`NitroConverter.Convert` turns any of them into the `.nitro` bundle the client loads. It is the
studio's converter ported to C#, and writes what that converter wrote:

- **Asset data** (`AssetDataMapper`): the index, manifest, animation, assets, logic and
  visualization documents and palette colours, filtered to the sizes the client draws
  (`AssetDataFilter`). Keys come in the same order, numbers are read and written as JavaScript reads
  and writes them, and a value it would leave out is left out.
- **Avatar effects** (asset type `fx`): the `animation` document becomes the `animations` the
  client's effects play (sprites, frames of effect and body parts, avatar layering, overrides), and
  the effect's assets are kept at every size but its `sh_` shadows, as for clothing (`figure`).
- **Images**: SWF bitmaps (lossless, JPEG with alpha) decoded with SkiaSharp and kept as
  unpremultiplied bytes, never redrawn.
- **The sheet** (`SpriteSheetPacker`): the images the assets use, trimmed of transparent edges,
  identical ones sharing a place, packed by MaxRects. Frames are in the Pixi layout.
- **A prebuilt `.hab`** keeps its packed sheet, with its JSON brought to the current layout.
- **A `.nitro`** is taken as it is.

It was checked against the studio's converter on 79 of Habbo's libraries (furniture, pets,
clothing), and on all 252 of Habbo's effect libraries: the asset data is byte for byte the same, and
every frame has the same name, trim and pixels (for the effects, the same name, trim and size). Not
ported: `room_visualization`, which only the room library has. The sheets pack differently, which
the client does not see.

## Serving the files

The gamedata host is a small web host beside the silo, like the admin and site APIs. It serves
the files the way Habbo serves its own:

| Address | What it is |
| --- | --- |
| `/gamedata/furnidata_json/0` | 307 redirect to the current build. Cached for `CurrentMaxAgeSeconds`. |
| `/gamedata/furnidata_json/<sha1>` | A build, by the hash of its content. Never changes: `immutable`, gzipped for clients that take it. |
| `/gamedata/external_flash_texts/0` and `/<sha1>` | The external texts, the same way: `key=value` lines, as Habbo serves them. |
| `/gamedata/productdata_json/0` and `/<sha1>` | The product data, the same way. |
| `/gamedata/figuredata_json/0` and `/<sha1>` | The figure data, as the client's FigureData.json. |
| `/gamedata/external_variables/0` and `/<sha1>` | The client's configuration, the JSON object Nitro loads (see [External variables](#external-variables)). |
| `/gamedata/hashes` | Each file's current hash, in Habbo's shape: `{"hashes":[{"name":"furnidata","url":".../gamedata/furnidata_json","hash":"..."}, {"name":"external_texts", ...}]}`. The url is the file's address without its hash, built on `PublicUrl` (or the address the request came to). |

Builds are kept in `gamedata_builds`: the current one and the `KeepBuilds` before it, so a client
that loaded an address before a rebuild still finds it. The same content hashes the same on every
silo.

Point the client at the redirect, through a reverse proxy in front of the host:

```json
"furnituredata.url": "https://gamedata.example.com/gamedata/furnidata_json/0"
```

When a catalog publish changes the furnidata (an item's offers moved), the `CatalogPublished`
packet carries the new file's hash, and clients online load that build at once. A publish that
leaves the furnidata as it was sends no hash.

Turbo now owns the hotel's FurnitureData; Nitro Studio no longer publishes it to the hotel.

## External variables

The client's configuration, what was its `nitro-config.json`, is kept by the hotel as its
**external variables** and served like the other files, as `external_variables`. Habbo serves its
own under that name as `key=value` lines; Nitro's values are typed (`true`, `120`, lists), so this
one is the JSON object Nitro loads. Turbo now owns the asset addresses in it; Nitro Studio no
longer publishes them.

- **The rows** are in `gamedata_variables`: a key, and its value as JSON (`"text"` in quotes,
  `true`, `120`, `[1, 2]`). Keys are case-sensitive, as they are to the client.
- **Staff** edit them under **Gamedata > Variables**: search, add, change and remove a variable, or
  **Import** a whole config (paste or upload a `nitro-config.json`). An import adds the keys the
  hotel lacks and changes those that differ; the hotel's other variables stay, unless **Remove the
  hotel's variables the config doesn't have** is ticked: then those are removed too, listed in the
  review first. A variable that follows a setting or a file is never removed. Every edit and import
  is a change set and rolls back like any other.
- **A variable can follow something** instead of holding a value of its own. **Link** it on the
  Variables tab, and the file writes what it follows, built again whenever that changes:
  - **a server setting** (`Turbo:Web:HotelName`, say): its value configured now, so saving the
    setting on the Settings page builds the variables again at once. A secret setting can't be
    followed: the variables are public.
  - **a gamedata file's address**, as Habbo's external variables give theirs:
    `<PublicUrl>/gamedata/<file>/<sha1>` of the file's current build (FurnitureData, product data,
    the external texts or the figure data). The client then loads exactly that build, cached for
    good, with no redirect. When the file is built anew (a catalog published, a text edited), the
    variables are too. Without `PublicUrl` the variable's own value is written instead.
- **The client's four gamedata addresses follow their files** from the start: the migration that
  brought links in (`AddServerSettings`) links `furnituredata.url`, `productdata.url`,
  `figuredata.url` and `gamedata.urls.externalTexts` to theirs, keeping any value they had as the
  one written while there is no `PublicUrl`, and adding them at `/gamedata/<file>/0` when they were
  missing. To serve one from elsewhere (a CDN), give it a value of its own.
- **A file's client setting can be changed** on **Gamedata > Overview**, under **Files the client
  loads**, for a client that reads its address under another key: the key chosen follows the file
  (added if the hotel lacks it), and the key that did before is unlinked, keeping the file's `/0`
  address. One change set, rolled back as one (`PUT /api/gamedata/files/<file>/key`).
- **Following stops** when a variable is given a value of its own, or on **Unlink**, which keeps
  what it was: a setting's value, or a file's `/0` address (it redirects to the current build; an
  address by hash is pruned in time). An import leaves a variable that follows something alone.
  Links are change sets and roll back like edits.
- **Clients online** keep the variables they loaded until they reload. FurnitureData still reaches
  them at once through `CatalogPublished`.
- **The panel** draws catalog, furniture and badge pictures from the same variables
  (`docs/admin-panel.md`).

## Hotel view

The reception players land in is built by the client from its `landing.view.*` external variables,
and the words on it from the external texts. The panel's **Hotel view** page edits both, as the
client reads them (nitro-react's `HotelViewWidgets.ts`, after Flash's `WidgetContainerLayout`):

| Tab | What it edits |
| --- | --- |
| **Preview** | Nothing: the reception on a 1600 by 900 window at the time chosen, and what each slot shows then. A slot clicked opens it. |
| **Slots** | The five widget slots (`landing.view.dynamic.slot.<n>.*`): the widget each holds; for a container (`widgetcontainer`), the schedule of promos it shows (`.conf`, `date,code;...`); for a promo of its own (`generic`), that promo; the headings over slots 4 and 5, and whether slots 2 and 3 line up. And the bottom slot 6, beside the avatar, which takes only the layout's fixed widgets: the expiring catalogue page, the community goal and the next limited rare. |
| **Promos** | The promos schedules name, each under a code (`landing.view.<code>.widget`, `.conf`, `.layout`): its picture and where it and the column sit, and the column's headings, text, buttons, links and countdowns. Each element's words are an external text, edited beside it. Promos are made, copied and removed here; removing one takes it out of the schedules. |
| **Articles** | The promo articles a slot holding the **Promo articles** widget (`promoarticle`) shows: title, text, a picture under the image library, a button to a web page or a client link, and the dates they show between. Kept in `promo_articles`, saved at once (not with the variables), and sent in order, ten at most (`Turbo:Catalog:Reception:PromoArticleLimit`). Another silo sees an edit within `CacheSeconds`. |
| **Community goals** | Campaigns the hotel plays together, shown by a slot holding a community goal widget (`communitygoal`, `communitygoalvsmode`, `communitygoalvsmodevote`). The last one started is played. Each item bought from its catalogue page is a point; a versus goal has a page per side, and a voting one lets every player vote once, for a point to a side. Its levels (one to three scores, or how far a side must lead), prize bands, dates and words (`landing.view.community.headline.<code>`, ...) are edited here, with the standing and the best contributors. Kept in `community_goals` and `community_goal_contributions`. |
| **Bonus rare** | The bonus rare campaigns: the furniture given for every so many credits a player brings in (bought, or spent in the catalogue), and recording credits bought outside the client. See `docs/reception.md`. |
| **Expiring pages** | The catalogue pages the expiring page widget (`expiringcatalogpage`, `expiringcatalogpagesmall`) counts down to, in `catalog_page_expiries`: it shows the one that runs out first among those still to come (`GetCatalogPageWithEarliestExpiry`), by the page's name, with its words (`landing.view.pageexpiry.page.<name>.header` / `.desc`) and the image library's `reception/catalog_teaser_<name>.png`. The page stays in the catalogue; the widget only promotes it. A page needs a name to be counted down to, and its expiry goes with it when it is deleted. Saved at once. |
| **Backgrounds** | The six background layers (`landing.view.<layer>.uri` and `.visible`) and sets of them under a code that `landing.view.bgtiming` switches to from a time on. A set's layer left empty keeps the picture of the set before it, as the client does. Each set's moving objects too (`landing.view.[<code>.]bgobject.<1-20>`, `<picture>;<type>;<numbers>`): along a line, wandering, in a spiral, or an animation, as nitro-next's `movingBackgroundObjects.ts` reads them. |
| **Look and widgets** | The colour and etching of every widget's text (`landing.view.common.*`), the panes' widths and the bonus rare's picture. What the fixed widgets read besides their slot: the next limited rare's switch (`next.limited.rare.countdown.widget.disabled`, the one variable the page saves outside `landing.view.`), the community goal's catalogue button and the page it opens (`landing.view.community.interactive`, `.catalog.target`), and the catalogue promo's and room hopper's targets, pictures and words, ready for when Nitro draws them. Each goal also shows its meter art (`reception/meter_level_<0-3>_<goal>.png`). |
| **All variables** | Every `landing.view.*` variable as JSON, for what the other tabs have no form for. |

- **Times** in schedules are UTC, as the server reads them (`ReceptionSchedule`): from each time on,
  its code shows, until a later one starts. The server answers the client's `GetCurrentTimingCode`
  and `GetSecondsUntil` from them; nothing else is kept.
- **Pictures** are addresses, shown beside the field as the client would load them;
  `${image.library.url}` is the client's image library. The panel doesn't host pictures.
- **Saving.** Changes stay in the panel until **Save**, then go together as one change set
  (`IHotelViewService`, `PUT /api/gamedata/hotel-view`), variables and texts alike, and roll back
  as one from the history. Only `landing.view.*` variables can be saved there. Players see the
  change when they next load the client.
- Widgets Nitro doesn't draw yet (the catalogue promos, daily quest, ...) can be chosen; players see
  the slot empty, and the panel says so.

## Setting it up in production (Ploi)

The files are served under `/gamedata/` on a site that already has a domain and a certificate,
such as the public site (`https://example.com/gamedata/...`). No new Ploi site is needed: that
site's nginx sends `/gamedata/` to the gamedata host on loopback. The host's paths start with
`/gamedata/` and its redirect is relative, so nothing is rewritten.

1. **On the Turbo site,** add to the **Environment** tab, then deploy:

   ```
   TURBO_GAMEDATA_ENABLED=true
   TURBO_GAMEDATA_PUBLIC_URL=https://example.com
   ```

   `TURBO_GAMEDATA_PUBLIC_URL` is the site's address without `/gamedata`. The host listens on
   `http://127.0.0.1:8094` (`TURBO_GAMEDATA_URL`). The log says `Gamedata host listening on ...`
   when it starts.
2. **On the site that serves them,** add the `location /gamedata/` block from
   `scripts/ploi/nginx-gamedata.conf` to its NGINX configuration (Site > Manage > Edit NGINX
   configuration), beside its other `location` blocks. On the public site, put it next to
   `location /api/`. Its `location /` stays: the longer prefix takes `/gamedata/`. Check with
   `sudo nginx -t`, then save.
3. **Check it** from anywhere:

   ```bash
   curl -sI https://example.com/gamedata/furnidata_json/0   # 307 to /gamedata/furnidata_json/<sha1>
   curl -s https://example.com/gamedata/hashes              # urls on https://example.com
   ```

4. **Take in Habbo's files first.** Under **Gamedata** in the panel, check Habbo and take in the
   furniture, texts, products and figures. Each file is built only from what the hotel has. Until
   the figure data is taken in, for example, its file has no clothing.
5. **Take in the client's config.** Under **Gamedata > Variables**, **Import** the client's current
   `nitro-config.json`. Its gamedata addresses are skipped: they follow the hotel's files.
6. **Point the client at the variables** in its page (`index.html`), in place of its
   `nitro-config.json`:

   ```js
   window.NitroConfig = {
       'nitro.config.url': 'https://example.com/gamedata/external_variables/0',
   };
   ```

   `nitro.config.url` may also be a list, merged in order, to lay a small file of the deployment's
   own after the hotel's. The client loads the variables, and the files they name, from its own
   subdomain. The host allows that for any site, so the client's host needs no change.

**Behind Cloudflare:** the files go through the site's Cloudflare settings like the rest of it.
A build's address never changes and is sent `immutable`, so Cloudflare may cache it for good; a
cache rule on `/gamedata/*/` addresses is optional. Don't let Cloudflare cache `/0` or
`/gamedata/hashes` longer than their own `max-age` (`CurrentMaxAgeSeconds`), or clients keep
loading the old build after a change.

## Settings (`Turbo:Gamedata`)

| Setting | Default | What it does |
| --- | --- | --- |
| `Enabled` | `false` | Whether the gamedata host listens. The files are built and Habbo is checked either way. |
| `Url` | `http://127.0.0.1:8094` | Where the host listens. Keep it on loopback, behind a reverse proxy. |
| `PublicUrl` | empty | Where clients reach the host, for the addresses `/gamedata/hashes` lists and those written into the external variables. Empty takes the address a request came to, and writes none into the variables. |
| `HabboDomain` | `com` | The Habbo hotel updates come from. |
| `ReleaseCheckMinutes` | `30` | How often Habbo is checked. `0` checks only when staff ask. |
| `HabboTimeoutSeconds` | `120` | How long a request to Habbo may take. |
| `FurnitureFileUrl` | `https://images.habbo.{domain}/dcr/hof_furni/{revision}/{name}.swf` | Where a furniture's asset file is, read when an update is taken in. |
| `FurnitureFileConcurrency` | `8` | Furniture files downloaded at once. |
| `KeepBuilds` | `10` | Builds of each file kept besides the current one. |
| `PreviewItemLimit` | `500` | Items an import review lists. Its counts cover every item. |
| `TextPageSize` | `50` | Texts per page of a search. |
| `HistoryPageSize` | `25` | Change sets per page of the history. |
| `TextCacheSeconds` | `300` | How long a text the server read is kept before it is read again. |
| `TextCacheSize` | `5000` | Texts kept at most; past it, what was kept is forgotten. |
| `TextKeyDepth` | `8` | Steps a text that is only another's key is followed. |
| `FigureCacheSeconds` | `300` | How long the figure data figures are checked against is kept before it is read again. |
| `CurrentMaxAgeSeconds` | `60` | How long `/0` may be cached. |

## Permissions

| Node | Gives |
| --- | --- |
| `admin.gamedata.view` | The **Gamedata** page: releases, reviews, definitions, variables, files and history; and the **Hotel view** page. |
| `gamedata.manage` | Checking Habbo, taking in updates, editing definitions, variables and the hotel view, importing a client config, rebuilding and rolling back; giving and taking players' clothing. |
| `figure.any` | Wearing any clothing and colour, whatever the figure data says of club, sale or selection. |

## Known limits

- `stack_height` keeps four decimals, as Habbo's heights have (`1.1125`, `0.0001`), so the file
  carries them as Habbo wrote them. Rooms still stack by the definition snapshot, which rounds to
  two decimals and never goes below its minimum height, and a placed item's `z` keeps three.
- An item Habbo gives no `name` is written with its classname.
- A player's club running out takes their club clothing off at their next login or look change, not
  the moment it runs out.
- Each silo checks Habbo and builds the file on its own. Definitions changed on one silo reach
  another's file on its next build; **Rebuild** in the panel forces one.
