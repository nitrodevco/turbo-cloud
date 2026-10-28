# Plugin Lifecycle

- [Introduction](#introduction)
- [A Plugin Is Its Sources](#a-plugin-is-its-sources)
    - [What a Plugin May Not Do](#what-a-plugin-may-not-do)
    - [When a Plugin Does Not Load](#when-a-plugin-does-not-load)
    - [From the Server's Internals to the Contracts](#from-the-servers-internals-to-the-contracts)
- [The Plugin Class](#the-plugin-class)
- [What a Plugin's Classes Can Take](#what-a-plugins-classes-can-take)
- [Load Order](#load-order)
- [A Plugin That Fails to Load](#a-plugin-that-fails-to-load)
- [What a Reload Touches](#what-a-reload-touches)
- [Stopping](#stopping)

## Introduction

A plugin is never built into the server. The server finds it in a folder, compiles its C# sources,
starts it, and from then on treats its commands, handlers, furni logic and the rest as its own.
When a file changes, the server compiles the plugin again and swaps the new version in while the
hotel keeps running; when the plugin is turned off, every trace of it goes. This page is that life,
step by step: what the server compiles and refuses, what a plugin's classes can take by
constructor, the order things start and stop in, and what a failure or a reload touches.

> [!NOTE]
> In a hurry? [Directory Structure](structure.md) shows a plugin's folder, and
> [Installation](installation.md#your-first-plugin) gets one loaded in three commands.

## A Plugin Is Its Sources

The server runs a plugin's code only by compiling it from the plugin's C# sources, as it loads the
plugin. There is no plugin assembly: a plugin folder holds its `manifest.json`, its sources and its
files (`wwwroot/`, `gamedata/`, `theme.json`), and an installed package, a GitHub link and a dev
folder are all the same sources.

```json
{ "Name": "Skills", "Key": "skills", "Version": "1.0.0", "Author": "you", "Sources": ["server/**/*.cs"] }
```

- **Sources**: globs of the C# files, relative to the plugin's folder (`*` and `?` within a name,
  `**` any folders). `server/**/*.cs` when the manifest names none, which is the template's layout.
  `bin/` and `obj/` are never sources. If no file matches but the folder has `.cs` files elsewhere
  or a `.csproj` outside those build folders, the plugin is **Rejected** with `no sources found`:
  the reason names the globs and the code they missed. Move it under `server/` or set `Sources`.
  A folder with no code is a data plugin (`docs/plugin-data.md`): it runs no code.
- **Compiled against** the framework (.NET and ASP.NET Core, with `Microsoft.Extensions.*`) and the
  server's plugin surface: `Turbo.Contracts`, `Turbo.Primitives`, `Turbo.Database` (a plugin's own
  tables, `docs/database.md`), EF Core with the MySQL provider, Orleans' serialization and
  abstractions, and SuperSocket's session abstractions. Nothing else: not the server's internals
  (`Turbo.Rooms`, grains, sessions), and no package of the plugin's own. The list is
  `PluginReferences` (`Turbo.Plugins/Compilation`); it is what a plugin may build on, not a fence
  (see below).
- **Compiled as** the server is: C# 14, nullable on, optimized, no unsafe code, with the source
  generators the server ships pinned with it: Orleans serializers (`[GenerateSerializer]`),
  `[LoggerMessage]`, System.Text.Json (`[JsonSerializable]`) and `[GeneratedRegex]`. A plugin brings
  no generator or analyzer of its own.
- **Symbols**: the assembly's portable PDB carries the sources, so an error thrown in a plugin's
  code names the plugin's file and line in the log.
- **Kept**: a compile is kept in `<plugins>/.compiled/<key>/`, keyed by a hash of everything it read
  (each source file's path and bytes, the references' module ids, the generators, the policy and
  this format). A restart, or a reload with nothing changed, loads it without compiling again. The
  kept files are signed with HMAC-SHA256 under a key only this server has (`.compiled/.key`, made
  once); files that are not the server's own are deleted and the plugin compiled again. Only the
  newest compile of each plugin is kept.

A plugin's own project (the template's `server/*.csproj`) imports what the server compiles with,
`plugin-sdk/Turbo.Plugin.props`, which the server ships beside its assemblies: the same references,
C# version, policy analyzer and Orleans generator. `npm run check` (`dotnet build` of that project)
fails as the server's compile would; the project is for the editor, the tests and `dotnet ef`, and
what it builds is never installed.

### What a Plugin May Not Do

Code that does one of these is **refused** (the plugin is `Rejected`, with the rule and its file
and line):

| Rule | What | Why |
|---|---|---|
| `TPP001` | load assemblies: `Assembly.Load*`, `LoadFrom`, `LoadFile`, `UnsafeLoadFrom`, `AppDomain.Load`, `Activator.CreateInstanceFrom`, any `AssemblyLoadContext` | the server runs a plugin's own sources and nothing else |
| `TPP002` | call native code: `[DllImport]`, `[LibraryImport]`, `extern` methods, `NativeLibrary` | native code is outside what the sources show |
| `TPP003` | unsafe code: `unsafe`, pointers, function pointers, `System.Runtime.CompilerServices.Unsafe` | it reads and writes memory the runtime does not check |
| `TPP004` | generate code at run time: `System.Reflection.Emit` | code that is not in the sources |
| `TPP005` | start processes: `Process.Start` | another program, outside the server |
| `TPP006` | use the compiler: `Microsoft.CodeAnalysis` | compiling code that is not in the sources |

A plugin folder that holds an executable file (an assembly or Windows executable, a Linux or macOS
binary, WebAssembly: `.dll`, `.exe`, `.so`, `.dylib`, `.wasm`, `.nupkg`, or any file whose first
bytes are one) is refused the same way, and the panel and GitHub installs refuse such a package
before installing it. Images, fonts, `.nitro` bundles, texts and JSON are a plugin's data and are
allowed. A refused plugin is not tried again until a file in its folder changes.

> [!WARNING]
> **Plugins are full trust.** A plugin runs in the server's process with the server's own access:
> its database, its files, the network, every player's session. The list above is a norm every
> plugin is held to and makes a plugin's sources the whole of what it runs, so reading them tells
> you what it does; it is not a sandbox (reflection reaches anything). Read a plugin, or know its
> author, before you install it, as you would any server code.

### When a Plugin Does Not Load

| The panel says | What to do |
|---|---|
| **Rejected**: `server/X.cs(12,9): TPP005 Plugins may not start processes ...` | take the call out; the rule's row above says why it is not allowed |
| **Rejected**: `server/bin/X.dll is a .dll file` | remove the file: the server compiles the sources itself (a package or a dev folder holds no build output) |
| **Rejected**: `The manifest's Sources glob "../x" leaves the plugin's folder` | name files inside the plugin's folder |
| **Failed**: `server/X.cs(42,9): CS0103 The name 'y' does not exist ...` | fix the code; `npm run check` shows the same errors locally |
| **Failed**: `CS0234 The type or namespace name 'Rooms' does not exist in the namespace 'Turbo'` | the plugin uses the server's internals: use the contracts (`Turbo.Contracts`, `Turbo.Primitives`) instead (below) |
| **Failed** with an exception | it compiled and threw as it started: the log has the stack trace with the plugin's file and line |

### From the Server's Internals to the Contracts

| A plugin that used | Uses |
| --- | --- |
| a core furni logic (`FurnitureFloorLogic`, `FurnitureWalkToUseLogic`) and its modules | `FurniLogic` and its `IFurniContext` (`docs/plugin-wired.md`, "Furni logic"): walks (`WalkToAsync`, `WalkToInOrderAsync`), `TileInFront`/`TilesBeside`, `Schedule` for a timer or tick, `UsagePolicy`, `Walkable`, `OnDetachedAsync` |
| the room's chat system (`SayAsAvatarAsync`, `WhisperToPlayerAsync`) | `IRoomTurn.SayAsync` with a `RoomSpeech`; in a command, `ICommandRoom.SayAsync` / `WhisperAsync` |
| the room's map and furni modules from a command | `ICommandRoom.FloorItemsAt(x, y)` |
| avatar, bot and temporary furni operations | `IRoomTurn`: `SetEffectAsync`, `SetBotFigureAsync`, `SetBotFollowingAsync`, `PlaceTemporaryFurniAsync`, `RemoveTemporaryFurniAsync` |
| a core wired box (`WiredActionShowMessage`) | a `WiredEffect` with that editor (`docs/plugin-wired.md`), `UsesTextPlaceholders`, `SayAsync` |
| the furni custody (`IFurnitureCustody.CreateInRoomAsync`) | `IRoomFurniWriter` (`Turbo.Database.Furniture`): a room's furni in your own unit of work |
| `WiredVariableIdBuilder.CreateFromBoxId` | `WiredVariableId.OfBox` |
| an embedded resource or the assembly's folder | `IPluginFiles.OpenRead(path)`: a file of the plugin's own folder |

## The Plugin Class

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
- **Hotel ready**: `HotelReadyAsync(ctx, ct)` runs once the hotel has started (at boot
  `StartingAsync` runs before it has: the host's `ApplicationStarted`), and at once after a reload
  or a later install. It is not awaited by the load; a failure is logged.
- **Stopping**: `ctx.Stopping` is cancelled as the plugin starts to stop, before its scheduled
  runs, hosted services and `StoppingAsync` are stopped; it is also the ready hook's `ct`, and the
  unload waits for that hook (up to 10 seconds). Work the plugin starts itself takes it, so it
  needs no `CancellationTokenSource` of its own.
- **Settings**: a class marked `[PluginSettings("Skills")]` is bound to that section
  (`IOptions<T>`, `IOptionsMonitor<T>`) and listed on the panel's Settings page; a class the plugin
  binds itself is left as it binds it.
- **Client module**: `client/src/index.tsx` (or `index.ts`) is built into its module and registered (`docs/client-modules.md`).
- **Tables**: a `PluginDbContextBase<T>` in its assembly is registered and migrated as it loads
  (`docs/database.md`, "A plugin's tables").
- **No class at all**: an assembly without one loads as a `TurboPlugin` with nothing of its own;
  its commands, messages, handlers, routes and the rest are found as always.

A plugin written against `ITurboPlugin` itself (`Key`, `Version`, `ConfigureServices`,
`StartAsync`, `StopAsync`, `BindExportsAsync`, `DisposeAsync`) loads too; the conventions
above apply to it as well, except where it does the same itself.

## What a Plugin's Classes Can Take

When a plugin loads, Turbo Cloud scans its assembly and builds every public, concrete, top-level
class it declares of these kinds: packet handlers and behaviours, chat commands, suggestion sources
and argument parsers, permission node sources, room object logic (furni logic and wired boxes),
wired variables, room event listeners, room games, game reward handlers and access policies.

Each is built by constructor from, in order (and so is every class the plugin registers itself by
type in `Services`/`ConfigureServices`, `AddSingleton<SkillBook>()` or `AddHostedService<T>()`: no
`Host<T>()` factory is needed for a host service):

1. the plugin's own container: what its `ConfigureServices` registered, its `PluginManifest`,
   `IPluginFiles` (its own folder's files, read only), `IPluginCatalog`, `IHostServices`, its
   prefixed `ILogger<T>` and the host's `IConfiguration`;
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

Two host services are handed out per plugin: `IExtensionPacketRegistry` (it notes the plugin's
headers for the admin panel) and `IClientModuleRegistry` (it serves the plugin's own folder). A
class the plugin registers itself gets the plugin's ones by constructor, as `IHostServices` gives
them; a class the scan builds reaches them through `IHostServices`. The plugin's `StartAsync` and
`BindExportsAsync` get its own container only; reach a host service there with
`services.GetRequiredService<IHostServices>().GetRequiredService<T>()`.

A class whose constructor still can't be resolved fails the plugin's load (below), with the
service named in the panel's error: "Unable to resolve service for type '...' while attempting to
activate '...'".

## Load Order

At start, every plugin found (the folders under `Turbo:Plugin:PluginFolderPath` and the
`Turbo:Plugin:DevPluginPaths`, a dev path winning over an installed folder of the same key) is
loaded in dependency order: a plugin after every plugin its manifest's `dependencies` name. A plugin turned off in the panel is not loaded. Loading one plugin
runs, in this order:

1. its checks: the plugin API version it was built against, and every dependency live;
2. its code: its folder checked, its sources compiled (or the kept compile loaded) into a context of
   its own, and its `ConfigureServices`;
3. its settings listed in the panel, its admin permission nodes, its client module files served,
   the gamedata it brings, its `BindExportsAsync`;
4. its migrations, its hosted services, its `StartAsync`; its admin pages go live;
5. what its assembly declares (the classes above): commands, permission nodes, packet handlers,
   room object logic, wired, room event listeners, games...

The plugin is **Loaded** once all five are done.

## A Plugin That Fails to Load

One plugin that fails does not stop the server or the other plugins. Whatever the step, the plugin
is marked **Failed** (or **Rejected**, above) with the reason, which the admin panel's plugin list
shows, and the server and the rest of the plugins carry on. A plugin that depends on it is marked Failed too ("dependency
inactive") and is loaded again with it once it loads.

A failed load leaves nothing of the plugin behind: what it registered in steps 3 to 5 is taken back,
the hosted services it started are stopped and its assembly context unloaded, whichever step failed,
so loading it again once fixed (an upload, a reload, a rescan) does not find its commands or permission nodes registered twice. A new version
that is refused or does not compile leaves the running version as it was: it is compiled before
the old one stops. One that fails after that (as it starts) does not keep the old one running: the
old version was stopped to make way for it. The panel's upload puts the kept earlier version back
when the new one fails.

## What a Reload Touches

A reload replaces the plugins it is about and leaves every other plugin running:

| Trigger | Plugins reloaded |
|---|---|
| A file of a plugin's code changes on disk (`manifest.json`, `*.cs`), when the server runs in the Development environment with `Turbo:Plugin:HotReloadEnabled`; or the panel's **Rescan** | the plugins new or changed since they loaded, and the ones not live (failed, waiting for a dependency); unchanged live ones keep running. Removed folders are unloaded |
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
