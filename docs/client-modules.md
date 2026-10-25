# Client modules

A plugin can bring code that runs in the client: a **client module**, a JavaScript ES module the
client loads and runs beside its own. Server code stays in the plugin's assembly; the module adds
what the player sees (a toolbar icon, a window, a drawing over an avatar) and talks to the plugin
through the plugin's named messages (`docs/plugin-messages.md`) or its own packets (`docs/plugin-packets.md`). A hotel turns a feature on or off by
installing or removing one plugin: its server half and its client half come and go together.

Client modules are a Turbo extension (`docs/client-capabilities.md`): a client that does not ask for
them (the Flash client, any other port) is never told of one and works as before.

## 1. A plugin with a module

```
plugins/hello/
  manifest.json
  Hello.dll
  wwwroot/            what clients may load: the module and the files it loads (icons, images)
    hello.js
    hello.png
  gamedata/           optional: what the plugin brings of the client's gamedata (§5)
    external_texts.txt
    external_variables.json
```

A plugin's `wwwroot/<key>.js` is its module: the host registers it as the plugin starts, unless
the plugin registered one itself, and takes it away as it stops. Nothing to write; the template's
plugin and a plugin with no plugin class at all (`docs/plugin-loading.md`) rely on that. A plugin
whose module has another name, or that decides as it starts whether to run one, registers it
through the registry its host services give it:

```csharp
public Task StartAsync(IServiceProvider services, CancellationToken ct)
{
    _module = services.GetRequiredService<IHostServices>()
        .GetRequiredService<IClientModuleRegistry>()
        .Register(Key, "hello.js");

    return Task.CompletedTask;
}

public Task StopAsync(CancellationToken ct)
{
    _module?.Dispose();

    return Task.CompletedTask;
}
```

- `Register(key, relativePath)`: `key` is the plugin's own key (another is refused), and
  `relativePath` the entry file under `wwwroot/`. A path outside `wwwroot/`, a hidden file or folder
  (`.env`, `.git/`) or a file that is not there is refused (`ArgumentException`).
- One module per plugin; a second `Register` while the first is live is refused
  (`InvalidOperationException`). The module is one file: nitro-next runs the checked bytes, so it
  cannot import files beside it; it bundles its code and loads its images and data from
  `wwwroot/` by address (§3, §4).
- Disposing the handle takes the module away. Unloading, reloading, turning off or uninstalling the
  plugin takes it away too, whether or not the plugin disposed it.
- Only a plugin can register: the registry in the host's own container has no folder to serve from
  and refuses.

`docs/examples/client-modules/hello` is a complete plugin: `HelloPlugin.cs`, its `manifest.json`,
`wwwroot/hello.js` (a toolbar icon, a me menu entry and the `hello/open` link opening a window, and
a small bar over the player's own avatar), its icon `wwwroot/hello.png`, and the texts and variables (the bar's colour and width) it brings. `Turbo.Tests` (`HelloExamplePluginTests`) builds and loads it
as a hotel would, so it keeps working as the server changes. Build it with
`dotnet build docs/examples/client-modules/hello/Hello.csproj` and copy `bin/Debug/net10.0/` to
`plugins/hello/`, or name that folder in `Turbo:Plugin:DevPluginPaths`.

### Start from the template

`templates/turbo-plugin` is a `dotnet new` template for a plugin with both halves: the server
assembly (a `TurboPlugin` of one line, a `[PluginSettings]` class for the panel, a permission node
source, a command, named messages and their handlers) and a client module in TypeScript and React written with nitro-next's
`@nitrodevco/client-module-sdk` (a window, a toolbar icon, a patch to the purse), with server and
client tests. Three commands:

```sh
dotnet new install <turbo-cloud>/templates/turbo-plugin
dotnet new turbo-plugin -n Skills --key skills --turbo <turbo-cloud> --nitro <nitro-next>
cd Skills && npm run dev
```

