# Server settings

- [Introduction](#introduction)
- [Where a value comes from](#where-a-value-comes-from)
- [What the page shows](#what-the-page-shows)
- [Kinds of setting](#kinds-of-setting)
- [Values](#values)
- [The admin API](#the-admin-api)
- [Who can do what](#who-can-do-what)
- [External variables that follow a setting](#external-variables-that-follow-a-setting)
- [More than one silo](#more-than-one-silo)
- [Starting up](#starting-up)

## Introduction

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
  whose class names its section in a `SECTION_NAME` constant, and of every loaded plugin's
  `[PluginSettings("Section")]` class. A nested class (`Turbo:Web:Discord`) is listed as settings
  of its own. A list or map is one setting, edited as JSON.
- **Its summary**, from the `/// <summary>` on its property. The build writes each assembly's XML
  documentation beside it (`GenerateDocumentationFile` in `Directory.Build.props`), and the page
  reads it from there.
- **Value and running value.** The server reads most options once, as it starts, so a change
  applies after a restart. Until then the setting shows **after restart**, with the value it runs
  with now, and the page offers **Restart** to staff with `admin.server.restart` (see
  `docs/admin-panel.md`, *Restarting the server*). A setting marked **applies now** is read as it
  is used, so a change takes effect at once.
- **Its source**: the default, the file that sets it, the panel, or the environment variable.

## Kinds of setting

| Marked | Examples | In the panel |
| --- | --- | --- |
| `[SecretSetting]` | `Turbo:Crypto:PrivateKey`, `Turbo:Web:Discord:ClientSecret`, `Turbo:Database:ConnectionString` | Can be replaced, never shown. Its history records only that it changed. No external variable can follow it. |
| `[StartupSetting]` | `Turbo:Database`, `Turbo:Orleans`, `Turbo:Admin:Enabled`, `Url`, `PanelUrl`, `PasskeyRpId` | Shown, not changed: the panel stands on it. A wrong value would lock staff out of the panel that could fix it. |
| `[LiveSetting]` | `Turbo:Operations:CountdownReminderMinutes`, `DefaultShutdownMinutes`, `MaxCountdownMinutes`, `RestartExitCode`; `Turbo:Admin:ClientLoginUrl`, `ClientPreviewUrl` | Changed, **applies now**: every reader reads it through `IOptionsMonitor<T>.CurrentValue` as it uses it, so it never waits on a restart. |
| `[UrlSetting]` | `Turbo:Admin:ClientLoginUrl`, `ClientPreviewUrl`, `ImagerUrl`, `Turbo:Web:SiteUrl`, `ClientUrl`, `Turbo:Gamedata:PublicUrl`, the asset and camera renderer addresses | Text that is a whole web address (`http://` or `https://` and a host) or empty; anything else is refused with a message. Placeholders (`{name}`, `%libname%`) may stand anywhere in it. Combines with the others. |
| none of these | everything else | Changed, applies after a restart. |

Mark a new option `[SecretSetting]` when its value must not be read, and `[StartupSetting]` (on the
property or its class) when the panel itself depends on it. Mark it `[LiveSetting]` only when every
reader takes it from `IOptionsMonitor<T>.CurrentValue` each time (never `IOptions<T>.Value`, and
never kept in a field or used to build something once at start): the page then compares the value
with what the monitor reads now instead of with what the server started with.

## Values

A value is JSON: `"text"` in quotes, `true`, `120`, `0.5`, an enum by name (`"Auto"`), a duration
as `"00:05:00"`, a list `["a", "b"]`, a map `{ "chair": 5 }`. The panel's fields write it for you.
It is checked against the setting's type before it is saved. One pair of quotes (`"` or `'`)
typed around the whole of a text value is taken off, so `"https://example.com"` pasted from a
`.env` file is saved as the address, not with its quotes.

A section a module validates as the server starts (`.Validate(...).ValidateOnStart()`, such as
`Turbo:Players` or `Turbo:Movements`) is built with the value in place, as a restart would build
it, before the value is saved. If its validators would refuse it, so would the restart: the value
is refused with what they say, and nothing is saved or written to the history. **Put back** is
checked the same way, against the value it would leave. A section that fails already (a file edited
since the server started) isn't held to it: a change is refused only for a failure it adds, so the
section can be mended a setting at a time.

If the panel's overrides couldn't be read as the server started (the database unreachable, a value
that isn't JSON), the server runs without them, logs why, and the Settings page says so above the
list (`startupError` in `GET /api/settings`) until it restarts and reads them.

**Lists and maps are taken whole.** A list set in the panel replaces the list in
`appsettings.json`; it is not merged item by item. The binder fills a list on top of the items its
class starts with, though, so those can't be taken away by any configuration, a file's included.
The panel keeps them first and saves only what follows. A map's default keys can be changed, not
removed.

## The admin API

The Settings page is these calls, which a script can make too (authenticated as
[admin-panel.md](admin-panel.md#calling-the-admin-api) says, with the same nodes as below):

| Call | Body or query | Answers |
| --- | --- | --- |
| `GET /api/settings` | | `{ settings: [...], canManage, startupError }`: every setting with its value, default, where it comes from (`source`) and whether a change applies now (`live`) |
| `PUT /api/settings` | `{ "path": "RareValues:RepriceRate", "value": "0.5" }` | The setting as saved. `value` is the JSON text of the value (`"0.5"`, `"true"`, `"\"text\""`, `"[\"a\"]"`). 400 with the reason when it is refused. |
| `DELETE /api/settings` | `?path=RareValues:RepriceRate` | 204 put back to the file's or the default value, 404 when there was no override |
| `GET /api/settings/history` | `?page=0` | Who changed what, newest first |

A plugin's settings are under the section its `[PluginSettings("<Section>")]` names.

## Who can do what

| Node | Allows |
| --- | --- |
| `admin.settings.view` | The Settings page: every value, where it comes from and what waits on a restart. Secrets are never shown. |
| `settings.manage` | Overriding a setting, replacing a secret and putting a setting back. |

A setting is the whole server's, so give `settings.manage` only to those who run it.

## External variables that follow a setting

An external variable can be linked to a setting (**Gamedata > Variables**), so the client gets
the setting's value, the same way one follows a gamedata file's address (see `docs/gamedata.md`). It follows the value configured now, so the variables
are built again as soon as the setting is saved, without waiting for the server's restart: at once
on the silo it was saved on, within `CheckSeconds` on the others (below).

## More than one silo

A setting saved or put back reloads the configuration of the silo whose panel it was saved
through. Every silo also asks the database every `Turbo:Settings:CheckSeconds` (default `5`)
whether the panel's overrides are still those it read (`ServerSettingsWatcher`), and reads them
again when they aren't: so within `CheckSeconds` another silo's Settings page shows the change, an
**applies now** setting applies there too, and the external variables that follow a setting are
built again. A setting that applies after a restart still waits on each silo's own restart.
`0` never asks: a change saved elsewhere then waits on this silo's restart.

## Starting up

The overrides are read straight from the database as the configuration is built, before any module
reads its options, with the connection `Turbo:Database` gives (which is why the panel can't change
that section). Until `server_settings` exists, nothing is overridden. If the database can't be
reached, nothing is overridden either, and the server reports the database itself.

`appsettings.json` edited on disk while the server runs is picked up by the page at once. Like a
panel change, it applies after a restart.
