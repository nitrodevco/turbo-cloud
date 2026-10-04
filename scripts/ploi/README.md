# Deploying Turbo with Ploi

These scripts run Turbo on a Ploi-managed Ubuntu server:

| File | Purpose |
| --- | --- |
| `deploy.sh` | Called by Ploi's deploy script. Publishes a release, applies migrations, switches releases and restarts the daemon. |
| `start.sh` | The Ploi daemon command. Runs the current release with the `.env` settings applied. |
| `lib.sh` | Shared helpers. Loads `.env` and maps it onto Turbo's configuration keys. |
| `turbo.env.example` | Every setting, with defaults. Paste it into the site's Environment tab. |
| `nginx-websocket.conf` | nginx block that proxies `wss://` to the WebSocket server. |

## Layout on the server

```
/home/ploi/<site>/                 git checkout (Ploi pulls here)
/home/ploi/<site>/.env             settings (Ploi > Site > Environment)
/home/ploi/<site>/.deploy/
    releases/<timestamp>-<sha>/    one publish per deploy (last TURBO_KEEP_RELEASES kept)
    current -> releases/...        what the daemon runs
    shared/plugins/                plugin folder, kept across releases
    turbo.pid                      written by start.sh, used by deploy.sh to restart
```

## One-time setup

1. **Install the .NET 10 SDK** as the `ploi` user (SSH in as `ploi`):

   ```bash
   curl -sSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 10.0
   ```

   The scripts find `~/.dotnet` on their own. A system-wide install under `/usr/share/dotnet` or `/usr/lib/dotnet` works too.

2. **Create a database** (Ploi > Server > Databases) with a user for it.

3. **Create the site** with the domain the client will connect to (for example `ws.example.com`). Install this repository on it.

4. **Environment tab.** Paste `scripts/ploi/turbo.env.example` and fill in the `TURBO_DB_*` settings. Generate your own `TURBO_CRYPTO_*` keys; the ones in `appsettings.json` are public.

5. **Deploy script** (Site > Deployment). Replace it with:

   ```bash
   cd {SITE_DIRECTORY}
   git pull origin {BRANCH}
   bash scripts/ploi/deploy.sh
   ```

   Deploy once now. The first deploy builds, migrates and says Turbo is not running yet.

6. **Daemon** (Server > Daemons):
   - Command: `bash /home/ploi/<site>/scripts/ploi/start.sh`
   - User: `ploi`
   - Processes: `1`

   Ploi runs daemons under Supervisor with auto-restart. `deploy.sh` depends on that: it stops the process with `SIGTERM` and Supervisor starts the new release. The daemon's log in Ploi shows Turbo's console output.

7. **WebSocket over TLS.** Issue a certificate in the site's SSL tab. Then, in Site > Manage > Edit NGINX configuration, replace the `location / { ... }` block with the one in `nginx-websocket.conf`. The client then connects to `wss://ws.example.com/`, so set the socket URL in the Nitro client's `renderer-config.json` to that address.

   Without nginx, set `TURBO_WS_HOST=0.0.0.0`, open `TURBO_WS_PORT` in Ploi's firewall and connect with `ws://<server-ip>:<port>/`. That is plain text. Browsers block it on pages served over `https://`.

## IP and port settings

All of these are set in `.env`. Restart the daemon (or deploy) after changing them.

| Setting | Configuration key | Default |
| --- | --- | --- |
| `TURBO_WS_HOST` / `TURBO_WS_PORT` | `serverOptions:WebSocketServer:listeners:0` | `0.0.0.0:9001` from appsettings, `127.0.0.1:9001` in the example |
| `TURBO_TCP_HOST` / `TURBO_TCP_PORT` | `serverOptions:TcpServer:listeners:0` | `127.0.0.1:30000` |
| `TURBO_SILO_ADDRESS`, `TURBO_SILO_PORT`, `TURBO_GATEWAY_PORT` | `Turbo:Orleans` | `127.0.0.1`, `11111`, `3000` |
| `TURBO_DB_*` or `TURBO_DB_CONNECTION_STRING` | `Turbo:Database:ConnectionString` | from appsettings |

Orleans listens on every interface for its silo and gateway ports. Leave them closed in the firewall.

To override any other setting from `.env`, use the environment-variable form Program.cs reads: prefix the key with `TURBO__` and replace `:` with `__`, for example `TURBO__Turbo__Rooms__MaxPlayersLimit=75`. `serverOptions` is the exception. SuperSocket reads it through its own host, which ignores the prefix, so its keys go unprefixed (`serverOptions__WebSocketServer__listeners__0__port=9001`). An `appsettings.Production.json` in the repository root is also published with each release.

## Admin panel

The admin panel runs as a second Ploi site on its own subdomain, from the `turbo-admin`
repository. On this site, it needs only `TURBO_ADMIN_ENABLED=true` and
`TURBO_ADMIN_PANEL_URL=https://<panel subdomain>` in the Environment tab.
[`docs/admin-panel.md`](../../docs/admin-panel.md) covers the whole setup, from the panel site to
the first admin's passkey.

## Migrations

`deploy.sh` runs `dotnet ef database update` against the configured database after the publish succeeds and before the running server is touched. If a migration fails, the deploy stops and the old release keeps running. MySQL does not roll back schema changes, so a migration that fails part-way may leave some of its changes applied.

Plugin migrations are unchanged. Each plugin still applies its own when it loads.

## Rollback

```bash
cd /home/ploi/<site>/.deploy
ls releases
ln -sfn "$PWD/releases/<older-release>" current
kill -TERM "$(cat turbo.pid)"
```

This does not undo migrations. Only roll back to a release whose code works with the current schema.
