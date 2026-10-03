#!/usr/bin/env bash
# Daemon entry point for Turbo. Ploi daemon command:
#
#   bash /home/ploi/<site>/scripts/ploi/start.sh
#
# Runs the release .deploy/current points at, with the .env settings applied. The pid is
# recorded so deploy.sh can restart the emulator after switching releases.
set -euo pipefail

# Supervisor does not always set HOME for the user it runs a program as, and .NET wants it.
HOME="$(getent passwd "$(id -un)" | cut -d: -f6)"
export HOME

# shellcheck source=lib.sh
. "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

turbo_load_env
turbo_find_dotnet
turbo_export_app_env

current="$TURBO_DEPLOY_ROOT/current"
[ -f "$current/Turbo.Main.dll" ] || turbo_fail "No release at $current. Run a deploy first."

mkdir -p "$TURBO_PLUGIN_PATH"

# The host's content root is the working directory, where the release's appsettings live.
# Resolving the link once keeps this process on its release when a deploy moves current.
cd "$(readlink -f "$current")"

echo "$$" >"$TURBO_PID_FILE"

exec "$TURBO_DOTNET" Turbo.Main.dll
