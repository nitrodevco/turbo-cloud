# Installation

- [Introduction](#introduction)
- [Requirements](#requirements)
- [Getting Turbo](#getting-turbo)
    - [Bootstrapping](#bootstrapping)
    - [The Database](#the-database)
- [Running the Server](#running-the-server)
    - [What Listens Where](#what-listens-where)
    - [Becoming the Owner](#becoming-the-owner)
- [Connecting a Client](#connecting-a-client)
- [Your First Plugin](#your-first-plugin)
- [Next Steps](#next-steps)

## Introduction

Turbo is a Habbo hotel server written in C# on .NET and [Orleans](https://learn.microsoft.com/dotnet/orleans/).
It speaks Habbo's own protocol, so the Flash client and the [nitro-next](https://github.com/nitrodevco/nitro-next)
client both connect to it, and it is built to be extended: commands, packets, furni logic, wired
boxes, web routes, settings, database tables and client code all come from **plugins** that the
server compiles, loads and reloads while the hotel keeps running.

This page gets a server running on your machine, a client connected to it, and your first plugin
loaded.

## Requirements

- The **.NET SDK 10**. The exact version is pinned in `global.json`; `dotnet --version` should
  print a `10.x`.
- **MySQL 8** or **MariaDB 10.6+**, local or reachable.
- **Git**.
- **Node.js 22**, only if you write a plugin with a client module or run the client yourself.

## Getting Turbo

```shell
git clone https://github.com/nitrodevco/turbo-cloud.git
cd turbo-cloud
```

### Bootstrapping

Run the bootstrap script once:

```shell
sh scripts/bootstrap.sh            # bash or zsh
pwsh -File scripts/bootstrap.ps1   # PowerShell
```

It checks your SDK, turns on the repository's git hooks, creates `appsettings.Development.json`
from `appsettings.json` (with Turbo's own logs at `Debug`) and builds the server once.
`appsettings.Development.json` is yours: git ignores it, and it is where your local settings go.

### The Database

Anything MySQL 8 or MariaDB 10.6+ will do. Docker is the quickest:

```shell
docker run -d --name turbo-mysql -e MYSQL_ROOT_PASSWORD=turbo-dev -e MYSQL_DATABASE=turbo \
  -p 127.0.0.1:3306:3306 mysql:8.4 --character-set-server=utf8mb4 --collation-server=utf8mb4_unicode_ci
```

Then point Turbo at it in `appsettings.Development.json`:

```json
{
    "Turbo": {
        "Database": {
            "ConnectionString": "server=127.0.0.1;port=3306;user=root;password=turbo-dev;database=turbo"
        }
    }
}
```

That is all the database needs. Turbo creates and migrates its own schema when it starts, and
says what it did:

```
Database core: creating the database, 47 migration(s)
Database core: applying 20260208101510_InitialCreate (1/47)
...
```

> [!NOTE]
> Turbo refuses, in one readable message, to touch a database made by a newer version or to run a
> migration that would drop data. [The Database](database.md) explains each refusal and the
> `migrate` command.

## Running the Server

Start the server in the Development environment:

```shell
DOTNET_ENVIRONMENT=Development dotnet run --project Turbo.Main/Turbo.Main.csproj
```

In PowerShell:

```powershell
$env:DOTNET_ENVIRONMENT = "Development"; dotnet run --project Turbo.Main/Turbo.Main.csproj
```

The Development environment reads `appsettings.Development.json`, logs the configuration sources
it loaded, and reloads plugins as their files change.

### What Listens Where

Out of the box, only the game listens. The web hosts are off until you turn them on:

| What | Address | Setting |
| --- | --- | --- |
| Game, TCP (the Flash client) | `127.0.0.1:30000` | `serverOptions:TcpServer` |
| Game, WebSocket (nitro-next) | `0.0.0.0:9001` | `serverOptions:WebSocketServer` |
| Admin API, for the [admin panel](admin-panel.md) | `127.0.0.1:8090` | `Turbo:Admin` (`Enabled: false`) |
| Public site API and plugin web routes | `127.0.0.1:8092` | `Turbo:Web` (`Enabled: false`) |
| Gamedata: the client's files, plugin modules | `127.0.0.1:8094` | `Turbo:Gamedata` (`Enabled: false`) |

[Configuration](configuration.md) explains how to change any of them.

### Becoming the Owner

The hotel's owner is in the `admin` group and holds every permission. Name yourself in
`appsettings.Development.json`:

```json
{
    "Turbo": {
        "Owner": { "Name": "YourName" }
    }
}
```

The player with that name becomes the owner when they are created, or at the next start if they
already exist. With no owner named, in the Development environment the first player created on an
empty hotel becomes the owner (`Turbo:Owner:FirstPlayerInDevelopment`, on by default). A live
hotel never makes someone the owner for being first.

## Connecting a Client

nitro-next is the client Turbo is developed with. Its [development guide](https://github.com/nitrodevco/nitro-next/blob/pixi-render/docs/development.md)
starts it with Yarn; point its `socket.url` at Turbo's WebSocket listener (`ws://localhost:9001`),
not at the TCP port or the Orleans gateway.

A client logs in with a **login ticket** (an SSO ticket), passed as `?sso=<ticket>` in the client's
address. The hotel issues them in two places:

- the [public site](public-site.md)'s **Play** button, once someone has signed in;
- the [admin panel](admin-panel.md#login-tickets)'s **Login ticket** card on a player's page.

A plain ticket works once; the panel can also issue one that can be used again.

## Your First Plugin

A plugin is a folder of C# sources, a `manifest.json` and any files it brings. The template gives
you both halves of one, a server half and a client module, with tests and one dev command:

```shell
dotnet new install ./templates/turbo-plugin
dotnet new turbo-plugin -n Skills --key skills --turbo <path to turbo-cloud> --nitro <path to nitro-next>
cd Skills && npm run dev
```

Tell the server where it is, and the server loads it, compiles it again whenever you save a `.cs`
file, and swaps the new version in without a restart:

```json
{
    "Turbo": {
        "Plugin": { "DevPluginPaths": ["<path to Skills>/dist/skills"] }
    }
}
```

The template's sample is a skill book: a `:skills` command, a window and a toolbar icon in the
client, a setting and a weekly reset. [Directory Structure](structure.md) walks through each file.

## Next Steps

- [Configuration](configuration.md): where settings come from and how to change them.
- [Directory Structure](structure.md): what goes where in a plugin.
- [Packet Lifecycle](lifecycle.md): what happens between a click in the client and the reply.
- [Plugin Lifecycle](plugin-loading.md): how a plugin is compiled, loaded and reloaded.
