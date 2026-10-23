# Loading plugins

How Turbo Cloud loads, reloads and isolates plugins, and what a plugin's classes can take by
constructor.

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

A service the plugin registers itself wins over the host's of the same type. Two host services are
handed out per plugin and are only right through `IHostServices` (from `StartAsync` or a factory in
`ConfigureServices`): `IExtensionPacketRegistry` (it notes the plugin's headers for the admin panel)
and `IClientModuleRegistry` (it serves the plugin's own folder). The plugin's `StartAsync` and
`BindExportsAsync` get its own container only; reach a host service there with
`services.GetRequiredService<IHostServices>().GetRequiredService<T>()`.

A class whose constructor still can't be resolved fails the plugin's load (below), with the
service named in the panel's error: "Unable to resolve service for type '...' while attempting to
activate '...'".

## A plugin that fails to load

One plugin that fails does not stop the server or the other plugins. Whatever the step (its API
version, a dependency that is not live, its migrations, its `StartAsync`, a class it declares that
can't be built), the plugin is marked **Failed** with the reason, which the admin panel's plugin list
shows, and the server and the rest of the plugins carry on. A failure while its classes are being
registered stops the plugin again and takes back everything that was registered, so loading it once
fixed (an upload, a reload) does not find its commands or permission nodes registered twice.
