# Asset bundles

The client draws furniture, clothing, avatar effects and pets from `.nitro` bundles: a zip holding the
library's asset data (`<name>.json`), its packed images (`<name>.png`) and their frames
(`<name>_spritesheet.json`). The hotel takes them from Habbo: a **sync** downloads each library
Habbo has that the hotel lacks, or has an older revision of, and converts it (`Turbo.Assets`,
`NitroConverter`). The bundles are kept on this server, and **published** to where the client loads
them: a folder on this server, or an FTP, FTPS or SFTP server. The admin panel's **Gamedata &
assets** page does all of this beside the gamedata, and checks the bundles against the hotel's
furniture, clothing, effects and pets: its **Updates** tab shows what a sync would download, each
kind's tab (Furniture, Clothing, Effects, Pets) lists its bundles with its data, and **Publish**
holds the targets.

## Uploading

The panel has one upload, for any file. The server reads what the file is from its content, not its
name or a kind chosen for it (`AssetBundleService.UploadFileAsync`):

- **A library** (an SWF, a `.hab` or a `.nitro`, by its first bytes; a `.nitro` of the old layout,
  from before bundles were zips, by its layout, repacked as the current one) is converted, and its asset
  data says its kind (`AssetKindDetector`): a `pet` logic or `pet_...` visualization is a pet; any
  other logic or visualization is furniture; an animation of its own is an effect; assets named
  `h_...` are clothing. When the data says none of these, a bundle already of that name gives its
  kind. An effect or clothing library is converted again as one, dropping its shadows. It is kept
  as an upload, which a sync never replaces.
  A furniture bundle no definition draws from yet can be made one from its own data (see
  `docs/gamedata.md`, furniture of the hotel's own): the upload offers it.
- **A gamedata file** (furniture data, product data, figure data, the figure or effect map, texts,
  variables or a client config, as JSON, XML or text; `GamedataFileDetector`) is recognised and
  not taken: the hotel builds its gamedata from the database. The panel points to where it is
  changed; a client config opens the Variables import with it.
- **Anything else** is refused as unknown (422), which the panel shows as a warning.

## Where bundles are kept

`Turbo:Assets:Directory` (default `assets`, under the server's folder) is laid out as an asset host
serves it, so it can itself be served, or published as it is:

| Kind | Folder | Named as | Client setting |
| --- | --- | --- | --- |
| Furniture | `bundled/furniture/<name>.nitro` | the classname without its `*N` colour | `furni.asset.url` |
| Effect | `bundled/effects/<lib>.nitro` | the effect map's `lib` | `avatar.asset.effect.url` |
| Pet | `bundled/pet/<name>.nitro` | the name `pet.configuration` lists | `pet.asset.url` |
| Figure (clothing) | `bundled/figures/<lib>.nitro` | the figure map's `lib` (`hh_human_body`, `shirt_U_...`) | `avatar.asset.url` |
| Template | `bundled/templates/<library>.nitro` | the client library (`habbo-free-flow-chat-com`) | `asset.bundles.templates` |
| Room | `bundled/room/<Name>.nitro` | the room's own library (`HabboRoomContent`, `TileCursor`, `SelectionArrow`, `PlaceHolderFurniture`, `PlaceHolderWallItem`, `PlaceHolderPet`) | `asset.bundles.room` |
| Furniture icon | `images/furni-icons/<asset>_icon.png`, `<asset>_<N>_icon.png` | the furniture's bundle, and its colour `*N` | `asset.urls.icons.furni` (`%libname%%param%_icon.png`) |
| Catalog icon | `images/catalog-icons/icon_<n>.png` | the icon's number | `catalog.icons.url` |
| Badge | `images/badges/<code>.gif` | the badge code | `badge.asset.url` |
| Catalog picture | `images/catalog/<name>.gif` | a page's `image_data` name | `asset.urls.catalog` |
| Image library picture | `c_images/<folder>/<name>.png` (`.gif`, `.jpg`, `.webp`) | its path under the library, folders kept (`web_promo/summer.png`); uploaded on the [media library](media-library.md) | `image.library.url` |
| Trax sample | `sounds/sound_machine_sample_<n>.mp3` | the sample's number | `flash.dynamic.download.url` (the folder's root) and `flash.dynamic.download.samples.template` (`sounds/sound_machine_sample_%typeid%.mp3`) |

This is the layout nitro-studio publishes a hotel's assets in, so a folder published from either is the
same: point each client variable above at the target's public address with the path after it. The two
wall items the client loads under another name (`RoomContentLoader`: `post.it` as `post_it`,
`post.it.vd` as `post_it_vd`) have their bundles and icons under the client's name; their files are
downloaded under Habbo's.

Each bundle has a row in `asset_bundles`: its kind and name, the revision it was taken at, where it
came from (`habbo`, `upload`, or `built` for a template the server builds from the hotel's own data: the chat styles, see `docs/admin-panel.md`), its SHA-1 and size, the ids that load it (the effects sharing an
effect's library, a pet's type), and, when it has no file, why. Files are written beside their
place and moved over it, so nothing ever reads half of one. Every server converting into the folder
must share it, and it must outlive a deploy: the Ploi scripts keep it in `.deploy/shared/assets`
(`TURBO_ASSETS_PATH`), not in a release folder a later deploy removes.

## Syncing from Habbo

A sync (`Turbo.Gamedata/Assets`):

1. Reads Habbo's `external_variables` for the client revision (`flash-assets-<revision>` in
   `flash.client.url`) and the pet list (`pet.configuration`).
