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
    styles/pixel.woff2     a font its text.fonts names ({ "Pixel": "styles/pixel.woff2" })
  gamedata/
    external_texts.txt     its phrases (key=value), editable by staff
    external_variables.json
  chatstyles/
    chatstyles.json        its chat bubbles (below)
    mytheme_plane/         one folder per bubble, by its asset id: chat_bubble_base.png, ...
```

### Chat bubbles

A theme's chat bubbles need no code: `chatstyles/chatstyles.json` lists them, each with its
`clientStyleId`, its `assetId` (the folder beside the list holding its bitmaps, by the client's file
names: `chat_bubble_base.png`, `chat_bubble_pointer.png`, `selector_preview.png`, `chat_bubble_color.png`,
`icon.png`, `chat_bubble_emblem.png`, `chat_bubble_emblem_multiline.png`), its `regPoints` (the client's
object: `scale9`, `pointerY`, `faceXY`, `textFieldMargins`, ...) and who may speak with it (`clubOnly`,
`staffOnly`, `ambassadorOnly`, `purchasable`, `staffOverrideable`, `allowHtml`, `notification`; off
unless given). As the plugin loads, each bubble whose client id and asset id the hotel has neither is
added as the panel adds one (Appearance > Chat bubbles) and built into the hotel's chat bundle; one the
hotel has - its own, or the plugin's from an earlier load that staff changed since - is left as it is. A
list the server can't read, or a bitmap that isn't an image, is logged and the plugin loads.

```json
// Comments and trailing commas are allowed.
[ { "clientStyleId": 39, "assetId": "mytheme_plane", "clubOnly": true,
    "regPoints": { "scale9": { "x": 7, "y": 7, "width": 37, "height": 9 }, "pointerY": 21 } } ]
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

The hotel's own plugin extends `habbo`, so **Appearance > Style properties** lists these with no theme file:
staff restyle the hotel's frames, colours, fonts and loading screen from the panel. A theme that
extends another plugin needs it installed; one that extends in a circle is refused.

## In the panel

Everything a theme author changes is under one sidebar section, **Appearance**; the Plugins page
links to it. Every editor there saves into the theme staff are **working on** (below): this hotel's
own by default.

