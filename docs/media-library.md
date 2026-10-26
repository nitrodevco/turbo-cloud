# Media library

Gamedata > Media library in the [admin panel](admin-panel.md) is one searchable index of every image
the hotel can show, wherever it is kept, and where each one is shown. Staff find a picture there by
name, path or tag instead of typing an address, copy the value a field stores for it, and upload new
ones. The admin API under `/api/media` serves the same index to the panel's image picker.

## What it lists

| Source | Where it is | Id |
| --- | --- | --- |
| Image library (`library`) | the asset folder's `c_images/`, the folder `image.library.url` names: hotel view, notice and front page promo pictures (`web_promo/`, `web_promo_small/`, `reception/`, ...) | `library:web_promo/summer.png` |
| Asset folder (`assets`, or `habbo` / `upload` by its record) | catalog pictures (`images/catalog`), catalog icons, badges and furniture icons ([asset bundles](asset-bundles.md)) | `catalog:<file>`, `catalog-icon:icon_<n>.png`, `badge:<code>.gif`, `furni-icon:<file>` |
| Client (`client`) | the PNGs of the `habbo-window-manager-com` template bundle, which a notice names without a `/` | `client:<name>` |
| Plugin (`plugin`) | each plugin's `wwwroot/styles/` images, the hotel's own data plugin's among them ([plugin data](plugin-data.md)) | `plugin:<key>/styles/<file>` |

A picture Habbo's sync brought in says so (`habbo`, with its revision); one staff uploaded says
`upload`. The library reads the folders and records again every 15 seconds, and at once after a
change made through it.

## What a field stores

Each image gives the values the kinds of fields store for it, under `values`:

| Value | Example | Fields |
| --- | --- | --- |
| `relative` | `web_promo/summer.png` | hotel view articles, front page items, notices and variables under `${image.library.url}` |
| `catalogPicture` | `catalog_header_furni` | a catalog page's headers and teasers |
| `catalogIcon` | `12` | a catalog page's icon |
| `clientImage` | the PNG's name in the bundle | a notice's image by name |
| `badge` | `ACH_Login1` | badges |
| `plugin` | `styles/frame.png` | a theme's image properties and patches |
| `url` | `https://images.example.com/c_images/web_promo/summer.png` | anything taking an address; empty when the image has none of its own |

## Where it is shown

An image's `usedOn` lists what shows it: notices (`notifications.new_feature.image.<key>`), other
external variables, hotel view articles, catalog pages (pictures and icon), catalog front page
items, campaign calendars and their gift images, and theme values. A value matches an image by its
path where it is kept, so a relative path, `${image.library.url}...` and a full address on any host
ending in the image's path all count.

An image anything shows can't be deleted or renamed: the answer is 409 with the places
(`{ message, usedOn }`), so staff change them first. The client's and plugins' images are not
deleted here (a plugin's go on its Theme tab).

## Uploading

Upload one or many images at once (drop them onto the page, or choose them): PNG, GIF, JPEG or WebP,
each at most 4 MB and named for its type. They go into the image library's chosen folder
(`c_images/<folder>/`), or into the catalog pictures, and are recorded like any asset, so the next
[publish](asset-bundles.md#publishing) sends them where the client loads them. Players see them once
the assets are published.

Tags (lowercase, found by search) are kept in the asset folder's `.media/tags.json`, beside the
images they name, so every server sharing the folder sees them; a publish does not send it.

## API

Reading needs `admin.gamedata.view`; uploading, renaming, tagging and deleting need `gamedata.manage`
as well, and each change goes to the command log as `media`.

| Call | Answers |
| --- | --- |
| `GET /api/media?q=&kind=&source=&shape=&used=all\|used\|unused&minWidth=&maxWidth=&minHeight=&maxHeight=&sort=name\|modified\|size\|used&desc=&page=&size=` | `{ total, items, facets: { kinds, sources }, canManage }`; `kind`, `source` and `shape` take comma lists, `shape` keeps images that give that value (`relative`, `catalogpicture`, `catalogicon`, `clientimage`, `badge`, `plugin`, `url`) |
| `GET /api/media/item?id=` | one image with `usedOn` |
| `GET /api/media/file?id=` | its bytes, before any publish |
| `POST /api/media` (form: `files`, `folder`, `kind`) | `{ items, refused: [{ file, why }] }`; `kind=catalogue-picture` keeps them as catalog pictures |
| `PATCH /api/media/item?id=` `{ name?, tags? }` | the image; a rename keeps its folder and type |
| `DELETE /api/media/item?id=` | 204, or 409 `{ message, usedOn }` |