2. Lists what Habbo has:
   - **Furniture**, from Habbo's furnidata: each asset name, at the highest revision any item of it
     has. The file is `Turbo:Gamedata:FurnitureFileUrl`. Two kinds are loaded under names
     furnidata doesn't list, as nitro-studio lists them (`FurnitureAssetNames`):
     - **Posters.** Furnidata's one `poster` item has no file; each placed poster carries its id
       and the client loads `poster<id>` (`poster5`) at the `poster` item's revision. The ids are
       those Habbo's external texts name (`poster_<id>_name` or `_desc`); an id with a text but no
       file (Habbo has a few) fails as any 404 does. The `poster` definition counts as having a
       bundle once any poster has one.
     - **Campaign versions** (`ads_cheetos_camp` for `ads_cheetos`, about fifty), each at its
       furniture's revision, and used while the hotel has that furniture.
   - **Effects**, from `effectmap.xml`: each library, with the effect ids that use it and the highest
     revision. Files and the map are `Turbo:Assets:GordonFileUrl` (`{name}` is `effectmap.xml`, or
     `<lib>.swf`).
   - **Pets**, from `pet.configuration`: each name, its type being its place in the list, at the
     client revision.
   - **Clothing**, from `figuremap.xml` (`GordonFileUrl` too): each `<lib id revision>`, but
     `hh_pets` and `hh_human_fx`, which are drawn from elsewhere, as nitro-studio skips them.
   - **Room libraries**, the six the client converts of its own (`GordonFileUrl`, `<Name>.swf`), at
     the client revision; a generic library, named as its file.
   - **Badges** (`BadgeFileUrl`): each code Habbo's external texts name (`badge_name_<code>` or
     `<code>_badge_name`), each the hotel has, and an achievement's levels no text names: for each
     `ACH_` badge, the levels above its highest are asked for until two in a row are missing (fifty at
     most), as nitro-studio finds them.
   - **Catalog icons** (`CatalogFileUrl`, `icon_<n>.png`, from 1) and **trax samples**
     (`SoundFileUrl`, `sound_machine_sample_<n>.mp3`, from 0), which Habbo lists nowhere: a sync asks
     for each number above the highest the hotel has (six at once) until 25 in a row are missing (100
     on the first), and stops where Habbo stops answering, going on from there next time. The preview
     asks for none and lists what the last sync found.
   - **Catalog pictures** (`CatalogFileUrl`): each name the hotel's catalog pages give in their
     `image_data`, as `<name>.gif`.

   Pictures and sounds are kept as Habbo serves them, and have no revision: once had, one is not
   taken again.
3. Skips what is up to date: a library whose row has the same revision and a file, or failed at that
   revision in a way that won't pass (Habbo has no file, the file doesn't convert). An **upload is
   never replaced** by a sync.
