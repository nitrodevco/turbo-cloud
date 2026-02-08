Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Write-Step {
    param([string]$Message)
    Write-Host ""
    Write-Host "==> $Message"
}

function Require-File {
    param([string]$Path, [string]$Description)
    if (-not (Test-Path $Path)) {
        throw "Missing $Description at '$Path'. Run this script from the repository root."
    }
}

Write-Step "Validating repository layout"
Require-File "Turbo.Cloud.sln" "solution file"
Require-File ".githooks" "git hooks directory"
Require-File "appsettings.json" "base appsettings file"

Write-Step "Checking .NET SDK"
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw "dotnet is not installed. Install .NET SDK 9.x and rerun."
}

$dotnetVersion = (dotnet --version).Trim()
if (-not $dotnetVersion.StartsWith("9.")) {
    throw ".NET SDK 9.x is required. Found '$dotnetVersion'."
}
Write-Host "Using dotnet SDK $dotnetVersion"

Write-Step "Configuring repository git hooks"
git config --local core.hooksPath .githooks

Write-Step "Ensuring local development settings exist"
if (-not (Test-Path "appsettings.Development.json")) {
    Copy-Item "appsettings.json" "appsettings.Development.json"
    Write-Host "Created appsettings.Development.json from appsettings.json"
} else {
    Write-Host "appsettings.Development.json already exists"
}

Write-Step "Running smoke build"
dotnet build Turbo.Main/Turbo.Main.csproj

Write-Step "Bootstrap complete"
Write-Host "Next steps:"
Write-Host "  1) Update Turbo:Database:ConnectionString in appsettings.Development.json"
Write-Host "  2) Run: dotnet build Turbo.Main/Turbo.Main.csproj -t:TurboCloudQualityGate"
Write-Host "  3) Run: `$env:DOTNET_ENVIRONMENT='Development'; dotnet run --project Turbo.Main/Turbo.Main.csproj"
Write-Host "  4) Optional integrated core+plugin loop: pwsh -File ../turbo-sample-plugin/scripts/dev-integrated.ps1"
