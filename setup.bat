@echo off
setlocal

if exist "%~dp0setup.ps1" (
    powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0setup.ps1"
) else (
    powershell -NoProfile -ExecutionPolicy Bypass -Command "Invoke-WebRequest 'https://raw.githubusercontent.com/avukelic-aiot/1wireHID/main/setup.ps1' -OutFile \"%TEMP%\1wireHID-setup.ps1\"; powershell -NoProfile -ExecutionPolicy Bypass -File \"%TEMP%\1wireHID-setup.ps1\""
)

endlocal