4. Downloads the rest (`DownloadConcurrency` at once, retried as the gamedata client retries) and
   converts it (`ConvertConcurrency` at once): furniture and pets as they are, effects as `fx`,
   clothing as `figure`. A
   download that failed is tried again on the next sync; a 404 or 403 is not, until the revision
   changes. A library whose newer revision fails keeps the file of the one before. Habbo's maps
   (`figuremap.xml`, `effectmap.xml`) that are missing or unreadable leave that kind out of the
   sync, logged, rather than failing it.

A Habbo check (the timer, or **Check now** on the Gamedata page) starts a sync when
`Turbo:Assets:SyncAfterCheck` is on and none is running. The first sync on an empty folder takes the
whole hotel: about 14,000 furniture and 2,000 clothing libraries, which takes a while and several
GB.

## Habbo's HTML client

Habbo's own client (the Electron one) carries the libraries the UI is drawn from as `.hab` files:
the window manager's skins and every window's layouts, the chat bubbles, the avatar and room
renderers' extras, the room content. The hotel takes it as nitro-studio does
(`Turbo.Gamedata/Habbo/Client/HabboClientService.cs`):

1. Each environment - `production` (`www.habbo.com`), then `sandbox` (`sandbox.habbo.com`, which
   gets releases first) - is asked `/gamedata/clienturls`: `html-windows-version` is the release,
   `html-windows` its listing (`native-release.json`) or, for an older release, a `.zip` with
   `resources/app.asar` in it. An environment with no address serves no client (production's
   `"0"`), which is no error.
2. A release the hotel lacks is downloaded in the background, once whichever environment served
   it: the listing's `app.asar` must be the size and SHA-256 it says (a web page in its place is
   Habbo's filter refusing this server), and every file the release's own
   `release-manifest.json` lists must be in it as large and hashed as listed. Only then is it kept,
   in `.habbo-client/releases/<version>/` under the bundle folder (`app.asar` and a
   `release.json`); the environments' state is `.habbo-client/environments.json`. The newest
   `Turbo:Gamedata:ClientKeepReleases` are kept, and any an environment is on.
3. The release built from is the followed environment's: `Turbo:Gamedata:ClientEnvironment`
   (`production`, `sandbox`, or `auto`, the first of them with a release).

It is asked on the release watcher's schedule (`ReleaseCheckMinutes`) and with **Check now**, and
on its own with **Check the client** under the **Habbo's HTML client** row of Habbo updates'
**Sources** table. The row shows the release built from, when Habbo built it and when the hotel
last built from it; opened, each environment, counts of the release's libraries, the releases kept
and the bundles built, and folded under those the releases kept and their libraries, each bundle
built from the release followed (marked when an upload stands in its place and is left alone),
what the last build left out and the templates' problems. The **Habbo's AIR client** row shows the
newest AIR release named, the one kept (when Habbo built it, as its zip dates `HabboAir.swf`, and
when it was downloaded, kept in `.habbo-client/air/<version>/air.json`) and how many images and
layouts the last build took from it. **Build now** builds every one again as an asset job,
whether its library changed or not. A release the hotel follows is built from as soon as it is
downloaded, once no other asset job runs (`Turbo:Gamedata:ClientBuildAfterDownload`, on by
default; off, it is built at the next asset sync).

What the hotel takes from the followed release:

- **Room libraries.** Each one the client has as `local_include/<Name>.hab` is read from it rather
  than from gordon, at the revision its manifest hash gives.
- **Chat styles.** `generated/habbo-free-flow-chat-com.hab`'s styles and the library's other
  bitmaps. Each style's look as taken is kept (`.habbo-client/chat-styles.json`), so a style
  changed here since is known - marked *changed* on the chat styles tab. A release with a new chat
  library is taken at the end of the sync as `Turbo:Gamedata:ClientChatStyles` says: `follow`
  (the default) adds its new styles and updates every one not changed here, `replace` replaces the
  changed ones too, `off` takes nothing. **From Habbo's client** on the chat styles tab takes them
  at any time, and asks whether to replace the ones changed here.
