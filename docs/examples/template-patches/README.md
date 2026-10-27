# Template patch examples

Plugins that change core windows without shipping a copy of their library (`docs/extensions.md`
§4.5/§4.6, nitro-next `docs/template-patches.md`): two that change the same window, the purse
(`habbo-toolbar-com/purse_xml`), without knowing about each other, and a skin that uses the rest of
the framework.

- `purse-theme/`: a theme, and a data plugin (`docs/plugin-data.md`): a manifest with no assembly,
  `wwwroot/template-patches.json` recolouring the purse's frame and its help button, and
  `theme.json` declaring those two colours as style properties the patches read
  (`${style:purse.frame}`), which staff change on the panel's Theme tab. No code on either side;
  the server lists the file for clients because it is there.
- `purse-skills/`: an add-on with a server half and a client module. The module adds a "Skills"
  button to the same purse (`api.templates.patch`), gives it a click (`api.templates.bind`) that
  opens a window of its own (`api.windows.register`), and that window asks the plugin's server half
  for the player's level over the plugin's packets. Its server half also serves two web routes
  (`docs/plugin-web-routes.md`): `GET /api/plugins/skills/top`, the hotel's best players, for
  anyone, and `GET /api/plugins/skills/me` for the signed-in player; the window shows the best level
  through `api.http.get('top')`.
- `infostand-skin/`: a skin. Its client module (`client/src/index.ts`, built by the server) changes the user infostand by
  the user it shows (the window's context): it tints the border, moves the motto up under the name
  keeping its name, pins the avatar (`set.params`), colours the name over the infostand's own code
  (`bind` with `override`) and makes the border taller after the window's own layout code
  (`api.templates.arrange`). Its `gamedata/external_variables.json` recolours the wired setup window,
  which has no template (`frame.overrides`), and replaces the default window frame's art with
  `wwwroot/frame.png` (`asset.overlays`, named by its path in the wwwroot).

The purse examples can be installed together, alone, or in either order: the theme's order (-10)
runs it first, and the add-on's elements are named `skills:<name>`, so neither can overwrite the
other. Turbo.Tests compiles and loads all three (`TemplatePatchExampleTests`).

Copy `purse-theme/` as it is into the server's plugins folder. Build the others with `dotnet build
docs/examples/template-patches/<name>/<Name>.csproj` and copy `bin/Debug/net10.0/` there, or name
it in `Turbo:Plugin:DevPluginPaths`.
