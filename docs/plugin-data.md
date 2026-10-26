# No-code: themes and patches in the panel

A theme, a recolour or a hidden button needs no C#. This page is what a hotel owner does in the
admin panel, and what a theme author puts in a folder. The patch format itself is nitro-next
`docs/template-patches.md`; the framework model is `docs/extensions.md` §4.5-4.6.

## A data plugin

A plugin whose `manifest.json` has no `AssemblyFile` is a **data plugin**: its files are all it
has. It is installed, turned off and on, updated, rolled back and uninstalled from the Plugins page
as any plugin is, and it runs no code - not on the server, and not in the client either (a
`wwwroot/<key>.js` in it is served as a file, never run as a module).

```
plugins/mytheme/
  manifest.json            { "Name": "My theme", "Key": "mytheme", "Version": "1.0.0", "Author": "me" }
  theme.json               its style properties (below)
  wwwroot/
    template-patches.json  its window patches, listed for clients without naming it
    frame.png              files its patches and styles name
  gamedata/
    external_texts.txt     its phrases (key=value), editable by staff
    external_variables.json
```

## Style properties (`theme.json`)

Named, typed values a theme's patches read, so staff change a colour once instead of in every op:

```json
{ "styles": {
    "frame.header": { "type": "color", "default": "#6b6861", "group": "Windows", "label": "Window header" },
    "frame.art":    { "type": "image", "default": "frame.png", "overlay": "habbo-window-manager-com/theme-habbo-frame-frame-default" },
    "purse.width":  { "type": "number", "default": 180, "min": 100, "max": 400 }
} }
```

| `type` | Staff type | Clients get |
| --- | --- | --- |
| `color` | `#rrggbb` (or `#aarrggbb`) | the `0xAARRGGBB` number a patch's `color` is |
| `number` | a number, within `min`/`max` | the number |
| `image` | an upload, or a url | its address; with `overlay`, also the hotel's `asset.overlays` entry of that bitmap |
| `font`, `text` | text | the text |
| `position` | `x,y` (`84,153`) | `{ "x": 84, "y": 153 }`, what a root patch's `position` and `frame.overrides` take |

A patch names one as a value: `{ "element": "help_button", "set": { "color": "${style:frame.header}" } }`
(the whole value, so a colour stays a number), or inside text: `"caption": "${style:shop.name}'s shop"`.
`${style:otherkey:name}` reads another plugin's. Clients get the values in the merged variable
`templates.styles` (`{ "mytheme:frame.header": 4285229153 }`): the theme brings its defaults, and
the hotel's own entries are staff's changes, kept in the variables' history and kept when the theme
is updated.

A property can also name where else its value goes, so it restyles something with no patch at all:

- `overlay` (an image): the library bitmap it stands in for, its `asset.overlays` entry.
- `variable`: a variable themes add entries to and the path in it, `[ "frame.overrides", "catalog",
  "color" ]`. One of `frame.overrides`, `loading.screen`, `text.styles.overrides`, `asset.overlays`,
  `asset.bundles.templates.overrides`, `templates.patches`, `templates.styles`. A colour goes there as
  `#rrggbb`, anything else as clients get it. An entry the theme's own `external_variables.json` gives
  stays its own.

A property with `"default": null` gives clients nothing until staff set it: the client draws its own.

## The base theme (`habbo`) and `extends`

Turbo ships Habbo's own look as a theme: `habbo`, the style properties hotels change most, each with
no default (`Turbo.Gamedata/Plugins/Themes/habbo/theme.json`). With no change a client draws exactly
as without it.

| Group | Properties | Goes to |
| --- | --- | --- |
| Windows | `window.frame`, `window.framePlain`, `window.header`: frame 3 (64x64) and its untinted pieces, header 3 (6x20), the art of the catalogue, inventory, navigator and most windows | `asset.overlays` |
| Catalogue, Navigator | `catalog.color`, `catalog.position`, `navigator.color`, `navigator.position` | `frame.overrides` |
| Purse | `purse.frame`: border 9 (15x16) | `asset.overlays` |
| Fonts | `font.text`, `font.title`: the font of the windows' text and titles | `text.styles.overrides` |
| Loading screen | `loading.background`, `loading.backgroundImage`, `loading.logo`, `loading.text`, `loading.textColor`, `loading.bar` | `loading.screen` |

A theme says `"extends": "habbo"` (or the key of another theme) and gets every property of it that it
does not name again. Naming one again changes only the fields it gives, so a theme sets Habbo's
catalogue colour and art with defaults alone:

```json
{ "extends": "habbo", "styles": {
    "catalog.color": { "default": "#b0452c" },
    "catalog.position": { "default": "330,110" },
    "window.frame": { "default": "catalog-frame.png" },
    "purse.frame": { "default": "purse-border.png" }
} }
```

The hotel's own plugin extends `habbo`, so **Plugins > Hotel theme** lists these with no theme file:
staff restyle the hotel's frames, colours, fonts and loading screen from the panel. A theme that
extends another plugin needs it installed; one that extends in a circle is refused.

## In the panel