- **Texts.** `habbo-localization-com`: the client's embedded texts, `default_localizations` with
  each language's `default_localizations_<lang>` over it, as `DefaultLocalizations_<lang>.json`
  (13 languages in a current release).
- **The renderers' own libraries**, built as nitro-studio builds them
  (`Turbo.Gamedata/Habbo/Client/ClientBundleBuilder.cs`) at the end of every asset sync and kept as
  templates with the source *built*:
  - `habbo-avatar-render-lib`: the avatar additions (the typing and muted bubbles, the idle Zs, the
    numbers) on one sheet as `avatar_addition_<name>`, and the tables the renderer starts from -
    `geometry.json`, `part-sets.json`, `figure-data.json` (the placeholder figure),
    `built-in-animations.json`, `action-offsets.json` and `animations.json`, converted from the
    library's XML - with `actions.json` from Habbo's `HabboAvatarActions.xml` in the gordon folder.
    Without the actions an avatar only stands, so when they can't be read a bundle built with them
    is kept.
  - `habbo-room-object-visualization-lib`: the Variable FX bitmaps on one sheet, and
    `variable-fx-tables.json` - each icon's offset and the renderer each id draws with.

  A bundle is stamped with its library's hash (and the actions'), so a sync builds it again only
  when they change; one uploaded on the Assets tab is left alone.
