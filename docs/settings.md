# Server settings

Every option of every config section (`Turbo:Rooms`, `Turbo:Admin`, `Turbo:Gamedata`, ...) is
listed on the admin panel's **Settings** page. Each shows its value, where that value comes from and
whether a restart is waiting on it. Staff with `settings.manage` can override a value there, without
editing `appsettings.json` or the Ploi environment.

The code is in `Turbo.Main/Settings`. The overrides are kept in `server_settings`, and every change
in `server_setting_changes`.

## Where a value comes from

Lowest first; each overrides the ones before it:

1. **The default** the config class ships with (`public int MaxUsers { get; init; } = 25;`).
2. **`appsettings.json`**, then `appsettings.{Environment}.json` (and user secrets in development).
3. **The panel** (`server_settings`).
4. **The environment and the command line**: `TURBO__...` variables, the unprefixed ones Ploi's
   `lib.sh` maps (`Turbo__Admin__ClientLoginUrl`), and command-line arguments.

The environment always has the last word. A setting it sets shows as locked by that variable, and
the panel refuses to change it. If a panel value ever stops the server from starting, an
environment variable overrides it, and **Put back** (or deleting its row) removes it.

## What the page shows

- **Every setting of every config class** a module registers with `services.Configure<T>(section)`
  whose class names its section in a `SECTION_NAME` constant. A nested class (`Turbo:Web:Discord`)
  is listed as settings of its own. A list or map is one setting, edited as JSON.
- **Its summary**, from the `/// <summary>` on its property. The build writes each assembly's XML
  documentation beside it (`GenerateDocumentationFile` in `Directory.Build.props`), and the page
  reads it from there.
- **Value and running value.** The server reads its options once, as it starts, so a change applies
  after a restart. Until then the setting shows **after restart**, with the value it runs with
  now.
- **Its source**: the default, the file that sets it, the panel, or the environment variable.

## Kinds of setting

| Marked | Examples | In the panel |
| --- | --- | --- |
| `[SecretSetting]` | `Turbo:Crypto:PrivateKey`, `Turbo:Web:Discord:ClientSecret`, `Turbo:Database:ConnectionString` | Can be replaced, never shown. Its history records only that it changed. No external variable can follow it. |
| `[StartupSetting]` | `Turbo:Database`, `Turbo:Orleans`, `Turbo:Admin:Enabled`, `Url`, `PanelUrl`, `PasskeyRpId` | Shown, not changed: the panel stands on it. A wrong value would lock staff out of the panel that could fix it. |
| neither | everything else | Changed, applies after a restart. |

Mark a new option `[SecretSetting]` when its value must not be read, and `[StartupSetting]` (on the
property or its class) when the panel itself depends on it.

## Values

A value is JSON: `"text"` in quotes, `true`, `120`, `0.5`, an enum by name (`"Auto"`), a duration
as `"00:05:00"`, a list `["a", "b"]`, a map `{ "chair": 5 }`. The panel's fields write it for you.
It is checked against the setting's type before it is saved.

**Lists and maps are taken whole.** A list set in the panel replaces the list in
`appsettings.json`; it is not merged item by item. The binder fills a list on top of the items its
class starts with, though, so those can't be taken away by any configuration, a file's included.
The panel keeps them first and saves only what follows. A map's default keys can be changed, not
removed.

## Who can do what

| Node | Allows |
| --- | --- |
| `admin.settings.view` | The Settings page: every value, where it comes from and what waits on a restart. Secrets are never shown. |
| `settings.manage` | Overriding a setting, replacing a secret and putting a setting back. |

A setting is the whole server's, so give `settings.manage` only to those who run it.

## External variables that follow a setting

An external variable can be linked to a setting (**Gamedata > Variables**), so the client gets
the setting's value, the same way one follows a gamedata file's address (see `docs/gamedata.md`). It follows the value configured now, so the variables
are built again as soon as the setting is saved, without waiting for the server's restart.

## Starting up

The overrides are read straight from the database as the configuration is built, before any module
reads its options, with the connection `Turbo:Database` gives (which is why the panel can't change
that section). Until `server_settings` exists, nothing is overridden. If the database can't be
reached, nothing is overridden either, and the server reports the database itself.

`appsettings.json` edited on disk while the server runs is picked up by the page at once. Like a
panel change, it applies after a restart.
