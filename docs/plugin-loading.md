# Loading plugins

How Turbo Cloud loads, reloads and isolates plugins, what a plugin's classes can take by
constructor, and what each step of a plugin's life guarantees.

## The plugin class

A plugin's class derives from `TurboPlugin` (`Turbo.Contracts.Plugins`) and writes only what it
adds: its key and version are its manifest's, and the host does the rest.

```csharp
public sealed class SkillsPlugin : TurboPlugin
{
    protected override void Services(IServiceCollection services) =>
        services.AddSingleton<SkillBook>();
}
```

- **Services** registers its own services. `StartingAsync(ctx, ct)` and `StoppingAsync(ctx, ct)`
  run as it starts and stops; what it registers by hand there it hands to `ctx.Track(...)`, and
  that is disposed when it stops. `ctx.Host<T>()` is a host service (`IHostServices`).
- **Settings**: a class marked `[PluginSettings("Skills")]` is bound to that section
  (`IOptions<T>`, `IOptionsMonitor<T>`) and listed on the panel's Settings page; a class the plugin
  binds itself is left as it binds it.
- **Client module**: `wwwroot/<key>.js` is registered as its module (`docs/client-modules.md`).
- **Tables**: a `PluginDbContextBase<T>` in its assembly is registered and migrated as it loads
  (`docs/database.md`, "A plugin's tables").
- **No class at all**: an assembly without one loads as a `TurboPlugin` with nothing of its own;
  its commands, messages, handlers, routes and the rest are found as always.

A plugin written against `ITurboPlugin` itself (`Key`, `Version`, `ConfigureServices`,
`StartAsync`, `StopAsync`, `BindExportsAsync`, `DisposeAsync`) loads as before; the conventions
above apply to it too, except where it does the same itself. `TurboPlugin` came with plugin API 2
(`PluginApi`).

## What a plugin's classes can take

When a plugin loads, Turbo Cloud scans its assembly and builds every public, concrete, top-level
class it declares of these kinds: packet handlers and behaviours, chat commands, suggestion sources
and argument parsers, permission node sources, room object logic (furni logic and wired boxes),
wired variables, room event listeners, room games, game reward handlers and access policies.

Each is built by constructor from, in order:

1. the plugin's own container: what its `ConfigureServices` registered, its `PluginManifest`,
   `IPluginCatalog`, `IHostServices`, its prefixed `ILogger<T>` and the host's `IConfiguration`;
2. the host's services, for any type the plugin did not register itself: `IGrainFactory`,
   `ISessionGateway`, `TimeProvider`, `IOptions<T>` of core's settings and the rest, as core's own
   commands and handlers take them.

So a command can take `IGrainFactory` as core's `:online` does, with nothing registered:

```csharp
[Command("hello")]
[RequiresPermission("hello.use")]
public sealed class HelloCommand(IGrainFactory grains, ILogger<HelloCommand> logger)
    : ICommand<NoArguments>
{
    // ...
}
```

A service the plugin registers itself wins over the host's of the same type. A type of the plugin's
own, or one built from it (`IOptions<ItsSettings>`, `IEnumerable<IItsThing>`), is never asked of
the host: the host's container keeps every type it is asked for, found or not, for as long as the
server runs, and with it the plugin's whole assembly. Register such a service in `ConfigureServices`
(its settings with `[PluginSettings]` or `services.AddOptions<T>()`); one not registered is not
there, so an optional constructor parameter of it gets its default.

Two host services are handed out per plugin and are only right through `IHostServices` (from `StartAsync` or a factory in
`ConfigureServices`): `IExtensionPacketRegistry` (it notes the plugin's headers for the admin panel)
and `IClientModuleRegistry` (it serves the plugin's own folder). The plugin's `StartAsync` and
`BindExportsAsync` get its own container only; reach a host service there with
`services.GetRequiredService<IHostServices>().GetRequiredService<T>()`.

A class whose constructor still can't be resolved fails the plugin's load (below), with the
service named in the panel's error: "Unable to resolve service for type '...' while attempting to
activate '...'".

## Load order

At start, every plugin found (the folders under `Turbo:Plugin:PluginFolderPath` and the
`Turbo:Plugin:DevPluginPaths`, a dev path winning over an installed folder of the same key) is
loaded in dependency order: a plugin after every plugin its manifest's `dependencies` name. A plugin turned off in the panel is not loaded. Loading one plugin
runs, in this order:

1. its checks: the plugin API version it was built against, and every dependency live;
2. its assembly, read into a context of its own, and its `ConfigureServices`;
3. its settings listed in the panel, its admin permission nodes, its client module files served,
   the gamedata it brings, its `BindExportsAsync`;
4. its migrations, its hosted services, its `StartAsync`; its admin pages go live;
5. what its assembly declares (the classes above): commands, permission nodes, packet handlers,
   room object logic, wired, room event listeners, games...

The plugin is **Loaded** once all five are done.

## A plugin that fails to load

One plugin that fails does not stop the server or the other plugins. Whatever the step, the plugin
is marked **Failed** with the reason, which the admin panel's plugin list shows, and the server and
the rest of the plugins carry on. A plugin that depends on it is marked Failed too ("dependency
inactive") and is loaded again with it once it loads.

A failed load leaves nothing of the plugin behind: what it registered in steps 3 to 5 is taken back,
the hosted services it started are stopped and its assembly context unloaded, whichever step failed,
so loading it again once fixed (an upload, a reload, a rescan) does not find its commands or permission nodes registered twice. A failed new version of a plugin
does not keep the old one running: the old version was stopped to make way for it. The panel's
upload puts the kept earlier version back when the new one fails.

## What a reload touches

A reload replaces the plugins it is about and leaves every other plugin running:

| Trigger | Plugins reloaded |
|---|---|
| A file of a plugin's code changes on disk (`manifest.json`, `*.dll`, `*.pdb`, `*.deps.json`), when the server runs in the Development environment with `Turbo:Plugin:HotReloadEnabled`; or the panel's **Rescan** | the plugins new or changed since they loaded, and the ones not live (failed, waiting for a dependency); unchanged live ones keep running. Removed folders are unloaded |
| The panel's **Reload**, an upload, turning a plugin on, the console's `reload-plugin <key>` | that plugin, whether it changed or not |
| The console's `reload-plugins`, `:reload plugins` | every plugin |

A plugin's other files (texts, client module files) are read by reloading it. Whenever a plugin is
replaced, the plugins that depend on it, directly or through another, are unloaded first
(dependents of dependents before them) and loaded again after it, bound to its new exports. A
dependent that fails to load again is marked Failed; the plugin asked for keeps its result.

## Stopping

A plugin stops in the reverse of its start: its hosted services, its `StopAsync`, then everything
it registered is taken back and its assembly context unloaded. When the server stops, every plugin
is stopped after the plugins that depend on it, so each stops while what it depends on still runs.
