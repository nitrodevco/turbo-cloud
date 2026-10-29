# Gamedata

- [Introduction](#introduction)
- [How it works](#how-it-works)
- [Product data](#product-data)
- [External texts](#external-texts)
- [Figure data](#figure-data)
    - [The figure and effect maps](#the-figure-and-effect-maps)
    - [What a player may wear](#what-a-player-may-wear)
- [Reading asset files (Turbo.Assets)](#reading-asset-files-turboassets)
- [Serving the files](#serving-the-files)
- [External variables](#external-variables)
- [Hotel view](#hotel-view)
- [Setting it up in production (Ploi)](#setting-it-up-in-production-ploi)
- [Settings (Turbo:Gamedata)](#settings-turbogamedata)
- [Permissions](#permissions)
- [Known limits](#known-limits)

## Introduction

The hotel keeps its gamedata in the database and builds the files the client loads from it:
FurnitureData, the external texts, the product data, the figure data, and the client's
configuration (its external variables). Habbo's releases are checked for updates and taken in after review,
without losing the hotel's own changes. Every change is recorded and can be rolled back.

It lives in `Turbo.Gamedata`, with its admin API in `Turbo.Admin/Api/GamedataEndpoints.cs` and its
page under **Gamedata & assets** in the panel, beside the asset bundles (`docs/asset-bundles.md`).

The page's **Furniture** tab lists every floor and wall definition with its tags
(`FurnitureStatusIndex`): `hotel-own` (Habbo's newest release has no such item), `habbo-update`
(that release changes fields the hotel left alone), `edited` (it holds values of the hotel's own),
`no-bundle`, `bundle-failed`, `bundle-outdated` (Habbo names a newer revision of its asset), and
`not-sold` (no visible offer on a reachable page sells it, alone or in a pack, in either catalog),
and `logic-mismatch` (below). The list is filtered by words, kind and tag, with each tag counted,
and shows each definition's server logic; the tags are worked out once and kept until a
definition, a bundle, an asset file, Habbo's release, a published catalog or the registered logics
change.

Each definition has a **matching logic**: the one a new definition of it would get, the logic the
server registers under its classname (colour variants: their base item's), else the server logic
its asset file's client logic names (`furniture_guild_customized` -> `guild_customized`, from the
newest read file in `habbo_furniture_assets`; `furniture_credit`, `furniture_roomdimmer`,
`furniture_habbowheel` and `furniture_randomstate` name `exchange`, `dimmer`, `habbo_wheel` and
`random_state`), else the logic its furnidata category stands for: a floor item in the client
category `vending_machine`, `gate`, `teleport` or `roller` is one, which its file never says
(to the client a fridge, a gate and a teleport are all `furniture_multistate`). An import gives a
new definition that logic. A definition running the default logic, or a logic the server has not
registered, where it has a matching one is tagged `logic-mismatch`: definitions made before
matching by classname existed (a group forum terminal sold as plain furniture opens no forum),
or whose logic a plugin later registered. A registered logic staff chose over the matching
one is left alone. The panel's Overview counts them, and `POST /api/gamedata/furniture/logic`
fixes them: `{}` gives every tagged definition its matching logic, `{ "ids": [...] }` those
definitions theirs, and `{ "ids": [...], "logic": "..." }` those definitions one registered logic
(the panel's picked rows). Each call is one change set in the history, rolled back as a whole;
rooms load the new logic as they load again.

**Hand items** in the Library are discovered from the avatar template's locally stored carry
actions and the hotel's figure bundles. Names come from imported `handitemN` texts; an unnamed
entry is shown by ID. Sync assets after a client release and import its texts to refresh the
catalogue. The catalogue and its PNG previews read local data only. A missing or unreadable
template explains what needs syncing; a temporary read failure keeps the last readable catalogue.
The avatar template is built from a downloaded client release: download and build that release
on Habbo updates first if the hotel does not yet have it.

The furniture filter **vending needs items** (`vending-config-missing`) lists floor definitions
running `vending_machine` whose default data has no valid positive hand-item list. Open Behaviour
to select items, choose the dispensing animation and usage policy, then Save. This uses the normal
definition edit and history, preserves unrelated JSON sections and extension fields, and keeps
custom IDs that the current catalogue does not list. Invalid JSON or section data must be corrected
in the raw field before the picker can edit it. The catalogue describes available hand items;
staff still confirm which items a particular machine gives.

**Repair vending** previews missing known item lists on existing floor definitions. Nothing is
selected automatically. Review the proposed items, animation and logic, then apply the selected
rows. Saved access policies stay as they are unless **Use default access** is explicitly checked
for a row; saved animation, extension fields and unrelated JSON sections are retained. Configured
custom item lists, wall furniture, unknown machines and custom logics are not overwritten.
Malformed sections and conflicting duplicate fields require manual correction in Behaviour.

The preview is versioned against the saved definition and known defaults. If any selected row
changed, was removed or is no longer eligible, the entire batch is rejected; refresh and review
it again. Repairs and their history commit together, and roll back through the existing history.
Loaded rooms pick up definition changes when they reload. There is no automatic startup repair.

`GET /api/gamedata/furniture/vending-repair?page=0` lists the preview in pages of at most 100.
`POST` to the same path accepts at most 100 distinct `{id, version, useDefaultUsagePolicy}`
selections under `items`. The preview requires `admin.gamedata.view`; apply also requires
`gamedata.manage`. Stale selections return 409 and invalid selections return 400.

`GET /api/gamedata/hand-items` returns the local catalogue and its revision;
`GET /api/gamedata/hand-items/{id}/preview` returns its PNG, or 404 when unavailable. Both require
`admin.gamedata.view`. Saving configuration uses `PUT /api/gamedata/furniture/{id}` and additionally
requires `gamedata.manage`.

A definition opened from the list edits every column it has: the furnidata fields, and the
server's own (`logic`, `usagepolicy`, `cangroup`, `cansell`, `extradata`, and its identity,
`classname` and `id`, its sprite id). The server's own are `FurnitureField.ServerOnly`: no Habbo
item says anything of them, so an import never compares or writes them, while edits to them are
change sets that roll back like any other. A classname another definition of the kind has, and a
sprite id another has with the same special type, are refused; extra data must be a JSON object.
The logic offered is every one the server has registered (`GET /api/gamedata/furniture/logics`),
plugins' included; a name none registered is kept, and the furniture behaves as plain furniture.
Whether it is floor or wall furniture is not edited: that changes which fields it has.

## How it works

- **Habbo's releases.** Every `ReleaseCheckMinutes`, and when staff press **Check now**, Turbo reads
  `www.habbo.<HabboDomain>/gamedata/external_variables/0` for the revision (the
  `flash-assets-<revision>` of `flash.client.url`). It then downloads `furnidata_json/0`. New
  furniture data is kept in `habbo_releases`, gzipped, by its SHA-1. Nothing is taken in until
  staff do it.
- **Habbo's hashes.** Each check first reads `www.habbo.<HabboDomain>/gamedata/hashes` (the same
  shape as the hotel's own, without the figure and effect maps the hotel builds itself), and each
  version it keeps of the furniture data, product data, texts, variables and figure data
  (`figurepartlist`) holds the hash Habbo listed it under (`habbo_hash`). The Sources tab of Habbo
  updates shows each hash and whether it changed since the hotel last took that file in. A check
  goes on without them when the list can't be read.
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
    A new floor definition assigned `vending_machine` also receives the known hand-item list,
    dispensing animation setting and usage policy from the frozen vending defaults used by
    `MapFurnitureBehaviours`. Colour variants use their base classname. Unknown machines keep
    their empty defaults; existing definitions retain their saved configuration on later imports.
    The per-furniture server configuration supplies the IDs: for example,
    [fridge.xml](https://github.com/80O/FurniData/blob/77aa627c7e05e2699442d06d1772386270f63fc1/Data/fridge.xml)
    lists drinks `3,4,5,6`, and
    [rare_icecream.xml](https://github.com/80O/FurniData/blob/77aa627c7e05e2699442d06d1772386270f63fc1/Data/rare_icecream.xml)
    lists `4`. Newer entries retain the migration's mappings from original NFT counterparts,
    external texts and Habbox documentation; those entries do not all have direct XML evidence.
  - Definitions Habbo dropped are left alone.

  The first import of a hotel that already had definitions has nothing to compare with. The
  client-only columns take Habbo's values; the server's columns keep the hotel's, and are
  reported. From then on Habbo's updates reach them too.
- **What a class does.** A definition the import makes gets the behaviour Sulake's furni data
  gives its class (`FurnitureBehaviours`, in Turbo.Database): a vending machine's hand items and
  usage, a crackable's state count, usage and contents, the effect furni's logics, the Banzai
  teleporter and the Black Hole. A colour variant takes its base item's. The
  `MapFurnitureBehaviours` migration gave the same to the definitions a hotel had before it; a
  hotel that imports after it ran gets it from the import, and a logic staff chose is left alone.
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
- **Furniture of the hotel's own** is made from its bundle. Once a furniture bundle is uploaded
  (see `docs/asset-bundles.md`), `GET /api/gamedata/furniture/draft/{bundle}` reads what its own
  asset data says (`FurnitureAssetReader`) and prefills a definition: the classname is the
  bundle's name; `xdim`, `ydim` and `height` are its logic's dimensions; `states` counts its
  state animations as an import does; it is a wall item when its logic or visualization is one
  only wall items have (`furniture_stickie`, `furniture_window`, `furniture_poster`,
  `furniture_external_image_wallitem`), otherwise a floor item. Its logic is the one the server
  registers under its classname, else the server's logic its client logic names
  (`furniture_dice` is `dice`), else plain furniture. Whether it is a seat, a bed or a surface is
  not in a bundle and is left for staff. The draft lists its colours and the definitions already
  drawn from it. `POST /api/gamedata/furniture` (`type` `floor` or `wall`, the `fields` by
  furnidata and server key, `colors`) makes the definition and one `classname*N` for each colour
  chosen, each under the sprite id above every one the hotel's definitions and Habbo's items of
  its kind use (or the `id` given), as one change set that rolls back as a whole. Its fields are
  checked as an edit's; a classname or sprite id another definition of its kind has is refused.
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

### The figure and effect maps

The client finds the library a clothing part or an avatar effect is drawn from in its figure map
(`figuremap.url`) and effect map (`effectmap.url`). Both are the hotel's own, built from its
bundles (`Turbo.Gamedata/Assets/ClientMaps.cs`) rather than Habbo's files:

- **FigureMap.json:** every clothing bundle with a file, with its revision and its parts. A library
  Habbo's `figuremap.xml` lists has the parts it lists there, kept with its row
  (`asset_bundles.map_entries`) by each asset sync. A library of the hotel's own has the parts its
  file holds (each `<size>_<action>_<type>_<id>_...` asset), read once per build of it. A file
  holds more than Habbo's map lists for about a quarter of Habbo's libraries, and the client would
  then load the wrong library for a part, so Habbo's own list is kept.
- **EffectMap.json:** every effect bundle with a file, an entry for each effect it draws - its id,
  type (`fx`, or `dance` for the dances, whose ids are `dance.1` to `dance.4`) and revision - as
  `effectmap.xml` lists them; one of the hotel's own has an `fx` entry for each id that loads it.

A bundle kept, changed or removed - by a sync, an upload or a delete - builds the map again at the
next request, and the external variables with its new address. The migration `AddClientMaps`
points `figuremap.url` and `effectmap.url` at the maps; **Gamedata → Client files** lists them.

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
| `.nitro` (old layout) | `LegacyNitroBundle` | Bundles from before they were zips: a big-endian count, then each file's name and zlib'd or gzip'd data. Its asset data keys its lists by name or id and holds the spritesheet. |

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
- **A `.nitro` of the old layout** is repacked as the current one, as a prebuilt `.hab` is: its
  JSON brought to the current layout, the spritesheet moved beside it as
  `<name>_spritesheet.json` and its frames named as their assets (the old ones carry the
  library's name in front: `lamp_lamp_64_a_0_0` is `lamp_64_a_0_0`).

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
| `/gamedata/figuremap_json/0` and `/<sha1>` | The figure map, as the client's FigureMap.json (see [The figure and effect maps](#the-figure-and-effect-maps)). |
| `/gamedata/effectmap_json/0` and `/<sha1>` | The effect map, as the client's EffectMap.json. |
| `/gamedata/external_variables/0` and `/<sha1>` | The client's configuration, the JSON object Nitro loads (see [External variables](#external-variables)). |
| `/gamedata/hashes` | Each file's current hash, in Habbo's shape: `{"hashes":[{"name":"furnidata","url":".../gamedata/furnidata_json","hash":"..."}, {"name":"external_texts", ...}]}`. The url is the file's address without its hash, built on `PublicUrl` (or the address the request came to). |

Builds are kept in `gamedata_builds`: the current one and the `KeepBuilds` before it, so a client
that loaded an address before a rebuild still finds it. The same content hashes the same on every
silo.

An edit builds the file again at once on the silo it was made on. Every silo also asks the database
whether a file's rows changed, at most every `FileCheckSeconds`: the newest change set (every
import, edit and rollback is one) and how many rows there are, or for the badge parts the rows
themselves. So another silo serves the edit within `FileCheckSeconds`, without a restart.

The external variables and texts a plugin brought and that still hold its value are written into
the files only while that plugin is loaded on the silo (`docs/client-modules.md` §5), so a theme
turned off or uninstalled with its data kept never points clients at files no longer served.

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
  review first. A variable that follows a setting or a file is never removed, nor the reception's
  (`landing.view.*` and the others the Hotel view page keeps), and a config with no keys can't
  remove anything. `renderer.petTypes` is the hotel's own, as the file addresses are: a publish
  writes it from the pet bundles, and an import neither sets nor removes it. Every edit and import is a change set and rolls back like any other.
- **A variable can follow something** instead of holding a value of its own. **Link** it on the
  Variables tab, and the file writes what it follows, built again whenever that changes:
  - **a server setting** (`Turbo:Web:HotelName`, say): its value configured now, so saving the
    setting on the Settings page builds the variables again at once on that silo, and on the
    others once they read the panel's overrides again, within `Turbo:Settings:CheckSeconds`
    (`docs/settings.md`). A secret setting can't be followed: the variables are public.
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
- **Widgets the server answers** besides the tabs above:
  - The **daily quest** (`dailyquest` widget and promo element) offers the quests' current quests
    (each campaign's first one not done), easy or hard, and the one being done while there is one.
  - The **concurrent users** element counts the players connected to the host against
    `Turbo:Catalog:Reception:ConcurrentUsersGoal` (0, the default, turns it off). Once the goal is
    reached, each player may take `ConcurrentUsersRewardBadge` once, until the host restarts.
  - The **room hopper** (`roomhoppernetwork`) sends a player to a random room of its network
    (`landing.view.roomhopper.network.id`), from the rooms listed under
    `Turbo:Navigator:RoomNetworks:<id>`.
  - The talent promo and the room competition elements have no server behind them yet: the
    talent track and room competitions are not built.

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
| `ClientEnvironment` | `auto` | Which environment's HTML client the client libraries are built from: `production`, `sandbox`, or `auto` (the first with a release). See `docs/asset-bundles.md`. |
| `ClientKeepReleases` | `5` | Releases of Habbo's HTML client kept, besides any an environment is on. |
| `ClientBuildAfterDownload` | `true` | A release of Habbo's HTML client the hotel follows is built from as soon as it is downloaded, once no other asset job runs. Off, it is built at the next asset sync, or with Build now. |
| `ClientChatStyles` | `follow` | What a release with a new chat library does to the chat styles: `follow` takes its styles and updates those not changed here since, `replace` replaces changes made here too, `off` takes nothing. |
| `ClientDownloadMinutes` | `30` | How long downloading a release may take. |
| `ClientAirImages` | `true` | The window templates take the bitmaps a library's class embeds in Habbo's newest AIR client that its library in the HTML client lacks (the AIR client is downloaded once per release for it). See `docs/asset-bundles.md`. |
| `ClientAirLayouts` | `["habbo-toolbar-com/prog_menu_view_xml"]` | The window layouts (`<library>/<asset>`) the templates take from Habbo's newest AIR client in place of its HTML client's, whose copy lacks what the client looks for (the Progression menu's list named `menu_items`). Empty takes none. See `docs/asset-bundles.md`. |
| `FurnitureFileUrl` | `https://images.habbo.{domain}/dcr/hof_furni/{revision}/{name}.swf` | Where a furniture's asset file is, read when an update is taken in. |
| `FurnitureFileConcurrency` | `8` | Furniture files downloaded at once. |
| `KeepBuilds` | `10` | Builds of each file kept besides the current one. |
| `FileCheckSeconds` | `5` | How often a silo asks the database whether a file's rows changed: how soon an edit made on another silo reaches its files. |
| `PreviewItemLimit` | `500` | Items an import review lists. Its counts cover every item. |
| `TextPageSize` | `50` | Texts per page of a search. |
| `FurniturePageSize` | `60` | Furniture definitions per page of the panel's list. |
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
  another's file within `FileCheckSeconds`, and a setting a variable follows within
  `Turbo:Settings:CheckSeconds`; **Rebuild** in the panel forces one on the silo it reaches.
