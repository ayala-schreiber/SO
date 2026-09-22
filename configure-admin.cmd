@echo off
cd /d "%~dp0so.api"
dotnet run --project "..\tools\So.AdminSetup" -- .
pause
