# Shared by deploy.sh and start.sh. Sourced, not run.
#
# Loads the site's .env (the file Ploi's "Environment" tab edits) and turns its TURBO_* settings
# into the configuration keys the emulator reads. Program.cs adds environment variables with the
# "TURBO__" prefix, which is stripped, so TURBO__Turbo__Database__ConnectionString sets
# Turbo:Database:ConnectionString. Only settings present in .env are exported; anything left out
# falls back to appsettings.json / appsettings.Production.json.

TURBO_SITE_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"

turbo_log() {
  printf '==> %s\n' "$*"
}

turbo_fail() {
  printf 'ERROR: %s\n' "$*" >&2
  exit 1
}

turbo_load_env() {
  local env_file="${TURBO_ENV_FILE:-$TURBO_SITE_DIR/.env}"

  if [ ! -f "$env_file" ]; then
    turbo_fail "Missing $env_file. Copy scripts/ploi/turbo.env.example into the site's Environment tab in Ploi and fill it in."
  fi

  set -a
  # shellcheck disable=SC1090
  . "$env_file"
  set +a

  TURBO_DEPLOY_ROOT="${TURBO_DEPLOY_ROOT:-$TURBO_SITE_DIR/.deploy}"
  TURBO_PID_FILE="$TURBO_DEPLOY_ROOT/turbo.pid"
  TURBO_PLUGIN_PATH="${TURBO_PLUGIN_PATH:-$TURBO_DEPLOY_ROOT/shared/plugins}"
}

# Ploi runs deploy scripts and daemons with a minimal PATH, so a user-level install from
# dotnet-install.sh (~/.dotnet) is not on it.
turbo_find_dotnet() {
  local candidate

  for candidate in "${DOTNET_ROOT:-}/dotnet" "$HOME/.dotnet/dotnet" /usr/share/dotnet/dotnet /usr/lib/dotnet/dotnet /usr/bin/dotnet; do
    if [ -x "$candidate" ]; then
      TURBO_DOTNET="$candidate"
      export DOTNET_ROOT="$(dirname "$candidate")"
      export PATH="$DOTNET_ROOT:$DOTNET_ROOT/tools:$PATH"
      return
    fi
  done

  if command -v dotnet >/dev/null 2>&1; then
    TURBO_DOTNET="$(command -v dotnet)"
    return
  fi

  turbo_fail "dotnet was not found. Install the .NET 10 SDK on the server (see scripts/ploi/README.md)."
}

# The connection string from TURBO_DB_CONNECTION_STRING, or built from the TURBO_DB_* parts.
# The password is double-quoted (quotes inside doubled) so a ';' or '=' in it survives.
turbo_connection_string() {
  if [ -n "${TURBO_DB_CONNECTION_STRING:-}" ]; then
    printf '%s' "$TURBO_DB_CONNECTION_STRING"
    return
  fi

  if [ -z "${TURBO_DB_DATABASE:-}" ]; then
    return
  fi

  local password="${TURBO_DB_PASSWORD:-}"
  password="${password//\"/\"\"}"

  printf 'server=%s;port=%s;user=%s;password="%s";database=%s' \
    "${TURBO_DB_HOST:-127.0.0.1}" \
    "${TURBO_DB_PORT:-3306}" \
    "${TURBO_DB_USERNAME:-root}" \
    "$password" \
    "$TURBO_DB_DATABASE"
}

# Exports "<prefix><key>" when the named .env setting is present. The prefix defaults to TURBO__.
turbo_map() {
  local setting="$1"
  local key="$2"
  local prefix="${3-TURBO__}"

  if [ -n "${!setting+x}" ]; then
    export "$prefix$key=${!setting}"
  fi
}

