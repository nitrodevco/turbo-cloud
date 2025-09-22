@echo off
setlocal
set DOTNET_ENVIRONMENT=Production
set APPDIR=%~dp0
pushd "%APPDIR%"
rem pass through args to your app
dotnet "%APPDIR%Turbo.Main.dll" %*
echo.
echo Application exited with error code %ERRORLEVEL%
pause