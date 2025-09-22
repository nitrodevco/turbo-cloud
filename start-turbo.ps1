$ErrorActionPreference = 'Stop'
$PSScriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$env:DOTNET_ENVIRONMENT = "Production"
Push-Location $PSScriptRoot
dotnet "$PSScriptRoot/Turbo.Main.dll" @args
Pop-Location