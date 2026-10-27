# Deployment

- [Introduction](#introduction)
- [What a Hotel Runs](#what-a-hotel-runs)
- [Deploying With Ploi](#deploying-with-ploi)
    - [Settings in the Environment](#settings-in-the-environment)
    - [Migrations on Deploy](#migrations-on-deploy)
    - [Rolling Back](#rolling-back)
- [Restarts](#restarts)
- [Plugins in Production](#plugins-in-production)
- [Checklist](#checklist)

## Introduction

A live hotel is the same server you run locally, published as a release and run by a supervisor
that starts it again when it stops. The repository ships scripts for one way of doing that, on a
[Ploi](https://ploi.io)-managed Ubuntu server, and every guide on this site assumes those scripts
where it talks about production. Nothing in Turbo depends on Ploi: any host that can run .NET 10
and restart a process will do.

## What a Hotel Runs

| Part | From | Guide |
| --- | --- | --- |
| The server | this repository (`Turbo.Main`) | this page |
| The database | MySQL 8 or MariaDB 10.6+ | [Database](database.md) |
| The admin panel | the `turbo-admin` repository, its own site | [Admin Panel](admin-panel.md) |
| The public site | `turbo-web`, its own site | [Public Site](public-site.md) |
| The client's files | the server's gamedata host, served under `/gamedata/` | [Gamedata](gamedata.md) |
| The client | nitro-next, built to static files | [Client Development](https://github.com/nitrodevco/nitro-next/blob/pixi-render/docs/development.md) |

## Deploying With Ploi

[`scripts/ploi/README.md`](../scripts/ploi/README.md) is the step-by-step setup. In short:

1. Install the .NET 10 SDK as the `ploi` user and create a database.
2. Create the site for the domain the client connects to, and install this repository on it.
3. Paste `scripts/ploi/turbo.env.example` into the site's **Environment** tab and fill in the
   database. Generate your own `TURBO_CRYPTO_*` keys: the ones in `appsettings.json` are public.
4. Make the deploy script `git pull` and `bash scripts/ploi/deploy.sh`.
5. Add a daemon running `bash /home/ploi/<site>/scripts/ploi/start.sh` as `ploi`.
6. Put the WebSocket behind nginx with a certificate (`nginx-websocket.conf`), so the client
   connects to `wss://`.

Each deploy publishes a new release into `.deploy/releases/`, migrates the database, points
`.deploy/current` at it and restarts the daemon. Plugins, asset bundles and camera pictures live in
`.deploy/shared/` and are kept across releases.

### Settings in the Environment

`lib.sh` maps the `.env` file's short names onto Turbo's settings (`TURBO_DB_MIGRATE`,
`TURBO_OWNER_DISCORD_ID`, `TURBO_ADMIN_ENABLED`, ...). Any other setting can be set with the
prefixed form the server reads, `TURBO__` and the full path with `__` for each `:`:

```shell
TURBO__Turbo__Rooms__MaxPlayersLimit=75
TURBO__Logging__LogLevel__Turbo=Debug
```

`serverOptions` (the game's listeners) is read by SuperSocket's own host, which ignores the prefix:
`serverOptions__WebSocketServer__listeners__0__port=9001`.

### Migrations on Deploy

`deploy.sh` runs the new release's `Turbo.Main migrate` after the publish and before the running
server is touched. If a migration fails, the deploy stops and the old release keeps running.
Plugins migrate their own tables as they load. Leave `TURBO_DB_MIGRATE` on `Auto` if you run
plugins that have tables. [Database](database.md) explains each refusal.

> [!WARNING]
> MySQL cannot roll back a schema change. Back up before a deploy whose migration the log calls
> destructive.

### Rolling Back

```shell
cd /home/ploi/<site>/.deploy
ln -sfn "$PWD/releases/<older-release>" current
kill -TERM "$(cat turbo.pid)"
```

This does not undo migrations: only roll back to a release whose code works with the current
schema.

## Restarts

Turbo expects its supervisor to start it again whenever it exits. The admin panel's **Restart**
stops the server, which exits with `Turbo:Operations:RestartExitCode` (75); `:shutdown` exits with 0.
Ploi runs daemons under Supervisor with auto-restart, which starts it again either way.

## Plugins in Production

A live hotel loads plugins from `Turbo:Plugin:PluginFolderPath` (`.deploy/shared/plugins/` with
Ploi). A plugin gets there one of three ways:

- copied into the folder, then **Rescan** on the admin panel's Plugins page;
- uploaded as a package on the Plugins page, when `Turbo:Plugin:UploadEnabled` is on;
- linked to a GitHub repository, whose releases the panel offers as updates.

Hot reload on file changes is a Development feature; a live hotel reloads a plugin when staff ask.

> [!WARNING]
> A plugin runs with the server's own access to its database, files and players. Read a plugin,
> or know its author, before you install it.

## Checklist

- Your own `TURBO_CRYPTO_*` keys.
- The owner named by Discord id (`TURBO_OWNER_DISCORD_ID`), or registration closed until you have
  signed up.
- The WebSocket served as `wss://` behind nginx.
- If the sites are behind Cloudflare, nginx told the visitors' real addresses:
  [Behind Cloudflare](cloudflare.md).
- A database backup before every deploy that migrates.
