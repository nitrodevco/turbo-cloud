# Directory Structure

- [Introduction](#introduction)
- [A Plugin's Folder](#a-plugins-folder)
    - [The Manifest](#the-manifest)
    - [The Server Half](#the-server-half)
    - [The Client Module](#the-client-module)
    - [Files Clients Load](#files-clients-load)
    - [Texts](#texts)
    - [Tests](#tests)
- [Adding Pieces With make](#adding-pieces-with-make)
- [What a Plugin Never Holds](#what-a-plugin-never-holds)
- [The Server's Repository](#the-servers-repository)

## Introduction

A plugin is one folder that holds both halves of a feature: the C# the server compiles and the
TypeScript the client runs, with the files, texts and tests that go with them. The template
(`templates/turbo-plugin`) lays the folder out the way the server and the tools expect, so you
rarely have to think about where something goes. Make one with:

```shell
dotnet new turbo-plugin -n Skills --key skills --turbo <path to turbo-cloud> --nitro <path to nitro-next>
```

This page walks through what you get. The sample in it is a skill book; `npm run clean-sample`
takes the sample out and leaves an empty plugin to build up.

## A Plugin's Folder

```
Skills/
├── manifest.json        name, key, version, the plugin API it was built for
├── server/              the server half: C# the hotel compiles
│   ├── SkillsPlugin.cs      the plugin's services
│   ├── SkillsSettings.cs    [PluginSettings("Skills")]
│   ├── SkillsPermissions.cs its permission nodes
│   ├── SkillsCommand.cs     :skills
│   ├── SkillsMessages.cs    named messages and their handlers
│   ├── SkillsWeeklyReset.cs a scheduled task
│   ├── SkillBook.cs         the plugin's own logic
│   └── Skills.csproj        for your editor, the tests and dotnet ef; never installed
├── client/
│   ├── src/                 the client module (TypeScript + React)
│   │   ├── index.tsx            defineModule: the entry point
│   │   └── made/                what `npm run make` adds
│   └── tests/               client tests (node --test)
├── wwwroot/             files clients load beside the module, and template-patches.json
├── gamedata/            the plugin's texts (external_texts.txt)
├── tests/Skills.Tests/  server tests (xUnit, against FakeHotel)
├── tools/               the template's dev, make and package scripts
└── package.json         npm run dev | build | check | test | make | package
```

Everything in `server/`, `client/src/`, `wwwroot/` and `gamedata/` is **found**, not registered:
a command class is a command because it is a public class implementing `ICommand<T>`, a setting is
a setting because its class says `[PluginSettings]`. Each piece goes away again when the plugin
unloads.

### The Manifest

`manifest.json` names the plugin:

```json
{
    "Name": "Skills",
    "Key": "skills",
    "Version": "0.1.0",
    "Author": "",
    "Dependencies": [],
    "ApiVersion": 1
}
```

The **key** is the plugin's identity everywhere: its folder, its permission nodes
(`skills.command.skills`), its texts (`skills.window.title`), its messages (`skills.state`), its
web routes (`/api/plugins/skills/...`) and its client module. `Dependencies` lists the keys of
plugins it needs loaded first. `Sources` (optional) lists which C# files to compile; it defaults to
`server/**/*.cs`. [Plugin Lifecycle](plugin-loading.md) has every field.

### The Server Half

`server/` is C# the hotel compiles itself as it loads the plugin: a plugin ships its sources,
never an assembly. The plugin class is the only one you write by convention, and only when you
have services to register:

```csharp
public sealed class SkillsPlugin : TurboPlugin
{
    protected override void Services(IServiceCollection services) =>
        services.AddSingleton<SkillBook>();
}
```

Everything else is a class of the kind it is: [commands](commands.md),
[messages](plugin-messages.md), [furni logic](furni-logic.md), [wired boxes](plugin-wired.md),
[policies](plugin-policies.md), [scheduled tasks](plugin-scheduler.md),
[web routes](plugin-web-routes.md). The hotel itself is `IHotel`, taken by constructor
([Contracts: The Hotel API](hotel-api.md)).

`Skills.csproj` compiles against the same references and rules as the hotel does (it imports
`plugin-sdk/Turbo.Plugin.props` from the server's bin folder, `TurboBin`), so `npm run check` shows
the errors the hotel would, but what it builds is never installed.

### The Client Module

`client/src/index.tsx` is the module's entry point, written against
`@nitrodevco/client-module-sdk`. The hotel builds it into one file as the plugin loads and tells
every client to run it:

```tsx
export default defineModule({
    activate(api) {
        api.windows.register('skills', SkillsWindow);
        api.toolbar.add({
            id: 'skills',
            icon: assetUrl('skills.png'),
            tooltip: api.texts.get('skills.toolbar.tooltip', 'Skills'),
            onClick: () => api.windows.toggle('skills'),
        });
    },
});
```

A module imports only its own files, the SDK, `react` and `zustand`. See
[Client Modules](client-modules.md).

### Files Clients Load

`wwwroot/` is served beside the module at `/gamedata/plugins/<key>/` (the toolbar icon above is
`wwwroot/skills.png`). `wwwroot/template-patches.json` changes core windows as data, with no code:
the template's adds a Skills button to the purse. See [Template Patches](https://github.com/nitrodevco/nitro-next/blob/pixi-render/docs/template-patches.md).

### Texts

`gamedata/external_texts.txt` holds the plugin's words, given to the hotel as the plugin loads.
Staff can change them in the admin panel like any of the hotel's texts.

```
skills.window.title=Skills
skills.window.level=%name%: level %level%
```

### Tests

`tests/Skills.Tests/` is an xUnit project that tests the server half against `FakeHotel`, the
hotel API in memory; `client/tests/` tests the module with `node --test`. `npm test` runs both. See
[Testing](testing.md).

## Adding Pieces With make

`npm run make <kind> <name>` writes one piece where the server or the client will find it, prints
the files it wrote, and names the guide to read:

| Command | Writes |
| --- | --- |
| `npm run make command hug` | `server/Commands/HugCommand.cs` and its permission node |
| `npm run make message levelled --to client --fields level:int,name:string` | the message record and its client half |
| `npm run make logic skill_stove` | `server/Logic/SkillStoveLogic.cs`, a `FurniLogic` |
| `npm run make wired effect whisper_room` | `server/Wired/WhisperRoomEffect.cs` (`trigger`, `condition`, `selector` too) |
| `npm run make listener entered` | `server/Listeners/EnteredListener.cs`: hears every room's events |
| `npm run make setting rate --type int --default 5` | a property in the `[PluginSettings]` class |
| `npm run make route rares/values --anonymous` | `server/Routes/RaresValuesRoute.cs`: `GET /api/plugins/skills/rares/values` |
| `npm run make window news` | `client/src/made/NewsWindow.tsx` |
| `npm run make patch habbo-friend-list-com/friend_list_xml` | an entry in `wwwroot/template-patches.json` |
| `npm run make migration AddPrizes` | the plugin's database context (once) and a migration |
| `npm run make schedule weekly-rotation --cron "0 18 * * MON"` | `server/Tasks/WeeklyRotation.cs` |
| `npm run make policy purchase club-discount` | `server/Policies/ClubDiscountPolicy.cs` |

## What a Plugin Never Holds

- **No assembly.** A `.dll`, `.exe`, `.so`, `.dylib`, `.wasm` or `.nupkg` (or any file whose first
  bytes say it is one) makes the hotel refuse the plugin. The hotel compiles the sources.
- **No built JavaScript.** The hotel builds the client module from `client/src/`.
- **No package of its own.** A plugin compiles against the framework and Turbo's contracts only
  (`Turbo.Contracts`, `Turbo.Primitives`, `Turbo.Database`); [Plugin Lifecycle](plugin-loading.md)
  lists exactly what it can reference.

`npm run package` makes `dist/skills-<version>.zip`, the package the admin panel's upload takes:
the sources and files above, nothing built.

## The Server's Repository

For core work, the `turbo-cloud` repository is one .NET solution, `Turbo.Cloud.sln`:

| Folder | What it is |
| --- | --- |
| `Turbo.Main/` | The host: startup, configuration, the console, the hotel API's implementation |
| `Turbo.Networking/` | Sockets, framing and sessions |
| `Turbo.Revisions/` | Each client revision's headers, parsers and serializers (`Revision20260909/`) |
| `Turbo.PacketHandlers/` | One handler per client packet, orchestration only |
| `Turbo.Rooms/`, `Turbo.Players/`, `Turbo.Catalog/`, `Turbo.Inventory/`, ... | The domain modules and their grains |
| `Turbo.Primitives/` | Shared contracts: messages, composers, grain interfaces, events, policies |
| `Turbo.Contracts/` | The plugin-facing API: `IHotel`, `TurboPlugin`, `FurniLogic`, the room turn |
| `Turbo.Plugins/` | Finding, compiling, loading and reloading plugins |
| `Turbo.Database/` | The EF Core context, entities and migrations |
| `Turbo.Testing/` | `FakeHotel` and the other testers plugins use |
| `Turbo.Tests/` | The server's tests |
| `templates/turbo-plugin/` | The plugin template |
| `docs/` | This documentation; `docs-site/` builds the website from it |

[`CONTEXT.md`](../CONTEXT.md) says which project may depend on which, and where new code belongs.