turbo_export_app_env() {
  export DOTNET_ENVIRONMENT="${TURBO_ENVIRONMENT:-Production}"
  export DOTNET_CLI_TELEMETRY_OPTOUT=1

  local connection_string
  connection_string="$(turbo_connection_string)"

  if [ -n "$connection_string" ]; then
    export "TURBO__Turbo__Database__ConnectionString=$connection_string"
  fi

  turbo_map TURBO_DB_LOGGING Turbo__Database__LoggingEnabled
  turbo_map TURBO_DB_MIGRATE Turbo__Database__Migrate
  turbo_map TURBO_DB_ALLOW_DESTRUCTIVE_MIGRATIONS Turbo__Database__AllowDestructiveMigrations
  turbo_map TURBO_DB_MIGRATION_LOCK_SECONDS Turbo__Database__MigrationLockSeconds
  turbo_map TURBO_DB_SERVER_VERSION Turbo__Database__ServerVersion

  # SuperSocket builds its own host per server, which reads plain environment variables and
  # not the TURBO__ prefixed ones, so the listeners take the unprefixed key.
  turbo_map TURBO_WS_HOST serverOptions__WebSocketServer__listeners__0__ip ""
  turbo_map TURBO_WS_PORT serverOptions__WebSocketServer__listeners__0__port ""
  turbo_map TURBO_TCP_HOST serverOptions__TcpServer__listeners__0__ip ""
  turbo_map TURBO_TCP_PORT serverOptions__TcpServer__listeners__0__port ""

  turbo_map TURBO_SILO_ADDRESS Turbo__Orleans__SiloAddress
  turbo_map TURBO_SILO_PORT Turbo__Orleans__SiloPort
  turbo_map TURBO_GATEWAY_PORT Turbo__Orleans__GatewayPort

  turbo_map TURBO_CRYPTO_PUBLIC_KEY Turbo__Crypto__PublicKey
  turbo_map TURBO_CRYPTO_PRIVATE_KEY Turbo__Crypto__PrivateKey
  turbo_map TURBO_CRYPTO_ENCRYPTION Turbo__Crypto__EnableServerToClientEncryption

  turbo_map TURBO_BADGE_ASSET_URL Turbo__Achievements__BadgeAssetUrl

  turbo_map TURBO_OWNER_NAME Turbo__Owner__Name
  turbo_map TURBO_OWNER_DISCORD_ID Turbo__Owner__DiscordId

  turbo_map TURBO_ADMIN_ENABLED Turbo__Admin__Enabled
  turbo_map TURBO_ADMIN_URL Turbo__Admin__Url
  turbo_map TURBO_ADMIN_PANEL_URL Turbo__Admin__PanelUrl
  turbo_map TURBO_ADMIN_PASSKEY_RP_ID Turbo__Admin__PasskeyRpId
  turbo_map TURBO_ADMIN_CLIENT_CONFIG_URL Turbo__Admin__ClientConfigUrl

  turbo_map TURBO_WEB_ENABLED Turbo__Web__Enabled
  turbo_map TURBO_WEB_URL Turbo__Web__Url
  turbo_map TURBO_WEB_SITE_URL Turbo__Web__SiteUrl
  turbo_map TURBO_WEB_HOTEL_NAME Turbo__Web__HotelName
  turbo_map TURBO_WEB_CLIENT_URL Turbo__Web__ClientUrl
  turbo_map TURBO_WEB_AVATAR_IMAGE_URL Turbo__Web__AvatarImageUrl
  turbo_map TURBO_WEB_REGISTRATION_OPEN Turbo__Web__RegistrationOpen
  turbo_map TURBO_DISCORD_CLIENT_ID Turbo__Web__Discord__ClientId
  turbo_map TURBO_DISCORD_CLIENT_SECRET Turbo__Web__Discord__ClientSecret

  turbo_map TURBO_LOG_LEVEL Logging__LogLevel__Turbo

  # Plugins live outside the release folders so a deploy does not drop them.
  export "TURBO__Turbo__Plugin__PluginFolderPath=$TURBO_PLUGIN_PATH"
  export "TURBO__Turbo__Plugin__HotReloadEnabled=${TURBO_PLUGIN_HOT_RELOAD:-false}"

  # Daemon output goes to a log file, where colour codes are noise.
  export "TURBO__Logging__TurboConsole__UseAnsiColor=${TURBO_LOG_COLOR:-false}"
}

# Pid of the running emulator, if the daemon started one that is still alive.
turbo_running_pid() {
  local pid

  [ -f "$TURBO_PID_FILE" ] || return 1
  pid="$(cat "$TURBO_PID_FILE")"
  [ -n "$pid" ] && kill -0 "$pid" 2>/dev/null || return 1
  grep -q "Turbo.Main" "/proc/$pid/cmdline" 2>/dev/null || return 1

  printf '%s' "$pid"
}

turbo_port_open() {
  (exec 3<>"/dev/tcp/$1/$2") 2>/dev/null
}
