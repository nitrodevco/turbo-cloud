#!/usr/bin/env sh
set -eu

step() {
  printf '\n==> %s\n' "$1"
}

require_file() {
  path="$1"
  description="$2"
  if [ ! -e "$path" ]; then
    printf "Missing %s at '%s'. Run this script from the repository root.\n" "$description" "$path" >&2
    exit 1
  fi
}

step "Validating repository layout"
require_file "Turbo.Cloud.sln" "solution file"
require_file ".githooks" "git hooks directory"
require_file "appsettings.json" "base appsettings file"

step "Checking .NET SDK"
if ! command -v dotnet >/dev/null 2>&1; then
  echo "dotnet is not installed. Install .NET SDK 9.x and rerun." >&2
  exit 1
fi

dotnet_version="$(dotnet --version | tr -d '\r')"
case "$dotnet_version" in
  9.*) ;;
  *)
    printf ".NET SDK 9.x is required. Found '%s'.\n" "$dotnet_version" >&2
    exit 1
    ;;
esac
printf "Using dotnet SDK %s\n" "$dotnet_version"

step "Configuring repository git hooks"
git config --local core.hooksPath .githooks

step "Ensuring local development settings exist"
if [ ! -f "appsettings.Development.json" ]; then
  cp appsettings.json appsettings.Development.json
  echo "Created appsettings.Development.json from appsettings.json"
else
  echo "appsettings.Development.json already exists"
fi

step "Running smoke build"
dotnet build Turbo.Main/Turbo.Main.csproj

step "Bootstrap complete"
echo "Next steps:"
echo "  1) Update Turbo:Database:ConnectionString in appsettings.Development.json"
echo "  2) Run: dotnet build Turbo.Main/Turbo.Main.csproj -t:TurboCloudQualityGate"
echo "  3) Run: DOTNET_ENVIRONMENT=Development dotnet run --project Turbo.Main/Turbo.Main.csproj"
echo "  4) Optional integrated core+plugin loop: sh ../turbo-sample-plugin/scripts/dev-integrated.sh"
