# Turbo Cloud

A Habbo Hotel emulator built with C# and [Orleans](https://learn.microsoft.com/en-us/dotnet/orleans/).

## Plugin Development

Turbo Cloud uses a plugin system where game features are implemented as loadable plugins. During development, plugins are hot-reloaded in-process when you rebuild them - no server restart needed.

### Prerequisites

- .NET 9 SDK
- A plugin project that references `Turbo.Contracts`

### Plugin Project Setup

Your plugin's `.csproj` must copy `manifest.json` to the build output:

```xml
<ItemGroup>
  <Content Include="manifest.json">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </Content>
</ItemGroup>
```

### Emulator Configuration

In `appsettings.Development.json`, point `DevPluginPaths` at your plugin's build output directory:

```json
{
  "Turbo": {
    "Plugin": {
      "DevPluginPaths": [
        "C:/path/to/your-plugin/bin/Debug/net9.0"
      ]
    }
  }
}
```

You can list multiple plugin paths if you're developing several plugins at once.

### Dev Workflow

Open two terminals:

**Terminal 1** - Run the emulator:
```
dotnet run --project Turbo.Main
```

**Terminal 2** - Watch your plugin for changes:
```
cd C:/path/to/your-plugin
dotnet watch build
```

Now when you edit a `.cs` file and save, `dotnet watch` rebuilds the plugin automatically. The emulator detects the new DLL and hot-reloads the plugin in-process. Message handlers and services are swapped live - connected clients stay connected.

### How It Works

1. You edit a `.cs` file in your plugin project
2. `dotnet watch build` detects the change and rebuilds
3. The new DLL lands in your plugin's `bin/Debug/net9.0/`
4. The emulator's file watcher detects the DLL change
5. The plugin is unloaded and reloaded with the new assembly
6. Message handlers and services are swapped atomically

### Released Plugins

For production/released plugins, place them in the `plugins/` directory inside the emulator's output folder. Each plugin should be in its own subdirectory with a `manifest.json`:

```
plugins/
  MyPlugin/
    manifest.json
    MyPlugin.dll
```

During development, `DevPluginPaths` takes precedence - if the same plugin key exists in both `plugins/` and a dev path, the dev version is loaded.

### Limitations

- **Grain types cannot be hot-reloaded.** Orleans requires grain types to be registered at silo startup. If your plugin adds new grain types, a server restart is needed.
- **Memory may grow over many reloads.** Assembly unloading relies on .NET's `AssemblyLoadContext` which may not fully release memory if type references are retained. Restart the emulator periodically during long dev sessions if memory grows.
