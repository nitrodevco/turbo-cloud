# Upgrading a Plugin

- [Introduction](#introduction)
- [Theme audience providers (2026-10-28)](#theme-audience-providers-2026-10-28)
- [To source-only plugins (2026-10-27)](#to-source-only-plugins-2026-10-27)
    - [1. Build a server that ships the plugin SDK](#1-build-a-server-that-ships-the-plugin-sdk)
    - [2. Move to the template's layout](#2-move-to-the-templates-layout)
    - [3. Manifest](#3-manifest)
    - [4. Server project](#4-server-project)
    - [5. Whatever the project file added to the assembly](#5-whatever-the-project-file-added-to-the-assembly)
    - [6. Client module](#6-client-module)
    - [7. From the server's internals to the contracts](#7-from-the-servers-internals-to-the-contracts)
    - [8. What else changed shape the same day](#8-what-else-changed-shape-the-same-day)
    - [9. The plugin rules](#9-the-plugin-rules)
    - [10. Tests](#10-tests)
    - [11. Check it as the server will](#11-check-it-as-the-server-will)
    - [12. Release](#12-release)

## Introduction

What a plugin author does when Turbo changes what plugins are. Each change, with who it affects and
whether it breaks a plugin, is in `docs/plugin-changelog.md` (one file per change in
`docs/changelog/`); this page is the work, in order. A pull request with a breaking change adds its
changelog entry and its steps here together.

## Theme audience providers (2026-10-28)

Host integrations implementing `IThemeAudiences` now implement its inherited `IThemeResolver`.
Implement `ResolveAsync` for the supplied batch of players and accepted preview requests,
returning the default theme and an entry for every player. Recheck permissions and saved choices
without reading or changing displayed revisions. There is no default implementation that skips
live updates.

Inject the host's singleton `IThemeClientState` into the audience provider. In `SessionAsync`,
record the accepted preview request (null if rejected) and the revision supplied by
`IThemeFileService.ThemeLookAsync` using `NoteDrawn`. Register the audience replacement after
the content module; the default `IThemeResolver` alias follows `IThemeAudiences`. Keep one shared
tracking service for session initialization and live reload. Existing plugins using the audience
API without implementing it need no changes.

## To source-only plugins (2026-10-27)

Turbo no longer loads a plugin's assembly. The server compiles the plugin from its C# sources as it
loads it, against the plugin surface only (`Turbo.Contracts`, `Turbo.Primitives`, `Turbo.Database`
and the packages listed in `docs/plugin-loading.md`), refuses code that breaks the plugin rules
(`TPP001` to `TPP006`), and builds the client module from `client/src/` itself. A plugin folder holds
sources and files, never a `.dll`.

A plugin made before this ships an assembly, so it no longer loads: the folder is **Rejected**
(`X.dll is a .dll file`), or it compiles and fails where it used the server's internals. Go through
the steps below from top to bottom. Two plugins went through them and are worth reading beside this
page:

- [turbo-events-plugin#1](https://github.com/Diddyy/turbo-events-plugin/pull/1): layout, project
  file and release only; the plugin used no internals.
- [turbo-roomcopy-plugin#1](https://github.com/Diddyy/turbo-roomcopy-plugin/pull/1): the same, plus
  two internals moved to contracts (the furni custody to `IRoomFurniWriter`, the box variable ids to
  `WiredVariableId.OfBox`).

The template (`templates/turbo-plugin`) is already in the new shape: when in doubt, copy what it does.

### 1. Build a server that ships the plugin SDK

Build `Turbo.Main` from turbo-cloud main (`dotnet build Turbo.Main/Turbo.Main.csproj`). Its bin
folder now holds `plugin-sdk/Turbo.Plugin.props`: the references, C# version, policy analyzer and
Orleans generator the server compiles plugins with. Your project imports it from there, so your
build fails where the server's compile would. `TurboBin` names that bin folder.

### 2. Move to the template's layout

```
manifest.json                 at the plugin's root
server/                       the C# sources (and the server project, server/<Name>.csproj)
client/src/index.ts(x)        the client module's entry, if the plugin has one
wwwroot/                      files clients load (icons, images, data): no JavaScript
gamedata/                     texts and variables, as before
tests/<Name>.Tests/           the tests
```

The server compiles `server/**/*.cs` unless the manifest says otherwise. **Check this first:** when
`Sources` matches no `.cs` files but the folder contains C# sources or a `.csproj` outside `bin/`
and `obj/`, the server rejects the plugin with `no sources found`. The reason names the globs and
where it found code. Move the sources under `server/`, or name them in `Sources` (step 3). A genuine
data plugin with no code still loads (`docs/plugin-data.md`). Never let `Sources` reach your tests:
`["**/*.cs"]` compiles `tests/` too, which does not build without xunit.

### 3. Manifest

- Remove `AssemblyFile` and `BuiltAgainst`. Neither is read any more.
- Add `"Sources": ["src/**/*.cs"]` only if your sources are not under `server/`. Globs are relative to
  the plugin's folder; `bin/` and `obj/` are never sources, and a glob that leaves the folder is
  refused.
- Keep `"ApiVersion": 1`. The plugin API stays 1.

```json
{ "Name": "Turbo Events", "Key": "events", "Version": "1.0.0", "Author": "Diddyy", "TablePrefix": "events_", "ApiVersion": 1 }
```

### 4. Server project

Replace the hand-written references with the SDK import. Before (turbo-events):

```xml
<EnableDynamicLoading>true</EnableDynamicLoading>
<CopyLocalLockFileAssemblies Condition="'$(DesignTime)' != 'true'">false</CopyLocalLockFileAssemblies>
...
<PackageReference Include="Microsoft.EntityFrameworkCore" />
<PackageReference Include="Microsoft.Orleans.Core.Abstractions" />
<Reference Include="Turbo.Contracts" HintPath="$(TurboCloudPath)\Turbo.Contracts\bin\..." Private="$(TurboPrivate)" />
<None Include="manifest.json" CopyToOutputDirectory="PreserveNewest" />
```

After:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <Import Project="$(TurboBin)/plugin-sdk/Turbo.Plugin.props" />
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <RootNamespace>TurboEvents</RootNamespace>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <NoWarn>$(NoWarn);CS1591</NoWarn>
  </PropertyGroup>
  <ItemGroup Condition="'$(DesignTime)' == 'true'">
    <!-- dotnet ef, at the server's EF Core version. -->
    <PackageReference Include="Microsoft.EntityFrameworkCore.Design" PrivateAssets="all" />
  </ItemGroup>
</Project>
```

- Set `TurboBin` in `Directory.Build.props` (default: a turbo-cloud checkout's built `Turbo.Main`) or
  pass `-p:TurboBin=...`. turbo-events' `Directory.Build.props` is a working one.
- A plugin brings **no package of its own**. Every package reference goes; what the plugin may use is
  the framework (.NET, ASP.NET Core, `Microsoft.Extensions.*`) and the list the props reference. Code
  from a package that is not on the list has to become the plugin's own source (if its license
  allows), or use what the framework has (`System.Text.Json`, `HttpClient`, ...).
- `Directory.Packages.props` pins only your tests' packages.

### 5. Whatever the project file added to the assembly

The server reads the `.cs` files the manifest names and nothing else: not the `.csproj`, not
`Directory.Build.props`. Anything those added to the assembly is missing from the server's compile.

- `InternalsVisibleTo` in the project file: move it into a source file, so the assembly the server
  compiles is the one your tests saw (turbo-events' `server/Properties.cs`):

  ```csharp
  using System.Runtime.CompilerServices;

  [assembly: InternalsVisibleTo("TurboEvents.Tests")]
  ```

- `EmbeddedResource` (a JSON file, images, a word list): there is no assembly file to embed in. Ship
  the file in the plugin's folder and read it with `IPluginFiles` (take it by constructor;
  `OpenRead("data/rooms.json")`, null when the file is missing), or turn small data into a constant
  in code.
- The assembly's folder (`typeof(X).Assembly.Location`, `AppContext.BaseDirectory`): the plugin's
  assembly is compiled in memory and has no file. Read the plugin's own files through `IPluginFiles`.
- `Compile Include` of sources outside `server/`: name them in the manifest's `Sources` instead.

### 6. Client module

The server builds the module from `client/src/index.tsx` (or `index.ts`) and registers it; it serves
no JavaScript from `wwwroot/` (`docs/client-modules.md`, "How the server builds a module").

- A module shipped as built JavaScript (`wwwroot/<key>.js`): move its sources to `client/src/`, entry
  `index.ts` or `index.tsx`, and delete the built file and the build step (esbuild in your scripts).
  A plain `.js` module becomes `index.ts` as it is: rename it and fix what TypeScript reports.
- A module may import its own `.ts`, `.tsx` and `.json` files under `client/src/`,
  `@nitrodevco/client-module-sdk` (and `.../theme`), and the client's `react`, `react/jsx-runtime`
  and `zustand`. Any other package, a `.js` file, a file outside `client/src/`, a `data:` or `https:`
  import fails the build, with its `file:line:column`. A module that loaded a peer `.js` file by URL
  imports it statically instead.
- A plugin that called `IClientModuleRegistry.Register(Key, "<key>.js")`: drop the call when the
  entry is `client/src/index.ts(x)` (the server registers that one itself), or pass a `.ts`/`.tsx`
  entry under `client/src/` (`Register(Key, "client/src/other.ts")`). A `.js` entry throws.

### 7. From the server's internals to the contracts

A plugin that compiled against `Turbo.Rooms`, `Turbo.Furniture`, `Turbo.Events` and the rest stops
at `CS0234 The type or namespace name 'Rooms' does not exist in the namespace 'Turbo'` (or `CS0246`
for a type). Each internal a plugin used has a contract now:

| A plugin that used | Uses |
| --- | --- |
| references to `Turbo.Events`, `Turbo.Messages`, `Turbo.Pipeline`, `Turbo.Runtime` | nothing more: `IEventHandler<T>`, `IMessageHandler<T>`, `IMessageBehavior<T>`, `MessageContext` and `IEventPublisher` are in `Turbo.Primitives` under the same names; drop the references |
| a core furni logic (`FurnitureFloorLogic`, `FurnitureWalkToUseLogic`) and its modules (`_ctx`, avatar, map) | `FurniLogic` with `[FurniLogic("key")]` and its `IFurniContext` (`docs/plugin-wired.md`, "Furni logic"): `WalkToAsync`, `WalkToInOrderAsync`, `TileInFront`, `TilesBeside`, `StandsBeside`, `Schedule`/`CancelSchedule` for a timer, `UsagePolicy`, `Walkable`, `OnDetachedAsync` |
| the room's users and map (`RoomGrain`, avatar and map modules) | `IRoomTurn` (`IFurniContext` is one): `RoomUsers` (each a `RoomUser` with `Rotation`, `IsWalking`, `IsIdle`, `EffectId`), `Map` (`IsFloor`, `IsFree`, `CanStand`, `FurniAt`), `StopWalkingAsync`, `SetRotationAsync` |
| the room's chat system (`ChatSystem`, `AvatarSpeech`, `SayAsAvatarAsync`, `WhisperToPlayerAsync`) | `IRoomTurn.SayAsync(user, text, RoomSpeech.Talk)` (or `RoomSpeech.Whisper`, `new RoomSpeech { Shout = true, StyleId = ... }`); in a command, `ICommandRoom.SayAsync` / `WhisperAsync` |
| avatar effects, bot figure or following, temporary furni | `IRoomTurn.SetEffectAsync`, `SetBotFigureAsync`, `SetBotFollowingAsync`, `PlaceTemporaryFurniAsync`, `RemoveTemporaryFurniAsync` |
| the room's furni at a tile from a command | `ICommandRoom.FloorItemsAt(x, y)` |
| a core wired box (`WiredActionShowMessage`) as a base class | a `WiredEffect` of your own with that editor (`docs/plugin-wired.md`), `UsesTextPlaceholders => true`, and `IWiredEffectContext.SayAsync` |
| the furni custody (`IFurnitureCustody.CreateInRoomAsync`, `FurniToPlace`) | `IRoomFurniWriter.CreateInRoomAsync` (`Turbo.Database.Furniture`) with `NewRoomFurni`, in your own unit of work; the room must not be loaded |
| `WiredVariableIdBuilder.CreateFromBoxId(id)` | `WiredVariableId.OfBox(id)` (`Turbo.Primitives.Rooms.Wired.Variable`): the same ids |
| an embedded resource or the assembly's folder | `IPluginFiles.OpenRead(path)` (step 5) |

Contracts are added when plugins need them. When yours is missing, open an issue naming what you
used and why: do not reach the internals by reflection.

### 8. What else changed shape the same day

Code that compiled against the contracts last week may still need these (each is in the changelog):

| Error | Change |
| --- | --- |
| `IWiredEffectContext` has no `ShowMessageAsync` | `SayAsync(user, text, RoomSpeech, ct)`, answering whether it was shown; `onlyForThem: true` is `RoomSpeech.Whisper` |
| `RoomUser` / `RoomFurni` constructor takes more arguments (in tests) | `RoomUser` gained `Rotation`, `IsWalking`, `IsIdle`, `EffectId`; `RoomFurni` gained `Rotation`, `Definition` |
| `PurchaseRequest` / `PurchaseCompletedEvent` needs `Kind` (in tests) | pass the `PurchaseKind` (`Catalog`, `ClubMembership`, `Gift`, `RoomAd`, `ClubGiftClaim`, `LimitedEdition`) |
| `IsArrivingByTeleportAsync` does not exist | `PassesRoomDoorAsync` (teleporters and a plugin's linked pairs) |

### 9. The plugin rules

Code that does one of these is **Rejected** with the rule, file and line (`docs/plugin-loading.md`,
"What a plugin may not do"). There is no setting to allow them; take the code out.

| Rule | You did | Do instead |
| --- | --- | --- |
| `TPP001` | `Assembly.Load*`, `AssemblyLoadContext`, `Activator.CreateInstanceFrom` | ship the code as sources; reflection over the plugin's own types (`typeof(X).Assembly.GetTypes()`) is fine |
| `TPP002` | `[DllImport]`, `[LibraryImport]`, `extern`, `NativeLibrary` | the framework's managed API, or a service outside the server that the plugin calls over HTTP |
| `TPP003` | `unsafe`, pointers, `Unsafe` | `Span<T>`, `MemoryMarshal`, `BinaryPrimitives` |
| `TPP004` | `System.Reflection.Emit` | ordinary code, a source generator the server ships (`[JsonSerializable]`, `[GeneratedRegex]`, `[LoggerMessage]`, `[GenerateSerializer]`) |
| `TPP005` | `Process.Start` | HTTP to a service that runs it, or a scheduled task in the plugin (`docs/plugin-scheduler.md`) |
| `TPP006` | `Microsoft.CodeAnalysis` | not available to plugins |

A folder holding an executable file (`.dll`, `.exe`, `.so`, `.dylib`, `.wasm`, `.nupkg`, or any file
that starts like one) is Rejected the same way. Delete it from the plugin's folder. Images, fonts,
`.nitro` bundles, texts and JSON are fine.

### 10. Tests

The tests run the plugin's project build beside the server's assemblies, not inside the server, so
they may reference what they need from `TurboBin` (`Turbo.Testing.dll` for `FakeHotel`, Orleans'
abstractions). Move them to `tests/<Name>.Tests/` with a `ProjectReference` to
`../../server/<Name>.csproj`; the template's `tests/Skills.Tests/Skills.Tests.csproj` is the shape.

### 11. Check it as the server will

- `npm run check` in a template plugin, or `dotnet build server/<Name>.csproj`: the same references,
  C# 14, nullable, no unsafe code and the policy analyzer, so it fails exactly where the server's
  compile would, as `server/File.cs(line,column)`.
- Then load it: name the plugin's folder (a template plugin's `dist/<key>/`, which `npm run dev`
  keeps current) in the server's `Turbo:Plugin:DevPluginPaths` and look at the admin panel's plugin
  list.

| The panel says | Cause |
| --- | --- |
| Rejected: `no sources found` | `Sources` missed the code in the folder: step 2 |
| Rejected: `server/bin/X.dll is a .dll file` | build output in the plugin's folder: step 9 |
| Rejected: `server/X.cs(12,9): TPP005 ...` | a plugin rule: step 9 |
| Failed: `CS0234 ... 'Rooms' does not exist in the namespace 'Turbo'` | an internal: step 7 |
| Failed: `The client module of <key> did not build: ... is not built into a client module` | a package or `.js` import: step 6 |
| Failed: `A circular dependency was detected for ...` | a replacement takes its own contract by constructor: reach the one it replaces through `ctx.Host<T>()` or `IServiceSlot<T>.Under(this)` (`docs/plugin-services.md`) |

### 12. Release

A server linked to your repository (admin panel, Plugins, Link from GitHub) installs a release's
**sources**: GitHub's archive of the tag, never a file attached to the release.

- Tag `v<Version>` where `Version` is the manifest's at that commit. The server installs whatever
  the tag holds and shows the manifest's version, so check it in the workflow:

  ```yaml
  - name: The tag is the manifest's version
    run: |
      version=$(jq -r .Version plugin/manifest.json)
      if [ "v$version" != "$TAG" ]; then
        echo "manifest.json says Version $version: tag v$version, or change the manifest first." >&2
        exit 1
      fi
  ```

- Build turbo-cloud's `Turbo.Main` (for `plugin-sdk/`), test, check the package, and make the
  release with no asset (`gh release create "$TAG" --verify-tag --generate-notes`). Drop the zip
  step, the `BuiltAgainst` stamp and the pinned turbo-cloud ref. turbo-events' `release.yml` and
  `ci.yml` are working ones.
- The server installs the folder holding the plugin's `manifest.json`, every file in it (tests and
  docs included), so nothing under it may be a binary. A repository with several plugins names the
  plugin's folder in the link's **Folder** field.
- For the panel's upload, `npm run package` (or the plugin's `scripts/package.sh`) makes the zip of
  the sources.

Done: the plugin loads from its sources, and a restart or a reload with nothing changed loads the
kept compile without compiling again (`docs/plugin-loading.md`, "Kept").