- **Appearance > Themes**: every plugin that is a theme (the hotel's own always) as a tree under the
  base theme `habbo`, each under the theme it `extends`, with how many style properties it has (and
  declares itself), how many staff changed, whether it is on (turn it off or on here), and links to
  its style properties, images and patches. Admin API: `GET /plugins/themes`. **New theme** makes one
  (below), **Duplicate** copies one, **Work on it** makes it the theme the editors save into.
- **Appearance > Style properties**: a theme picker (the same tree) and an area picker (the
  `theme.json` groups; the base theme's say what each restyles) over each style property as a
  picker (colour, number, image upload, font), with **Reset** and a "changed" mark. Reset says
  whose default it goes back to: **Habbo's own** for one the base theme declares, or the theme's
  that named a default again (each property's `origin` in `GET /plugins/{key}/styles`, beside the
  theme's `extends`). Saving writes the hotel's entry; **Preview** reloads the embedded client, which
  reads the new values as it starts. An uploaded image is kept in the hotel's own data plugin
  (below), so updating the theme keeps it; **Pick an uploaded image** uses one from its Images.
  A style with no default says "Habbo's own". Every appearance save says who has it: the players
  online get it at their next load, anyone logging in has it now.
- **Appearance > Images** (a theme picker, the hotel's by default): the images in its `wwwroot/styles/` (the hotel's:
  `plugins/hotel/wwwroot/styles/`). Upload a PNG, GIF, JPEG or WebP (at most 2 MB, checked by its
  content; one of the same name is replaced) and copy its **Path** (`styles/tab0.png`: what a patch's
  `asset` and an image style take, resolved against the plugin's served files) or its **Address**
  (what a variable such as `asset.overlays` takes). Uploading and removing need
  `admin.plugins.manage` and go to the command log. Admin API: `GET`/`POST /plugins/{key}/images`
  (form field `file`, optional `name`), `DELETE /plugins/{key}/images?path=styles/x.png`.
  **Fonts**, beside them: upload a WOFF2, WOFF, TrueType or OpenType file (at most 2 MB, checked by
  its content) and clients load it as the family its file name says (`pixel.woff2` is `pixel`) before
  their first text: the hotel's `text.fonts` entry `{ "pixel": "<its address>" }`. A **font** style
  property (the base theme's Text and Window titles) then offers it beside the faces the client
  ships. Admin API: `GET`/`POST /plugins/{key}/fonts` (form field `file`, optional `name`),
  `DELETE /plugins/{key}/fonts?path=styles/pixel.woff2` (the family goes with it). A theme plugin
  names its own fonts in its `gamedata/external_variables.json` `text.fonts`, each a path in its
  `wwwroot/`, as its overlays are.
- **Appearance > Template patches**: the patches by who made them - this hotel, then each plugin
  (its `wwwroot/template-patches.json`, read through `GET /plugins/{key}/template-patches`; turn the
  plugin off or on there) - each with how many players' clients report it applied / unmatched /
  ineffective (a clash beside them; a report is put beside the patch whose op names the element it
  is about), and an **outdated** line when its window or an element it names is gone from the
  client the hotel runs. The hotel's own patches: make one, edit its
  ops as JSON (checked as it is saved: an op names its `element` and does one of `set`, `remove`,
  `insert`, `move`, `wrap`, `bind`, `arrange`), turn it off without losing it, or remove it. The client's alt-click inspector
  ("copy patch") gives JSON that pastes straight in. A window's frame - its colour, style and where
  it first opens - is one patch on its root: `{ "element": "", "set": { "color": 4288688176,
  "position": { "x": 84, "y": 153 } } }`; a window the client draws without a template is named by
  its frame id instead of a template (`wired-setup`, listed in the client's `skins.json`). A `bind` op
  gives an element a value per window or per row of a list - the catalogue's tabs an icon each - with
  `${ctx.<key>}`, `${item.<key>}` (with `"items": true`), `${text.<key>}` and `${style:<name>}` filled as
  the client draws it, once (they do not nest: `${text.icon.${item.index}}` names no text); the check
  refuses an unknown field or placeholder and one inside another, and a relative `asset` is one
  of the hotel's own served files (nitro-next `docs/template-patches.md`, "A binding as data").
  An `arrange` op moves and sizes an element once the window is laid out, as a module's
  `api.templates.arrange` does: each of `x`, `y`, `width`, `height` a number or a formula over
  `${<element>.<measure>}`, `${window.<measure>}` and `${ctx.<key>}` (`${catalog.header.image.y} + 83`,
  `${ctx.hasHeaderImage} ? 290 : 0`); the check refuses an unknown field, function or measure and a broken
  formula with the client's own sentence, and one longer than 500 characters, nested deeper than 32
  (brackets, calls, choices, signs) or calling with more than 16 values (nitro-next `docs/template-patches.md`,
  "An arrangement as data").
  **Test**, under the ops, shows the window's element tree with them applied before saving: what each
  op changes, and each one naming an element the window lacks (clients would report it unmatched).
- **Appearance > Outdated patches**: every patch whose window or element is gone from the client's
  `template-index.json` (a client update renamed or removed it), with why, before any player's client
  reports it unmatched. Elements a patch adds itself (`insert`, `wrap`) count as there.
- **Appearance > Texts**: the texts windows show, by window (pick one; every text key it shows with
  the text players see, edited in place) or by what they say (type "Earnings" to find
  `earnings.title`, with the windows showing it).
- **Appearance > Windows & skins**: every window of the client - each template window and each window
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
  again: **Read again** on the page). **Upload a skin set** replaces many sprites at once: pick files,
  a folder or a zip, and each file is matched to a sprite by its name - the sprite's own
  (`theme-habbo-frame-3-frame-3-default.png`) or the skin and part (`frame-3-plain.png`,
  `button-0-hovering.png`, `border-9.png` for a one-sprite skin; a name two sprites take is no match) -
  in a review table (matched, unmatched, a later file for the same sprite, and a size that is not the
  sprite's, which a nine-slice would cut wrongly) with the art drawn in the window's preview before
  anything is saved. **Apply** uploads the matched files into the chosen plugin's images and writes
  every overlay in one change: `PUT /plugins/{key}/overlays` `{ "overlays": { "<library>/<asset>":
  "<url>" | null } }` (`admin.plugins.manage`, one command log line), every entry checked before any
  is written; a sprite the base theme declares as an image style (the window frames, the purse) is
  set through that style, so its Theme tab shows it.
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
  code. That holds while its files are data (JSON, text, Markdown, images, fonts); one bringing texts or
  variables needs `gamedata.manage` as well (`docs/admin-panel.md`, "Installing a package").

## Themes made in the panel, and where edits land

**Appearance > Themes > New theme** makes a theme as a data plugin of its own: a name, a key, and the
theme it starts from - Habbo's own (`habbo`), this hotel's, or any installed theme - which it
`extends`. The server writes `plugins/<key>/` as a package would install it - `manifest.json` with no
`AssemblyFile`, `theme.json` `{ "extends": "<parent>", "styles": {} }`, an empty
`wwwroot/template-patches.json` and `wwwroot/styles/` - and loads it. **Duplicate** makes a copy of a
theme as this hotel has it (what **Make a plugin** packages: its files, the hotel's values of its
styles as its defaults, the images uploaded for them) under a new key, with its patches' names. Both
need `admin.plugins.manage` and go to the command log. Admin API: `POST /plugins/themes`
`{ "key", "name", "extends": "habbo" | <theme> | null, "copyOf": <theme> | null }`.

The panel's **Working on** picker (a bar at the top of Style properties, Template patches, Images and
Windows & skins, with the folder it writes) is where staff edits land, XenForo's development mode: one
choice for every editor, kept in the browser, `hotel` by default. Working on a theme, its edits are
written into its own files and the theme is loaded again:

- a style property: its default in the theme's `theme.json` (an inherited one is named again with
  only its `default`; **Reset** takes it away, back to the parent's), an image by its path in the
  theme's `wwwroot/` - `PUT`/`DELETE /plugins/{key}/styles/{name}?target={key}`, an image upload
  `POST /plugins/{key}/styles/{name}/image?target={key}`. Without `target` (or `target=hotel`) the
  change is this hotel's value, as above;
- a window patch: the theme's `patches.json` (names, on/off) and the `wwwroot/template-patches.json`
  clients load - `GET`/`POST /plugins/{key}/patches`, `DELETE /plugins/{key}/patches/{id}` (the
  hotel's own are `key` `hotel`). A theme whose patches were written by hand lists those of its file,
  each named by its window, until the first save;
- images and fonts: its `wwwroot/styles/`; a font's family and a skin set's bitmaps
  (`PUT /plugins/{key}/overlays`) its `gamedata/external_variables.json` `text.fonts` and
  `asset.overlays`, each by its own path (a bitmap an image style stands in for: that style's default).

Only a data plugin's files are written; a plugin with code is its author's to change, and refused.
The theme's **Make a plugin** is then its package, with every change in it, for another hotel to
install.

## The hotel's own data plugin

What staff make is kept as a plugin too: the folder `plugins/hotel/` (key `hotel`), made the
first time something is saved. `patches.json` holds staff's patches with their names and whether
they are on; `wwwroot/template-patches.json` is what clients load (the enabled ones);
`wwwroot/styles/` holds uploaded images and fonts. It is listed on the Plugins page like any other, and
turning it off takes every staff patch away at once.

## Who gets which theme

A theme is a plugin with a `theme.json` (the hotel's own `hotel` always is one). Like XenForo's
styles, each has a **status**, may be the hotel's **default**, and may be **chosen by players**;
nothing of it is a setting, it is the hotel's data (`gamedata_themes`, `player_themes`), changed on
**Appearance > Themes** (**Who gets it**, `admin.plugins.manage`, audited) or
`PUT /api/plugins/themes/{key}/audience`.

| Status | Who draws it |
| --- | --- |
| **Live** (a theme no one set) | everyone, when it is the default (or there is no default, as before themes had an audience); a player who chose it, when players may choose it |
| **In development** | its **audience** only: the players and permission groups it names (a group held by inheritance counts), and staff with `appearance.themes.preview` who choose it or open **Preview as this theme** |
| **Off** | nobody, but as the theme one that reaches them extends |

- A client draws one theme's **chain**: the theme and every theme it `extends`, plus every plugin
  that is no theme (a client module's patches). The hotel's own entries of the merged variables are
  the `hotel` theme's, so they are drawn only where `hotel` is in the chain (its template library
  addresses, `asset.bundles.templates.overrides`, always).
- A session's theme is, in order: a development theme the player chose and may have, a development
  theme whose audience they are in, the theme they chose while it may be chosen, the default.
- The external variables every client loads are the default's. While any theme is in development or
  may be chosen, they also name `theme.session.url` (the gamedata host's `/gamedata/theme`): before
  it connects the client posts `{ "ticket": "<its login ticket>", "theme": "<staff's preview>" }`
  there (the ticket is looked at, not used up) and gets `{ theme, variables, choices, chosen }`;
  `variables` are its theme's merged variables, built once per theme, or null when they are the
  file's own. A client never takes the address from its page's query: the ticket goes there.
- **Choosing**: the client's **Other settings** lists **Hotel default** and the themes the player
  may choose; **Apply and reload** sends the Turbo named message `turbo.theme.choose { key }`
  (answered `turbo.theme.chosen { key, ok, message }`, `docs/plugin-messages.md`) and reloads.
- **Preview as this theme** opens the client page with `?theme=<key>`: honoured for that load for a
  player who may have the theme.

## Not yet

- Switching theme reloads the client: its theme's variables are read as it starts.
- Clients read styles and patch files as they start: a change reaches a player at their next load
  (the preview reloads its client for that reason).
- A data plugin's files are not watched: change one by hand and **Reload** it on the Plugins page.
- Working on a theme, **Appearance > Texts** and **Chat bubbles** still save the hotel's own (a
  plugin's texts reach clients only for keys the hotel has none of), and Windows & skins shows the
  hotel's skin replacements.
- One language per hotel: texts are the hotel's external texts, so "per language" is per hotel.
