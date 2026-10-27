# Configuration

- [Introduction](#introduction)
- [Where a Value Comes From](#where-a-value-comes-from)
    - [Environment Variables](#environment-variables)
    - [The Settings Page](#the-settings-page)
- [The Environment](#the-environment)
- [Reading Settings in Code](#reading-settings-in-code)
    - [Core Settings](#core-settings)
    - [Plugin Settings](#plugin-settings)
    - [Settings That Apply at Once](#settings-that-apply-at-once)
- [The Sections](#the-sections)

## Introduction

Everything Turbo can be told lives in its configuration: the database to use, the ports to listen
on, how many rooms a player may own, which web hosts are on. It is standard .NET configuration
under one root, `Turbo`, so a setting has a path such as `Turbo:Rooms:MaxUsers`, and it can come
from a file, the environment or the admin panel.

`appsettings.json` in the repository lists every section with its defaults. Don't edit it for
your own hotel: put what you change in `appsettings.Development.json` (created for you by
[bootstrap](installation.md#bootstrapping), and ignored by git), in the environment, or on the
admin panel's **Settings** page.

## Where a Value Comes From

Lowest first; each source overrides the ones before it:

1. **The default** the settings class ships with (`public int MaxUsers { get; init; } = 25;`).
2. **`appsettings.json`**, then `appsettings.{Environment}.json`.
3. **The admin panel** (the `server_settings` table).
4. **The environment and the command line.**

The environment always has the last word. A setting it sets shows on the panel as locked by that
variable, and the panel refuses to change it, so an environment variable can always rescue a
hotel from a bad panel value.

### Environment Variables

A variable's name is the setting's path with `__` (two underscores) for each `:`:

```shell
Turbo__Database__ConnectionString="server=127.0.0.1;user=turbo;password=...;database=turbo"
Turbo__Admin__Enabled=true
```

The server also reads variables with a `TURBO__` prefix in front of the full path
(`TURBO__Turbo__Rooms__MaxPlayersLimit=75`), which keeps a hotel's settings apart from everything
else in a shared environment. This is how a deployment sets its secrets and addresses without a
file on disk. The Ploi scripts map shorter names (`TURBO_DB_MIGRATE`, `TURBO_OWNER_NAME`) onto
these; see [Deployment](deployment.md).

### The Settings Page

Staff holding `settings.manage` can change any setting on the admin panel's **Settings** page,
which lists every option of every section with its value, where that value came from, and whether
a restart is waiting on it. Most settings apply after a restart; the page offers one. Secrets can be
replaced but are never shown. [Settings](settings.md) covers the page, its API and the rules for
each kind of setting.

## The Environment

`DOTNET_ENVIRONMENT` names the environment: `Development` on your machine, nothing (Production)
on a live hotel. Development:

- reads `appsettings.Development.json`;
- logs each configuration source as the server starts;
- reloads a plugin when its files change (`Turbo:Plugin:HotReloadEnabled`);
- makes the first player on an empty hotel its owner, unless an owner is named
  (`Turbo:Owner:FirstPlayerInDevelopment`).

## Reading Settings in Code

### Core Settings

A module's settings are a class that names its section in a `SECTION_NAME` constant, registered
with `services.Configure<T>`, and taken by constructor as `IOptions<T>` (read once) or
`IOptionsMonitor<T>` (read each time):

```csharp
public sealed class OwnerConfig
{
    public const string SECTION_NAME = "Turbo:Owner";

    /// <summary>The owner's player name, whatever its case. Empty names no one.</summary>
    public string Name { get; init; } = "";
}
```

```csharp
services.Configure<OwnerConfig>(builder.Configuration.GetSection(OwnerConfig.SECTION_NAME));
```

The `/// <summary>` on each property is what the Settings page shows beside it, so write it for
the person running the hotel. Mark a property with an attribute from `Turbo.Primitives.Settings`
when it is more than a plain value:

| Attribute | Means |
| --- | --- |
| `[SecretSetting]` | Never shown, only replaced (a password, a key). |
| `[StartupSetting]` | Shown but not changed in the panel: the panel itself depends on it (`Turbo:Database`, `Turbo:Admin:Url`). |
| `[LiveSetting]` | Applies at once (see below). |
| `[UrlSetting]` | Must be a whole `http://` or `https://` address, or empty. |

### Plugin Settings

A plugin's settings are a class marked `[PluginSettings]` (`Turbo.Contracts.Plugins`). There is
nothing to register: the server binds it to its section as the plugin loads and lists it on the
Settings page while the plugin is loaded.

```csharp
[PluginSettings("Skills")]
public sealed class SkillsSettings
{
    /// <summary>Experience one press of Train gives.</summary>
    public int ExperiencePerTrain { get; init; } = 25;
}
```

Take it by constructor as `IOptionsMonitor<SkillsSettings>` and read `CurrentValue` as you use it,
and a change saved on the panel reaches the plugin at once. In `appsettings.Development.json` it
sits under its section, beside `Turbo`:

```json
{
    "Skills": { "ExperiencePerTrain": 50 }
}
```

> [!NOTE]
> Settings are for a hotel's **policy**: an economy rate, a feature turned on or off, who may
> trade. How Habbo behaves is not a setting: it ships as Habbo does it.

### Settings That Apply at Once

The server reads most options once, as it starts, so a change waits for a restart. A property (or
class) marked `[LiveSetting]` promises that every reader takes it from
`IOptionsMonitor<T>.CurrentValue` each time it is used, never `IOptions<T>.Value` and never kept in
a field, so the panel can apply a change at once. Mark it only when that is true.

## The Sections

`appsettings.json` has every section. The ones you are most likely to change, and where each is
explained:

| Section | What it is | Guide |
| --- | --- | --- |
| `Turbo:Database` | Connection string and migration rules | [Database](database.md) |
| `Turbo:Owner` | Who owns the hotel | [Installation](installation.md#becoming-the-owner) |
| `Turbo:Admin` | The admin API and panel | [Admin Panel](admin-panel.md) |
| `Turbo:Web` | The public site and plugin web routes | [Public Site](public-site.md), [Web Routes](plugin-web-routes.md) |
| `Turbo:Gamedata` | The client's files, Habbo releases, plugin modules | [Gamedata](gamedata.md) |
| `Turbo:Plugin` | Plugin folders, dev paths, hot reload, uploads | [Plugin Lifecycle](plugin-loading.md) |
| `Turbo:Commands` | Chat commands | [Commands](commands.md) |
| `Turbo:Wired` | Wired limits | [Wired](plugin-wired.md) |
| `Turbo:Orleans` | Silo and gateway ports | [Orleans Internals](orleans.md) |
| `Turbo:Telemetry` | OpenTelemetry export | [Telemetry](telemetry.md) |
| `serverOptions` | The game's TCP and WebSocket listeners | [Installation](installation.md#what-listens-where) |
