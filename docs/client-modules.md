# Client modules

A plugin can bring code that runs in the client: a **client module**, a JavaScript ES module the
client loads and runs beside its own. Server code stays in the plugin's assembly; the module adds
what the player sees (a toolbar icon, a window, a drawing over an avatar) and talks to the plugin
through the plugin's own packets (`docs/plugin-packets.md`). A hotel turns a feature on or off by
installing or removing one plugin: its server half and its client half come and go together.

Client modules are a Turbo extension (`docs/client-capabilities.md`): a client that does not ask for
them (the Flash client, any other port) is never told of one and works as before.

## 1. A plugin with a module

```
plugins/hello/
  manifest.json
  Hello.dll
  wwwroot/            what clients may load: the module and anything it imports
    hello.js
  gamedata/           optional: what the plugin brings of the client's gamedata (§5)
    external_texts.txt
    external_variables.json
```

The plugin declares its module as it starts, through the registry its host services give it:

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
  (`InvalidOperationException`). A module that needs more files imports them (§3).
- Disposing the handle takes the module away. Unloading, reloading, turning off or uninstalling the
  plugin takes it away too, whether or not the plugin disposed it.
- Only a plugin can register: the registry in the host's own container has no folder to serve from
  and refuses.

`docs/examples/client-modules/hello` is a complete plugin: `HelloPlugin.cs`, its `manifest.json`,
`wwwroot/hello.js` (a toolbar icon opening a window, and a small bar over the player's own avatar)
and the texts and variables it brings. `Turbo.Tests` (`HelloExamplePluginTests`) builds and loads it
as a hotel would, so it keeps working as the server changes. Build it with
`dotnet build docs/examples/client-modules/hello/Hello.csproj` and copy `bin/Debug/net10.0/` to
`plugins/hello/`, or name that folder in `Turbo:Plugin:DevPluginPaths`.

## 2. The protocol: `client.modules` (version 1)

| Direction | Header | Packet | Body |
| --- | --- | --- | --- |
| server → client | 30009 | `TurboClientModulesMessage` | `int count`, then per module `string key`, `string url`, `string version`, `string sha256` |

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

## 3. How the files are served

The gamedata host (`Turbo:Gamedata`, `docs/gamedata.md`) serves every live module's `wwwroot/`
under `/gamedata/plugins/<key>/`. Under `/gamedata/`, so the reverse proxy that already passes the
gamedata on passes the modules too.

- GET only. A path outside `wwwroot/`, a hidden file or folder, an unknown key or a missing file is
  a 404. A module taken away is not served any more.
- `Access-Control-Allow-Origin: *`, as for every gamedata file: they are what every player's client
  downloads, never anything private. Do not put secrets in `wwwroot/`.
- Asked for with `?v=` matching the start of the file's own hash (the address the list gives), it is
  cached for good (`immutable`). Asked for otherwise, as a module's own imports (`import './chunk.js'`)
  are, it is `no-cache` with its hash as `ETag`, answered 304 while unchanged. A module's relative
  imports therefore always load the files beside it, and a new version's entry has a new address.
- The content type follows the extension (`.js`/`.mjs` as `text/javascript`, `.json`, `.css`,
  images, fonts).
- A file changed in `wwwroot/` is served as it is now at once, but clients are sent the entry's new
  hash and address at the plugin's next load: reload it from the panel, or in development touch its
  `manifest.json` or assembly so hot reload does.

A hotel with the gamedata host off (`Turbo:Gamedata:Enabled` false) and no `PublicUrl` gets a
warning in the log as a module registers: its clients could not load it.

## 4. The client side

The client decides how a module runs; nitro-next documents its side (its loader, the host allowlist
`client.modules.hosts` for modules on another site, the shared libraries a module imports instead of
bundling its own, and the `api` object). In short, for api version 1:

- The client imports the module (`import(url)`), checks its SHA-256 first when one is given, and
  refuses an address on another site unless that site is in `client.modules.hosts`.
- It calls the module's default export `activate(api)`, and `deactivate()` when the module is no
  longer listed or the client leaves.
- `api` gives a module the client's own parts rather than a copy of them: windows
  (`windows.open/close/register`, from the client's own window templates), `toolbar.add`,
  `meMenu.add`, `links.handle(prefix, fn)`, the plugin's packets
  (`packets.registerParser/registerComposer/send`, headers 20000-32767 outside Turbo's 30000-30099),
  `config.get` (external variables), `texts.get` (external texts), `sounds.play`,
  `room.avatarOverlay(fn)` and `notifications.registerLayout`.

## 5. Texts and variables a plugin brings

A plugin's words and settings for its client half are the hotel's external texts and variables, so
staff edit them in the panel like any other. A plugin brings its own in its folder:

- `gamedata/external_variables.json`: a JSON object, as a client config is (the panel's variable
  import reads the same), comments and trailing commas allowed.
- `gamedata/external_texts.txt`: `key=value` lines, as Habbo's texts file (`#` starts a comment).

As the plugin loads, before it starts, each key the hotel has none of is added: one change set for
its variables and one for its texts, by nobody, summarised "The plugin hello added its texts", in
the gamedata history like every other change. A key the hotel has, its own or one staff changed, is
never touched, so a reload, a restart or an update adds only what is new. A text the file cannot
hold (a key with `=`, a line break) and a variable the hotel keeps itself are left out; a variables
file that is not JSON is logged and adds nothing. The plugin loads either way.

A key staff remove while the plugin is installed comes back at its next load, as the plugin needs
it: change its value instead.

Uninstalling the plugin from the panel with **Delete its data** lists, in the dry run, the variables
and texts still as the plugin gave them, and takes those away (one change set each, by the staff
member who uninstalled it). One staff changed since is the hotel's now and stays, as does any key
the hotel had before the plugin. **Keep its data** leaves them all.

## 6. What it changed in the server

- `Turbo.Primitives`: `IClientModuleRegistry` (a plugin's), `IClientModuleDirectory` (the host's:
  the list, the files, `Changed`), `ClientModuleSnapshot`, `ClientModuleSource`,
  `TurboClientModulesMessage`, `ClientCapabilities.CLIENT_MODULES`; `IPluginGamedataSeeds`;
  `PluginDataKind.Variable` and `Text`, and `PluginDataScope.Folder`; `GetValuesAsync`, `SeedAsync`
  and `RemoveUnchangedAsync` on the variable and text services.
- `Turbo.Gamedata`: `ClientModuleRegistry` (paths, hashes, addresses), `ClientModuleBroadcast`
  (sends the list to everyone online on a change), the `/gamedata/plugins/` route in
  `GamedataServer`, and `PluginGamedataSeeds` (the seeding, and an `IPluginDataOwner` for the
  uninstall).
- `Turbo.Plugins`: `PluginClientModules`, the registry bound to one plugin's folder, key and version,
  given through its `IHostServices` and disposed when it unloads; the seeding before a plugin starts.
- `Turbo.Revisions`: header 30009 in `Revision20260909`.

## 7. Tests

- `ClientModuleTests`: a plugin's module is listed with its hash and address and sent to everyone
  online, and taken away when the plugin unloads without disposing it; the public address; paths
  outside `wwwroot/`, hidden files, another plugin's key, a second module and core's registry are
  refused; the gamedata host serves the files to any site, immutable by hash, revalidated otherwise,
  404 for anything else, GET only; a client that does not ask is sent nothing; the wire layout.
- `PluginGamedataSeedTests`: loading adds what the hotel lacks once and keeps the hotel's own; the
  uninstall dry run lists, and deleting the data removes, only what is still as the plugin gave it.
- `HelloExamplePluginTests`: the example in `docs/examples/client-modules/hello` builds, loads, lists
  its module and gives the hotel its words.

## 8. Open

- **The panel** lists a plugin's data by kind; it shows variables and texts once it names the two new
  kinds (`variable`, `text`). Until then the uninstall still removes them and its summary counts them.
- **More than one module per plugin.** One entry file covers what a plugin needs today (it imports
  the rest); a plugin that wants two independent modules can be split, or the registry can take a
  module name beside the key when one needs it.
