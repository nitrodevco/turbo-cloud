#!/usr/bin/env bash
# Ploi deploy script for Turbo. Call it from the site's deploy script after the git pull:
#
#   cd {SITE_DIRECTORY}
#   git pull origin {BRANCH}
#   bash scripts/ploi/deploy.sh
#
# Steps: publish a new release, apply the database migrations, point .deploy/current at the
# release, then restart the daemon (scripts/ploi/start.sh) and wait for the WebSocket port.
# Nothing touches the running server until the publish and the migrations have succeeded.
set -euo pipefail

# shellcheck source=lib.sh
. "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

turbo_load_env
turbo_find_dotnet
turbo_export_app_env

cd "$TURBO_SITE_DIR"

dotnet_version="$("$TURBO_DOTNET" --version | tr -d '\r')"
case "$dotnet_version" in
  10.*) turbo_log "Using .NET SDK $dotnet_version" ;;
  *) turbo_fail ".NET SDK 10.x is required, found '$dotnet_version'." ;;
esac

connection_string="$(turbo_connection_string)"
[ -n "$connection_string" ] || turbo_fail "Set TURBO_DB_DATABASE (and the other TURBO_DB_* settings) or TURBO_DB_CONNECTION_STRING in .env."

release_name="$(date +%Y%m%d%H%M%S)-$(git rev-parse --short HEAD 2>/dev/null || echo manual)"
release_dir="$TURBO_DEPLOY_ROOT/releases/$release_name"

mkdir -p "$TURBO_DEPLOY_ROOT/releases" "$TURBO_PLUGIN_PATH"

turbo_log "Restoring tools"
"$TURBO_DOTNET" tool restore

turbo_log "Publishing release $release_name"
"$TURBO_DOTNET" publish Turbo.Main/Turbo.Main.csproj \
  --configuration Release \
  --output "$release_dir" \
  --nologo

# The design-time context factory resolves appsettings.json from the parent of the current
# directory and detects the server version from Turbo:Database:ConnectionString before EF
# applies --connection, so it runs from Turbo.Database with the connection string in the
# environment as well.
turbo_log "Applying database migrations"
(
  cd Turbo.Database
  Turbo__Database__ConnectionString="$connection_string" \
    "$TURBO_DOTNET" ef database update \
    --project Turbo.Database.csproj \
    --startup-project Turbo.Database.csproj \
    --configuration Release \
    --connection "$connection_string"
)

turbo_log "Activating release"
ln -sfn "$release_dir" "$TURBO_DEPLOY_ROOT/current.tmp"
mv -Tf "$TURBO_DEPLOY_ROOT/current.tmp" "$TURBO_DEPLOY_ROOT/current"

keep="${TURBO_KEEP_RELEASES:-5}"
find "$TURBO_DEPLOY_ROOT/releases" -mindepth 1 -maxdepth 1 -type d -printf '%f\n' \
  | sort -r \
  | tail -n "+$((keep + 1))" \
  | while read -r old; do
      rm -rf "${TURBO_DEPLOY_ROOT:?}/releases/$old"
    done

# The daemon (Supervisor, which Ploi uses for daemons) restarts start.sh when the process
# exits, and start.sh runs whatever .deploy/current points at. SIGTERM lets the host shut down
# cleanly first.
old_pid="$(turbo_running_pid || true)"

if [ -z "$old_pid" ]; then
  turbo_log "Turbo is not running. Add the Ploi daemon described in scripts/ploi/README.md to start it."
  exit 0
fi

turbo_log "Stopping Turbo (pid $old_pid)"
kill -TERM "$old_pid"

for _ in $(seq 1 "${TURBO_STOP_TIMEOUT:-60}"); do
  kill -0 "$old_pid" 2>/dev/null || break
  sleep 1
done

if kill -0 "$old_pid" 2>/dev/null; then
  turbo_fail "Turbo (pid $old_pid) did not stop within ${TURBO_STOP_TIMEOUT:-60}s."
fi

ws_host="${TURBO_WS_HOST:-127.0.0.1}"
[ "$ws_host" = "0.0.0.0" ] && ws_host="127.0.0.1"
ws_port="${TURBO_WS_PORT:-9001}"

turbo_log "Waiting for the daemon to bring Turbo back on $ws_host:$ws_port"
for _ in $(seq 1 "${TURBO_START_TIMEOUT:-120}"); do
  new_pid="$(turbo_running_pid || true)"

  if [ -n "$new_pid" ] && [ "$new_pid" != "$old_pid" ] && turbo_port_open "$ws_host" "$ws_port"; then
    turbo_log "Turbo is up (pid $new_pid), release $release_name"
    exit 0
  fi

  sleep 1
done

turbo_fail "Turbo did not start listening on $ws_host:$ws_port within ${TURBO_START_TIMEOUT:-120}s. Check the daemon log in Ploi."
