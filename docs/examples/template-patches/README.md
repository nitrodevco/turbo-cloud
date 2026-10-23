# Template patch examples

Two plugins that change the same core window, the purse (`habbo-toolbar-com/purse_xml`), without
shipping a copy of its library and without knowing about each other (`docs/extensions.md` §4.5,
nitro-next `docs/template-patches.md`).

- `purse-theme/`: a theme. Its only content is `wwwroot/template-patches.json`: it recolours the
  purse's frame and its help button. No client code; the server lists the file for clients because
  it is there.
- `purse-skills/`: an add-on with a server half and a client module. The module adds a "Skills"
  button to the same purse (`api.templates.patch`), gives it a click (`api.templates.bind`) that
  opens a window of its own (`api.windows.register`), and that window asks the plugin's server half
  for the player's level over the plugin's packets. Its server half also serves two web routes
  (`docs/plugin-web-routes.md`): `GET /api/plugins/skills/top`, the hotel's best players, for
  anyone, and `GET /api/plugins/skills/me` for the signed-in player; the window shows the best level
  through `api.http.get('top')`.

Both can be installed together, alone, or in either order: the theme's order (-10) runs it first,
and the add-on's elements are named `skills:<name>`, so neither can overwrite the other. Turbo.Tests
compiles and loads both (`TemplatePatchExampleTests`).

Build each with `dotnet build docs/examples/template-patches/<name>/<Name>.csproj` and copy
`bin/Debug/net10.0/` into the server's plugins folder, or name it in `Turbo:Plugin:DevPluginPaths`.