`npm run dev` builds both halves into `dist/skills/` and keeps it current; name that folder once in
`Turbo:Plugin:DevPluginPaths` and run the server in Development. A saved `.cs` file is rebuilt and
the plugin reloaded; a saved client file is rebundled and the plugin reloaded, so clients are sent
the module's new address and run it without a page reload (measured: about 2.5-3 s from saving a
server line, 1.5 s from saving a client line, to the change in a player's open client).
`npm run package` makes the zip the panel's plugin upload takes. The server half builds against a
server's own assemblies (`TurboBin`, by default the checkout's `Turbo.Main/bin/Debug/net10.0`),
never copied beside the plugin. `Turbo.Tests` (`PluginTemplateTests`) compiles and loads the
template's server half and checks what its project references against the server's assemblies, so
the template keeps working as the server changes; the generated `README.md` has the rest.

`npm run make <kind> <name>` adds one piece in the shape the server and the client find by
themselves (`tools/make.mjs`; the README's "Recipes" table): `command`, `message` (with its fields,
and its handler or client listener), `logic`, `listener`, `setting`, `route`, `window`, `patch`
and `migration` (the plugin's tables and `dotnet ef migrations add`, no design-time factory).
`PluginTemplateTests` runs every kind on a copy of the template and loads the result.
`npm run clean-sample` takes the template's sample out, leaving an empty plugin that loads.

### A hotel to develop a plugin on

The hosts a plugin uses are off by default. One block for the server's
`appsettings.Development.json` (or the same keys as environment variables, `Turbo__Gamedata__Enabled=true`):

```json
{
  "Turbo": {
    "Plugin": { "DevPluginPaths": [ "<plugin folder>/dist/<key>" ] },
    "Gamedata": { "Enabled": true, "PublicUrl": "http://127.0.0.1:8094" },
    "Web": {
      "Enabled": true,
      "ClientUrl": "http://localhost:5173/?sso={ticket}",
      "PluginRoutes": { "PublicUrl": "http://127.0.0.1:8092" }
    },
    "Admin": { "Enabled": true, "PanelUrl": "http://localhost:5174" },
    "Owner": { "Name": "<your player name>" }
  }
}
```

- `Gamedata` serves the module, the plugin's `wwwroot/` and its texts; with `PublicUrl` the module's
  address is absolute, so the client (nitro-next's dev server on 5173) can load it from 8094 once its
  configuration lists the host: `"client.modules.hosts": [ "127.0.0.1:8094" ]` (section 4).
- `Web` serves the plugin's routes (`docs/plugin-web-routes.md`): clients call them at
  `PluginRoutes:PublicUrl`, and `ClientUrl`'s origin may.
- `Admin` is the API behind the panel's Settings and Plugins pages; `PanelUrl` is where the panel
  runs (passkeys are bound to it), here on 5174 beside the client (`yarn dev --port 5174` in
  turbo-admin). Scripts call the API with the panel's session token (`docs/admin-panel.md`,
  "Calling the admin API").
- `Owner:Name` makes your player the owner: the `admin` group, and a setup link for your panel
  passkey in the log at the next start (`docs/admin-panel.md`, "The first admin").

## 2. The protocol: `client.modules` (version 1)

| Direction | Header | Packet | Body |
| --- | --- | --- | --- |
| server → client | 30009 | `TurboClientModulesMessage` | `int count`, then per module `string key`, `string url`, `string version`, `string sha256` |
| client → server | 30012 | `TurboClientExtensionReportMessage` | `int count` (32 read at most), then per line `string owner`, `string kind`, `string subject`, `string reason`, `string message` |

- **Sent** straight after the capability answer, to a client that accepted `client.modules`, and
  again to every such client online whenever the set changes: a plugin with a module loads, unloads
  or reloads. It is always the whole set; a module no longer listed is to be taken away (an empty
  list takes them all away). A reload is sent as two lists, without the module and then with its new
  version.
- **`key`** is the plugin's key. **`version`** is its manifest's version, for people to read.
- **`url`** is the entry file's address, ending in `?v=` and the first 16 characters of its hash, so
  every version has its own address. It is absolute when the hotel set `Turbo:Gamedata:PublicUrl`
  (`https://gamedata.example.com/gamedata/plugins/hello/hello.js?v=...`), and site-relative
  (`/gamedata/plugins/hello/hello.js?v=...`) when the gamedata host is reached on the client's own
  site.
- **`sha256`** is the lowercase hex SHA-256 of the entry file as it is served. A client checks the
  file against it before running it; empty means unchecked (the server always fills it in).
- A module whose entry file has gone from disk is left out of the list until it is back.

The presence drops the packet for a session that did not accept the extension, so a client that
never asks is never sent one, the later changes included.

**`TurboClientExtensionReportMessage`** is the client telling the server what it found running the
plugins' client halves, for the extension inspector (`docs/extensions.md` roadmap step 5). `owner`
is a plugin key; `kind` is `module.refused` (reason `host`, `hash`, `load`, `url` or `exports`),
`module.failed` (`activate` threw), `patch.unmatched` (subject the template, reason the element key),
`patch.conflict` or `patch.applied` (reason the ops applied). It is never answered. The server keeps
each distinct line per plugin in memory, counted by player and bounded (100 plugins, 50 lines each,
texts cut to 300 characters), lists it under the plugin's `GET /api/plugins/{key}/contributions`,
and logs a refused module or an unmatched patch once per line and quarter hour, naming what to change:

```
A client refused plugin rarevalues's client module http://localhost:18441/...: its origin is not in
the external variable client.modules.hosts. Add the origin to that variable (admin panel: Texts &
variables), or serve plugin files from the client's own origin.
```

## 3. How the files are served

The gamedata host (`Turbo:Gamedata`, `docs/gamedata.md`) serves every loaded plugin's `wwwroot/`
under `/gamedata/plugins/<key>/`, whether or not it registers a module: a theme or data-only plugin
whose variables name its files (`asset.bundles.templates.overrides` pointing at its template
libraries) needs no module, and clients are sent none for it. Under `/gamedata/`, so the reverse
proxy that already passes the gamedata on passes the modules too.

- GET only. A path outside `wwwroot/`, a hidden file or folder, an unknown key or a missing file is
  a 404. A plugin that unloads (turned off, uninstalled, reloading) is not served any more.
- `Access-Control-Allow-Origin: *`, as for every gamedata file: they are what every player's client
  downloads, never anything private. Do not put secrets in `wwwroot/`.
- Asked for with `?v=` matching the start of the file's own hash (the address the list gives), it is
  cached for good (`immutable`). Asked for otherwise, as the files a module loads beside it are
  (`new URL('hello.png', api.baseUrl)`), it is `no-cache` with its hash as `ETag`, answered 304 while
  unchanged. Those files are therefore always current, and a new version's entry has a new address.
- The content type follows the extension (`.js`/`.mjs` as `text/javascript`, `.json`, `.css`,
  images, fonts).
- A file changed in `wwwroot/` is served as it is now at once, but clients are sent the entry's new
  hash and address at the plugin's next load: reload it from the panel, or in development touch its
  `manifest.json` or assembly so hot reload does.

A hotel with the gamedata host off (`Turbo:Gamedata:Enabled` false) and no `PublicUrl` gets a
warning in the log as a module registers: its clients could not load it.

## 4. The client side

The client decides how a module runs; nitro-next documents its side (its loader, the host allowlist
`client.modules.hosts` for modules on another site, the shared libraries a module takes from
`api.libs` (React, the theme's components) instead of bundling its own, and the `api` object). In
short, for api version 1.1 (`api.version` 1, `api.minor` 1):

- The client refuses an address on another site unless that site is in `client.modules.hosts`.
  A refusal is a console warning naming the origin to add, and a report to the server (§2), whose
  log names the variable: if the gamedata host is on another origin than the client, add that
  origin to `client.modules.hosts` or the module never runs.
  With a SHA-256 it fetches the file, checks it, and runs the checked bytes, so the module is one
  file; `api.baseUrl` is its folder's address, for the files it loads beside it.
  Without one it imports the address.
- It calls the module's default export `activate(api)`, and `deactivate()` when the module is no
  longer listed or the client leaves.
- `api` gives a module the client's own parts rather than a copy of them: windows
  (`windows.register/open/close/toggle`, drawn with the theme's own frames, `onTop` to stay above
  the others), `toolbar.add`, `meMenu.add/toggle`, `links.handle(prefix, fn)` and `links.open`
  (Habbo's own links too, `toolbar/memenu` among them), the plugin's packets
  (`packets.registerParser/registerComposer/send`, headers 20000-32767 outside Turbo's 30000-30099),
  the plugin's named messages by name (`messages.on(name, handler)`, `messages.send(name, object)`,
  read and written from the schema `client.messages` sends: `docs/plugin-messages.md`),
  `config.get` (external variables), `texts.get` (external texts), `sounds.play`,
  `room.avatarOverlay(fn)`, `room.leave()`, `hotelView.addLayer(component, { replacesBackground })` (drawn in the
  reception behind its widgets), `hotelView.addWidget(slot, component)` (a widget in one of the
  reception's widget slots 1-6, in place of the hotel's configured one; a plugin that picks its
  widgets on the server sends them in a named message and the module adds one per slot), `http.get/post/put/patch/delete` (the plugin's own web routes, signed in as
  the player: `docs/plugin-web-routes.md`), `notifications.registerLayout` and `notifications.add` (a bubble that may `stay` up
  until the module takes it down).
- Since 1.1 a module reads the client without its internals: `state` (the own user - id, name,
  figure, gender, motto, security level - their club, credits and activity points, and the room they
  are in, read-only, with `use(selector)` for components and `hasPermission(node, level?)`, which
  asks the permission nodes the server sent: `docs/permissions-client-gates.md`), `events.on`
  (room entered and left, avatars and furni added, removed and clicked, every line said),
  `contextMenu.add` (a button in another user's avatar menu, the own avatar menu or the furni
  infostand, shown while its condition passes) and `chatCommands.register` (a `:name` run in the
  client and offered by the chat input; returning `false` sends the line on, so a server command
  of the same name still runs). `api.libs.stores` is deprecated: the stores are the client's own
  and change between releases. A command only a server can carry out (it changes the hotel) stays a
  server command (`docs/commands.md`); a module's command is for what the client does.

## 5. Texts and variables a plugin brings

A plugin's words and settings for its client half are the hotel's external texts and variables, so
staff edit them in the panel like any other. A plugin brings its own in its folder:

- `gamedata/external_variables.json`: a JSON object, as a client config is (the panel's variable
  import reads the same), comments and trailing commas allowed.
- `gamedata/external_texts.txt`: `key=value` lines, as Habbo's texts file (`#` starts a comment).

As the plugin loads, before it starts, each key the hotel has none of is added: one change set for
its variables and one for its texts, by nobody, summarised "The plugin hello added its texts", in
the gamedata history like every other change. A key the hotel has of its own is never touched, so
a reload, a restart or an update adds only what is new. A text the file cannot hold (a key with
`=`, a line break) and a variable the hotel keeps itself are left out; a variables file that is not
JSON is logged and adds nothing. The plugin loads either way.

The hotel keeps what each plugin gave (`gamedata_plugin_values`): a key still holding the value the
plugin gave is **the plugin's own**; one staff changed (or linked to a setting or file) is the
hotel's from then on.

- **Sent only while the plugin is loaded.** The client's variables and texts files leave a plugin's
  own out while it is not loaded here: turned off, uninstalled with **Keep its data**, failed to
  start, or its folder gone. A theme's `asset.bundles.templates.overrides` therefore never points
  clients at libraries that are no longer served; the client loads the hotel's own instead. The
  rows stay in the hotel (the panel lists them) and are sent again as soon as the plugin loads. A
  key the hotel changed is always sent.
- **They follow the plugin.** When a new version brings another value for a key of its own, the
  load gives the hotel the new one (one change set by nobody, "The plugin hello updated its
  variables"); a key it brings no more is taken away. A key staff changed is never moved again.
- A key the hotel already holds at exactly the plugin's value when the plugin brings it (a hotel
  that ran the plugin before this was kept) becomes the plugin's. A key another plugin owns is left
  to that one.

A key staff remove while the plugin is installed comes back at its next load, as the plugin needs
it: change its value instead.

A **merged** variable is one several plugins add to rather than own: `asset.bundles.templates.overrides`
(a url per template library, `{ "<library>": "<url>" }` or that object as JSON text). What a plugin
brings of it is never given to the hotel; the client's file carries every loaded plugin's entries
(the first plugin's by key where two name one library) with the hotel's own entries over them, so
staff point a library elsewhere, or take a plugin's away with an empty url, by changing the hotel's
value. A plugin that unloads takes its entries with it. A plugin that gave the hotel the whole
variable before keeps nothing of it there once it loads again, while the hotel's value is still the
one it gave.

Uninstalling the plugin from the panel with **Delete its data** lists, in the dry run, the variables
and texts still as the plugin gave them, and takes those away (one change set each, by the staff
member who uninstalled it). One staff changed since is the hotel's now and stays, as does any key
the hotel had before the plugin. **Keep its data** leaves them all in the hotel, but its own are
not sent to clients until it is installed again (the panel's result says so, as it does when the
plugin is turned off).

## 6. What it changed in the server

- `Turbo.Primitives`: `IClientModuleRegistry` (a plugin's), `IClientModuleDirectory` (the host's:
  the list, the files, `Changed`), `ClientModuleSnapshot`, `ClientModuleSource`,
  `TurboClientModulesMessage`, `ClientCapabilities.CLIENT_MODULES`; `IPluginGamedataSeeds`;
  `PluginDataKind.Variable` and `Text`, and `PluginDataScope.Folder`; `GetValuesAsync`, `SeedAsync`,
  `RemoveUnchangedAsync` and `ReplaceUnchangedAsync` on the variable and text services;
  `IClientModuleDirectory.AddFiles` (a plugin's `wwwroot/` without a module).
- `Turbo.Database`: `gamedata_plugin_values` (`GamedataPluginValueEntity`), what each plugin gave.
- `Turbo.Gamedata`: `ClientModuleRegistry` (paths, hashes, addresses), `ClientModuleBroadcast`
  (sends the list to everyone online on a change), the `/gamedata/plugins/` route in
  `GamedataServer`, `PluginGamedataSeeds` (the seeding and following, and an `IPluginDataOwner` for
  the uninstall), `LoadedPluginGamedata` (the plugins loaded here), and `GamedataFileService`
  leaving out the own values of a plugin that is not loaded.
- `Turbo.Plugins`: `PluginClientModules`, the registry bound to one plugin's folder, key and version,
  given through its `IHostServices` and disposed when it unloads, which serves its `wwwroot/` from the
  load; the seeding before a plugin starts, and the handle that sends its own while it is loaded.
- `Turbo.Revisions`: header 30009 in `Revision20260909`.

## 7. Tests

- `ClientModuleTests`: a plugin's module is listed with its hash and address and sent to everyone
  online, and taken away when the plugin unloads without disposing it; the public address; paths
  outside `wwwroot/`, hidden files, another plugin's key, a second module and core's registry are
  refused; the gamedata host serves the files to any site, immutable by hash, revalidated otherwise,
  404 for anything else, GET only; a client that does not ask is sent nothing; the wire layout; a
  plugin without a module has its `wwwroot/` served while it is loaded, and no module listed.
- `PluginGamedataSeedTests`: loading adds what the hotel lacks once and keeps the hotel's own; the
  uninstall dry run lists, and deleting the data removes, only what is still as the plugin gave it.
- `PluginOwnedGamedataTests`: the client's files carry a plugin's own variables and texts only while
  it is loaded (turned off, on again, uninstalled keeping its data, installed again, failing to
  start) and the hotel's changes always; its own follow a new version's values and staff's stay; a
  key already at the plugin's value becomes its own.
- `HelloExamplePluginTests`: the example in `docs/examples/client-modules/hello` builds, loads, lists
  its module and its icon, and gives the hotel its texts and variables.

## 8. Open

- **The panel** lists a plugin's data by kind; it shows variables and texts once it names the two new
  kinds (`variable`, `text`). Until then the uninstall still removes them and its summary counts them.
- **More than one module per plugin.** One entry file covers what a plugin needs today (it bundles
  the rest); a plugin that wants two independent modules can be split, or the registry can take a
  module name beside the key when one needs it.