- **Plugins > a plugin > Theme**: each style property as a picker (colour, number, image upload,
  font), grouped as `theme.json` groups them, with **Reset** back to the theme's default and a
  "changed" mark. Saving writes the hotel's entry; **Preview** reloads the embedded client, which
  reads the new values as it starts. An uploaded image is kept in the hotel's own data plugin
  (below), so updating the theme keeps it; **Pick an uploaded image** uses one from the Images below.
  A style with no default says "Habbo's own".
- **Plugins > a plugin > Theme > Images**: the images in its `wwwroot/styles/` (the hotel's:
  `plugins/hotel/wwwroot/styles/`). Upload a PNG, GIF, JPEG or WebP (at most 2 MB, checked by its
  content; one of the same name is replaced) and copy its **Path** (`styles/tab0.png`: what a patch's
  `asset` and an image style take, resolved against the plugin's served files) or its **Address**
  (what a variable such as `asset.overlays` takes). Uploading and removing need
  `admin.plugins.manage` and go to the command log. Admin API: `GET`/`POST /plugins/{key}/images`
  (form field `file`, optional `name`), `DELETE /plugins/{key}/images?path=styles/x.png`.
- **Plugins > Template patches**: every plugin's patches per window, with what players' clients
  report of them (applied, unmatched, ineffective, conflicting), and the hotel's own patches: make one, edit its
  ops as JSON (checked as it is saved: an op names its `element` and does one of `set`, `remove`,
  `insert`, `move`, `wrap`, `bind`), turn it off without losing it, or remove it. The client's alt-click inspector
  ("copy patch") gives JSON that pastes straight in. A window's frame - its colour, style and where
  it first opens - is one patch on its root: `{ "element": "", "set": { "color": 4288688176,
  "position": { "x": 84, "y": 153 } } }`; a window the client draws without a template is named by
  its frame id instead of a template (`wired-setup`, listed in the client's `skins.json`). A `bind` op
  gives an element a value per window or per row of a list - the catalogue's tabs an icon each - with
  `${ctx.<key>}`, `${item.<key>}` (with `"items": true`), `${text.<key>}` and `${style:<name>}` filled as
  the client draws it; the check refuses an unknown field or placeholder, and a relative `asset` is one
  of the hotel's own served files (nitro-next `docs/template-patches.md`, "A binding as data").
  **Test**, under the ops, shows the window's element tree with them applied before saving: what each
  op changes, and each one naming an element the window lacks (clients would report it unmatched).
- **Plugins > Windows & skins**: every window of the client - each template window and each window
  Nitro draws itself - found by its id, an element's name or key, a text key or a text it shows (the
  hotel's texts). A window lists its element tree as a patch names it (key, tag, style, name,
  caption, the text keys it shows, the skins it draws) and the patches on it with what clients report
  of them; picking an element's key starts a new patch on it, a text key opens it under Gamedata >
  Texts. **Skins** lists every skin of the theme with its sprites (drawn from the theme sheet), their
  size and nine-slice, the `asset.overlays` key that replaces each, and the windows drawing it. The
  panel reads both from the client: `template-index.json` and `skins.json`, which the client's build
  writes beside its page (nitro-next `docs/template-patches.md`, "The skin map"), through
  `GET /api/client/windows/index` and `/api/client/windows/skins` (`admin.plugins.view`; read beside
  `Turbo:Admin:ClientPreviewUrl`, else `ClientLoginUrl`, and kept 10 minutes, `?fresh=true` reads them
  again: **Read again** on the page).
- **Plugins > a data plugin > Make a plugin**: a zip of it under a new key, name and version, ready
  to install on another hotel. A theme's package carries this hotel's style values as its defaults
  (and the images uploaded for them); the hotel's own plugin's carries its enabled patches, its
  images, and a `theme.json` that extends `habbo` with the base theme's values staff changed.
- **Gamedata > Texts**, filtered to a plugin: the phrases it brought, each marked when staff changed
  it from the plugin's default, with the default beside it to put back. A label's text key is in
  its caption: the client's alt-click inspector copies a label as a patch whose `caption` is
  `"${earnings.title}"`, and `earnings.title` is the key to edit (or to give in a plugin's
  `gamedata/external_texts.txt`).
- **Installing** one needs `admin.plugins.manage` only, not `admin.plugins.install`: it runs no
  code. That holds while its files are data (JSON, text, Markdown, images); one bringing texts or
  variables needs `gamedata.manage` as well (`docs/admin-panel.md`, "Installing a package").

## The hotel's own data plugin

What staff make is kept as a plugin too: the folder `plugins/hotel/` (key `hotel`), made the
first time something is saved. `patches.json` holds staff's patches with their names and whether
they are on; `wwwroot/template-patches.json` is what clients load (the enabled ones);
`wwwroot/styles/` holds uploaded images. It is listed on the Plugins page like any other, and
turning it off takes every staff patch away at once.

## Not yet

- Clients read styles and patch files as they start: a change reaches a player at their next load
  (the preview reloads its client for that reason).
- A data plugin's files are not watched: change one by hand and **Reload** it on the Plugins page.
- One language per hotel: texts are the hotel's external texts, so "per language" is per hotel.
