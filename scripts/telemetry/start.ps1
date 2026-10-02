$ErrorActionPreference = "Stop"
$composeFile = Join-Path $PSScriptRoot "compose.yaml"
docker compose --file $composeFile up --detach
if ($LASTEXITCODE -ne 0) {
    throw "Docker Compose failed to start the Aspire dashboard."
}

Write-Host "Aspire dashboard: http://localhost:18888"
Write-Host "Copy the browser login token from: docker logs turbo-aspire-dashboard"
