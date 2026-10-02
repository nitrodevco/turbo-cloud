$ErrorActionPreference = "Stop"
$composeFile = Join-Path $PSScriptRoot "compose.yaml"
docker compose --file $composeFile down
if ($LASTEXITCODE -ne 0) {
    throw "Docker Compose failed to stop the Aspire dashboard."
}