- **The window templates** (`ClientTemplates.cs`, nitro-studio's `packTemplateBundles`): a bundle
  for each library with a `<layout>`, and the window manager's, which every one draws from. Each has
  `templates.json` - every layout converted as nitro-theme's `layoutToTemplate` converts it, keyed
  `<library>/<asset>`, a frame's content area from its window layout and a composite window's
  window layouts (`skins`) from the window manager's element description - the asset data, and
  every bitmap the library has as `<library>-<asset>.png` (a layout's bitmap is found in its own
  library, else the window manager's). A layout the client would refuse and a bitmap no library has
  are listed in the sync's log as template problems. The bundles are stamped with the release's
  libraries and built again together when one changes; the bundle of a library the release no
  longer has is taken away, and an uploaded one is left alone.
  Habbo puts some bitmaps in its AIR client only (`cursed_dragon_adoption_icon`, which the window
  manager's class embeds since build 1745), so the newest AIR client the environments' clienturls
  name (`flash-windows-version`, `flash-windows`: a `HabboWin.zip`) is downloaded when the
  templates are built and its `HabboAir.swf` kept in `.habbo-client/air/<version>/`, the newest
  only. Each image a library's class (the one its manifest names, `HabboWindowManagerCom`) embeds
  there - a static field its static initializer sets to an image's class - that the library lacks
  goes into its bundle under the field's name, never over one the library has, and the build's
  notes list them ("taken from Habbo's AIR client"). The bundles are built again when the AIR
  release changes. `Turbo:Gamedata:ClientAirImages` (on by default) turns it off.
  Some layouts the HTML client has lack what the client looks for, where the AIR client's have it:
  the Progression menu (`habbo-toolbar-com/prog_menu_view_xml`) adds a reward track's item only
  into the list named `menu_items` (build 1745's `ProgMenuController.as:185`), which the HTML
  client 95 leaves unnamed. The layouts `Turbo:Gamedata:ClientAirLayouts` names
  (`<library>/<asset>`, that one by default) are taken from the AIR client in place of the
  library's own - the static field its class embeds the layout in, found as an image is - and the
  build's notes list them ("layouts taken from Habbo's AIR client"); one the AIR client lacks keeps
  the HTML client's. Changing the list builds the bundles again.
- **The UI theme** (`Habbo/Client/Theme/`, nitro-studio's `clientTheme` / `themeVariants` /
  `skinArt`): built from the window manager library's element description, window layouts and
  skins, and put into `habbo-window-manager-com.nitro` beside its templates as Studio's `withTheme`
  puts it - `theme.png` (every sprite on one sheet, each edge extruded into a 1-pixel gutter),
  `theme_spritesheet.json` and `theme-variants.json` - with its `theme-*` sprites named in the asset
  data. `ThemeRules.cs` is a copy of Studio's `theme-rules.json`.

## Publishing

A publish target is kept in `asset_publish_targets`, edited on the Publish tab:

| Field | |
| --- | --- |
| Name | What staff call it. |
| Protocol | `folder` (a path on this server), `ftp`, `ftps` (explicit TLS) or `sftp`. |
| Host, Port, User, Password | For FTP, FTPS and SFTP. Port 0 is the protocol's own. |
| Remote path | The folder the asset host serves from; `bundled/...` goes under it. A path on this server for `folder`. |
| Public URL | Where the client reaches it, for the addresses the panel shows. |
| Allow self-signed | FTPS only. |
| Cloudflare zone, token | Optional: the zone the public URL is served through, and an API token allowed to purge its cache. |

The password is sealed with AES-GCM before it is saved, and never sent back to the panel; leaving it
empty when editing keeps it. The key is `Turbo:Assets:SecretKey` (32 bytes, base64), or, when that is
empty, one made once and kept in the bundle folder as `.publish-key`, so a database dump alone does
not give the passwords away. SFTP host keys are trusted on first connection and must match after; a
changed key refuses to connect until staff forget the old one.

A publish sends only what the target lacks or holds an older copy of, by the hash recorded for it in
`asset_published_files`, largest first, `PublishConcurrency` at once. Each file is uploaded beside its
place and renamed over it. What is sent is recorded as it goes, so a publish that stops resumes where
it was. A **dry run** only counts. **Delete removed** also deletes from the target what the hotel no
longer has. Each publish is kept in `asset_publishes`, the target's history.

Before it sends anything (not on a dry run), a publish draws the furniture's icons
(`PublishedFurnitureIcons`) and makes its pets known (`PublishedPets`).

**Furniture icons** are drawn from each furniture bundle as nitro-studio draws them
(`Turbo.Assets/Conversion/FurnitureIcon.cs`, a port of its `shared/furni-icon.ts`; on a sample of
Habbo's furniture every icon, each colour too, is the Studio's to the pixel): the size-1
visualization's layers, each its `<type>_icon_<letter>` asset at its offset, alpha and ink, tinted by
the colour, back to front by `z + id`, a group furniture's colour layers in a group's default colours.
One is drawn for the bundle and one for each colour `classname*N` the hotel's furniture has. Its row
keeps the bundle's hash as its revision, so it is drawn again only when the bundle changes; one no
bundle or colour wants any more is removed (and, with **Delete removed**, from the target). A bundle
with no icon to draw from has Habbo's (`FurnitureIconUrl`, at its revision), as the Studio takes it.

Then the pets:

- **`renderer.petTypes`**, the client's pet types, where a type's id is its place in the list. Its
  order is kept, since pets in the database are numbered by it. A pet bundle from Habbo fills the
  places its type ids name (from Habbo's `pet.configuration`: `gnome` is both 26 and 27), growing the
  list to them; a place the hotel set to another pet stays as it is, and is logged. The hotel's own
  pet bundles, and one whose places were all taken, come after. A change is saved as a variable
  change, so it is in the gamedata history.
- **`pet_breeds`**: every palette a type's bundle can be drawn in that has no row yet, with the
  bundle's `breed` (else the palette's id) and `colorTag` (else -1). A master palette, and one for a
  part alone (tags without `body`: hair, a tail, a hat), are left out. Rows already there keep their
  rarity and sellability, and the server's pet breeds are read again.

The job's log says what changed; the history entry's error names the first file that failed, and why.

### Comparing with the target's copy

A file pending as **changed** can be compared with the copy the target holds, before it is sent:
**Compare** on its row (or **Compare all**, the 100 largest changed files) reads the target's copy
through the target itself (folder, FTP, FTPS or SFTP, so it works without a public URL) and says what
differs from the hotel's copy. It needs `gamedata.manage`, as testing a target does: it connects with the
target's stored password. A bundle is unpacked to compare it only up to 32 MB a file and 128 MB in all (the
target's copy comes from outside the hotel); past that it is `unreadable`, saying which.

- a bundle's window templates (`templates.json`) added, removed or changed, and for a changed one its
  elements added, removed or changed by name and the text keys its elements name (`${catalog.title}`);
- its texts (`DefaultLocalizations_<language>.json`), per language: keys added, removed and changed,
  with the text before and after;
- every other file in it (images, asset data) added, removed or changed, with its bytes and, for a PNG
  or a GIF, its pixel size on each side; the panel shows both sides of an image.

The client build stamp a JSON file is packed with (`"version":"99_classic-js-69df354f4175"`) is set
aside: a bundle whose only difference is that stamp is **stamp only**, nothing changes for players.
Each comparison also shows the hotel's bundle row (its source, revision and when it last changed),
the stamp of each copy, and whether the target's copy is the one recorded as sent. A copy read is kept
in memory (64 MB in all, by target, path and recorded hash) until a publish sends the file again.

### Purging Cloudflare

A target with a Cloudflare zone and token asks Cloudflare, after each publish that sent something, to
purge exactly the addresses of the files it sent (the public URL and each file's path), 30 a request
(`POST {CloudflareApiUrl}/zones/{zone}/purge_cache` with `files`): a file that was missing is otherwise
served from Cloudflare's cache as missing for hours after it is published. The token is sealed like the
password, never sent back to the panel and never logged; a zone needs the public URL. A purge that
fails is logged, and the history keeps how many addresses were purged and why one failed (`purged`,
`purgeError`); the files were sent either way.

## Broken assets

The Publish page's **Broken assets** tab lists everything players can't load, from four sources, each
with the fixes that apply (`habbo`: take it from Habbo with a sync, or the catalog pictures by name;
`upload`; `publish`):

| Source | What it lists |
| --- | --- |
| `client` | The addresses players' clients were refused (`Invalid response 404 for <url>`), as the client errors plugin's notices of new and returning issues tell them (`docs/plugin-notices.md`), kept in `asset_broken_reports`. Read only while that plugin is loaded. An address under a target's public URL whose file the hotel has is published (the target, or a cache in front of it, lacks it); one it does not have is taken from Habbo or uploaded. |
| `check` | Everything the checks below found that is broken (not `furniture-unused`), whole. |
| `catalog` | The pictures catalog pages show that the hotel does not have, with the pages. |
| `badge` | The badges players hold with no picture, by how many players hold each. |

Staff may ignore one with a note (`asset_broken_ignores`); it stays listed, marked, until it is
brought back.

## Checks

| Check | Severity | What it finds |
| --- | --- | --- |
| `furniture-missing` | error | Furniture definitions whose asset name has no bundle: the client draws a placeholder. |
| `file-missing` | error | Bundles whose row says they have a file that the folder doesn't have. |
| `failed` | warning | Libraries that could not be downloaded or converted, with why. |
| `effects-missing` | warning | Effects the catalog sells that no bundle carries. |
| `pets-missing` | warning | Pet types with breeds that have no bundle. |
| `furniture-unused` | warning | Furniture bundles no definition names. |

Each lists how many, and up to `CheckSampleLimit` of them.

## Settings (`Turbo:Assets`)

| Setting | Default | |
| --- | --- | --- |
| `Directory` | `assets` | Where bundles are kept. |
| `GordonFileUrl` | `https://images.habbo.{domain}/gordon/flash-assets-{revision}/{name}` | Where Habbo's client libraries and effect map are. |
| `SyncAfterCheck` | `true` | Sync after a Habbo check. |
| `DownloadConcurrency` | `6` | Downloads at once. |
| `ConvertConcurrency` | `4` | Conversions at once. |
| `PublishConcurrency` | `4` | Uploads at once. |
| `PublishTimeoutSeconds` | `30` | How long connecting to a target may take. |
| `UploadMaxMegabytes` | `64` | The largest file an upload in the panel may be. |
| `JobLogLimit` | `300` | Lines of a job's log kept. |
| `PageSize` | `60` | Bundles per page of the panel's list. |
| `CheckSampleLimit` | `25` | Examples each check lists. |
| `CloudflareApiUrl` | `https://api.cloudflare.com/client/v4` | Cloudflare's API, for a target's purge. |
| `CloudflarePurgeBatch` | `30` | Addresses purged a request (Cloudflare takes up to 30). |
| `SyncPreviewMinutes` | `15` | How long Habbo's lists are kept for the view of what a sync would download; read again when asked, and by every sync. |
| `SyncPreviewItemLimit` | `500` | Libraries each kind of that view lists; its counts cover them all. |
| `BadgeFileUrl` | `https://images.habbo.{domain}/c_images/album1584/{name}` | Where Habbo's badges are; `{name}` is `<code>.gif`. |
| `CatalogFileUrl` | `https://images.habbo.{domain}/c_images/catalogue/{name}` | Where Habbo's catalog icons and pictures are. |
| `SoundFileUrl` | `https://images.habbo.{domain}/dcr/hof_furni/mp3/{name}` | Where Habbo's trax samples are. |
| `FurnitureIconUrl` | `https://images.habbo.{domain}/dcr/hof_furni/{revision}/{name}_icon.png` | Habbo's icon of a furniture, for a bundle with none to draw (`{name}` is the classname, `*` as `_`). |
| `SecretKey` | empty | The key passwords are sealed with (secret). |

## Admin API

Under `/api/assets`. Reading needs `admin.gamedata.view`; changing needs `gamedata.manage` as well.
Kinds are `furniture`, `effect`, `pet`, `figure`, `template`, `room`, `furni-icon`, `catalog-icon`,
`badge`, `catalog-image` and `sound`; protocols `folder`, `ftp`, `ftps`, `sftp`. Times are UTC.
A refusal is the panel's usual `{ "message": "..." }` (`AdminResults.Error`) with 400, 404 or 409.

**Job** (`AssetJob`): `{ id, kind: "sync"|"publish", title, status: "running"|"done"|"failed"|"canceled",
phase, total, done, failed, log: string[], error, result, playerId, startedAt, finishedAt }`. One job
runs at a time; starting another while one runs is 409.

| Call | Answer |
| --- | --- |
| `GET /assets` | `{ directory, canManage, kinds: [ { kind, bundles, failed, bytes } ], job: AssetJob \| null, checks: { errors, warnings }, targets }`. `bundles` and `bytes` count the bundles with a file; `checks` counts the checks that found something, by severity; `targets` the publish targets. |
| `GET /assets/bundles?kind=&q=&status=&page=` | `{ items: [ AssetBundle ], total, pageSize }`. `status` is `all`, `ok`, `failed` or `unused`; `q` matches the name, or an id. `page` from 0. |
| `GET /assets/bundles/{kind}/{name}` | `AssetBundle` and `{ path, files: [ { name, size } ] }`, what the zip holds. |
| `GET /assets/bundles/{kind}/{name}/file` | The `.nitro` file. |
| `POST /assets/bundles` | Multipart `file` (a `.swf`, `.hab` or `.nitro`), `kind`, and `name` (default: the file's). Converts and keeps it as an upload; answers the `AssetBundle`. |
| `POST /assets/upload` | Multipart `file` and `name` (default: the file's), any file, found by its content (see Uploading). `{ type: "bundle", bundle: AssetBundle }` or `{ type: "gamedata", gamedata: "furnidata"\|"productdata"\|"figuredata"\|"figuremap"\|"effectmap"\|"texts"\|"variables" }`; 422 with why when it can't be told what it is, 400 for a file it can't take. |
| `GET /assets/pending?relist=` | What a sync would download, without downloading it: `{ domain, revision, listedAt, kinds: [ { kind, listed, habboLibraries, upToDate, uploads, new, updated, items: [ { name, reason: "new"\|"updated", habboRevision, hotelRevision, error } ], truncated } ] }`, furniture, clothing, effects, pets, room libraries, badges, catalog icons and pictures, and trax samples. Habbo's lists are read again after `SyncPreviewMinutes`, or now with `relist=true`; 502 when Habbo can't be read. |
| `DELETE /assets/bundles/{kind}/{name}` | 204. A Habbo library is downloaded again by the next sync. |
| `POST /assets/sync` | Starts a sync; answers the `AssetJob`. |
| `GET /assets/job` | The job running or last run; 204 before the first. |
| `POST /assets/job/cancel` | 204; 409 when none runs. |
| `GET /assets/checks` | `{ items: [ { id, severity: "error"\|"warning", title, detail, count, samples: string[], kind, status } ] }`. `kind` and `status` (when set) open the bundle list the check is about. |
| `GET /assets/targets` | `{ items: [ AssetTarget ] }` |
| `POST /assets/targets` | Body `AssetTargetInput`; answers the `AssetTarget`. |
| `PUT /assets/targets/{id}` | Body `AssetTargetInput`; `password` null keeps it, `""` clears it; `cloudflareToken` likewise. `cloudflareZoneId` empty purges nothing. `AssetTarget` says `cloudflareZoneId` and `hasCloudflareToken`, never the token. |
| `DELETE /assets/targets/{id}` | 204, with what it recorded and its history. |
| `POST /assets/targets/{id}/test` | `{ ok, message }`: connected and listed the folder, or why not. |
| `POST /assets/targets/{id}/publish` | Body `{ dryRun, deleteRemoved }`; answers the `AssetJob`. |
| `POST /assets/targets/{id}/forget-host-key` | 204: the next SFTP connection trusts the key it is shown. |
| `GET /assets/targets/{id}/compare?path=` | Needs `gamedata.manage` (it reads the target with its stored password, as a test does). `{ items: [ AssetComparison ], total }`: the file at `path` (404 when the hotel has none), or without it every file pending as changed, the 100 largest (`total` counts them all). |
| `GET /assets/targets/{id}/compare/image?path=&entry=&side=` | A PNG or GIF a comparison names: the file at `path`, or its `entry` in that bundle, as the target holds it (`side=target`) or the hotel does; 404 for anything else. |
| `GET /assets/targets/{id}/history` | `{ items: [ { id, startedAt, finishedAt, playerId, playerName, dryRun, uploaded, skipped, deleted, bytes, error, purged, purgeError } ] }`, newest first. |
| `GET /assets/broken` | `{ sources: [ { id, title, read, note, count } ], items: [ { source, key, title, kind, name, detail, count, actions: ("habbo"\|"upload"\|"publish")[], link, ignored: { note, playerId, playerName, ignoredAt } \| null } ] }`. A source's `count` leaves out the ignored. |
| `POST /assets/broken/ignore` | Body `{ source, key, note }`; 204. |
| `POST /assets/broken/unignore` | Body `{ source, key }`; 204, 404 when it was not ignored. |
| `POST /assets/broken/badge-picture` | Multipart `code` and `file` (a GIF or a PNG): kept as `images/badges/<code>.gif`, recorded so a publish sends it; 204, 400 with why. |

`AssetBundle`: `{ kind, name, revision, source: "habbo"|"upload", hash, size, ids: number[], error,
updatedAt, used }`. `used` is whether the hotel names it: a furniture definition for furniture, a
breed's type for a pet; an effect and a clothing library are always used.

`AssetTarget`: `{ id, name, protocol, host, port, user, hasPassword, remotePath, publicUrl,
allowSelfSigned, hostKey, pending, lastPublish }`. `pending` is how many bundles it lacks or holds an
older copy of; `lastPublish` is the newest history entry, or null.

`AssetComparison`: `{ kind, name, path, size, result: "changed"|"stamp-only"|"same"|"not-on-target"|"unreadable",
problem, targetSize, targetAsRecorded, source, revision, updatedAt, stampBefore, stampAfter, windows: [ { id,
change, elementsAdded, elementsRemoved, elementsChanged, textKeysAdded, textKeysRemoved } ], texts: [ { language,
added, removed, changed, edits: [ { key, change, before, after } ] } ], entries: [ { name, change, bytesBefore,
bytesAfter, widthBefore, heightBefore, widthAfter, heightAfter } ], entriesChanged }`. `change` is `added`,
`removed` or `changed`; `edits` holds a language's first 200 keys and `entries` the first 300 files
(`entriesChanged` counts them all).

`AssetTargetInput`: `{ name, protocol, host, port, user, password, remotePath, publicUrl, allowSelfSigned }`.
