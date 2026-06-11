@echo off
setlocal EnableDelayedExpansion

REM ============================================================
REM 1wireHID Setup Script v0.1.0
REM ============================================================

set "SCRIPT_VERSION=0.1.3"
set "REPO_URL=https://github.com/avukelic-aiot/1wireHID"
set "GITHUB_API=https://api.github.com/repos/avukelic-aiot/1wireHID/releases/latest"

echo ============================================================
echo 1wireHID Setup v%SCRIPT_VERSION%
echo ============================================================
echo.

REM ============================================================
REM Check for Admin privileges
REM ============================================================
net session >nul 2>&1
if %errorLevel% neq 0 (
    echo [INFO] This script requires administrator privileges.
    echo [INFO] Requesting elevation...
    echo.
    powershell -Command "Start-Process cmd -ArgumentList '/c cd /d %CD% ^&^& %~nx0 %*' -Verb RunAs"
    exit /b
)

echo [OK] Running with administrator privileges.
echo.

REM ============================================================
REM Check if .NET 8 SDK is installed
REM ============================================================
echo [INFO] Checking for .NET 8 SDK...

dotnet --version >nul 2>&1
if %errorLevel% neq 0 (
    echo [WARN] .NET 8 SDK not found!
    echo [INFO] Would you like to download and install it now?
    echo        (Required for building from source)
    echo.
    choice /C YN /M "Download .NET 8 SDK installer? (Y/N): "
    if errorlevel 2 (
        echo [INFO] Skipping .NET installation.
        echo [WARN] Build will fail without .NET 8 SDK.
    ) else (
        echo [INFO] Downloading .NET 8 SDK installer...
        powershell -Command "[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12; Invoke-WebRequest -Uri 'https://dot.net/v1/dotnet-install.ps1' -OutFile '%TEMP%\dotnet-install.ps1'"
        echo [INFO] Installing .NET 8 SDK (this may take a few minutes)...
        powershell -ExecutionPolicy Bypass -File "%TEMP%\dotnet-install.ps1" -Channel 8.0
        del "%TEMP%\dotnet-install.ps1" 2>nul

        echo [INFO] Verifying .NET installation...
        dotnet --version
        if errorlevel 1 (
            echo [ERROR] .NET installation failed. Please install manually from:
            echo        https://dotnet.microsoft.com/download/dotnet/8.0
            echo.
            echo Press any key to close...
            pause >nul
            exit /b 1
        )
    )
)

REM Check dotnet is now available
dotnet --version >nul 2>&1
if %errorLevel% neq 0 (
    echo [ERROR] .NET 8 SDK is not available. Cannot proceed with build.
    echo.
    echo Press any key to close...
    pause >nul
    exit /b 1
)

echo [OK] .NET 8 SDK found: 
dotnet --version
echo.

REM ============================================================
REM Check for Updates (if git remote is configured)
REM ============================================================
echo [INFO] Checking for updates...

REM Check if this is a git repo with origin remote
git remote get-url origin >nul 2>&1
if %errorLevel% equ 0 (
    set "REMOTE_URL=$(git remote get-url origin)"
    echo [INFO] Remote URL: !REMOTE_URL!

    REM Fetch latest tag info from GitHub
    for /f "tokens=*" %%i in ('git ls-remote --tags origin 2^>nul') do (
        set "LATEST_TAG=%%i"
    )

    REM Extract version from latest tag
    for /f "tokens=2 delims=refs/tags/v" %%v in ('git tag -l "v*" --list 2^>nul ^| sort -V ^| tail -1') do (
        set "LATEST_TAG_VER=%%v"
    )

    if defined LATEST_TAG_VER (
        echo [INFO] Latest version on GitHub: v!LATEST_TAG_VER!
        if "!LATEST_TAG_VER!" gtr "%SCRIPT_VERSION%" (
            echo [INFO] A newer version is available: v!LATEST_TAG_VER!
            echo [INFO] Current version: v%SCRIPT_VERSION%
            echo.
            echo Would you like to download the latest release instead?
            echo    https://github.com/avukelic-aiot/1wireHID/releases
            echo.
            choice /C YN /M "Continue with current build? (Y/N): "
            if errorlevel 2 (
                echo [INFO] Please download the latest release manually and run this script again.
                pause
                exit /b 0
            )
        )
    )
) else (
    echo [WARN] Not a git repository or no remote configured.
    echo [INFO] Skipping version check.
)
echo.

REM ============================================================
REM Ask for installation path
REM ============================================================
echo [INFO] Installation path selection:
echo        Default: C:\Program Files\1wireHID
echo.
set /p INSTALL_PATH="Enter installation path (press Enter for default): "
if "!INSTALL_PATH!"=="" set "INSTALL_PATH=C:\Program Files\1wireHID"

REM Remove quotes if present
set "INSTALL_PATH=!INSTALL_PATH:"=!"

echo [INFO] Installing to: !INSTALL_PATH!
echo.

REM ============================================================
REM Create installation directory if it doesn't exist
REM ============================================================
if not exist "!INSTALL_PATH!" (
    echo [INFO] Creating installation directory...
    mkdir "!INSTALL_PATH!" 2>nul
    if errorlevel 1 (
        echo [ERROR] Failed to create directory: !INSTALL_PATH!
        echo [INFO] Please check permissions and try again.
        echo.
        echo Press any key to close...
        pause >nul
        exit /b 1
    )
)

REM ============================================================
REM Build the application
REM ============================================================
echo [INFO] Building 1wireHID v%SCRIPT_VERSION%...
echo.

cd /d "%~dp0"

dotnet build 1wireHID.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o "!INSTALL_PATH!\build"
if errorlevel 1 (
    echo.
    echo [ERROR] Build failed!
    echo.
    echo Press any key to close...
    pause >nul
    exit /b 1
)

echo.
echo [OK] Build successful!
echo.

REM ============================================================
REM Copy files to installation directory
REM ============================================================
echo [INFO] Copying files to installation directory...

REM Copy the published executable
copy /y "!INSTALL_PATH!\build\1wireHID.exe" "!INSTALL_PATH!\1wireHID.exe" >nul

REM Copy IBFS64.dll if it exists in the source folder
if exist "IBFS64.dll" (
    copy /y "IBFS64.dll" "!INSTALL_PATH!\IBFS64.dll" >nul
    echo [INFO] Copied IBFS64.dll to installation directory.
) else (
    echo [WARN] IBFS64.dll not found in source folder.
    echo [INFO] Please copy it manually to: !INSTALL_PATH!
)

REM Copy version file
(
    echo Version=%SCRIPT_VERSION%
    echo InstallPath=!INSTALL_PATH!
    echo BuildDate=%date% %time%
    echo GitCommit=!GIT_COMMIT!
) > "!INSTALL_PATH!\version.ini"

REM Clean up build folder
rd /s /q "!INSTALL_PATH!\build" 2>nul

echo.
echo ============================================================
echo  Installation Complete!
echo ============================================================
echo.
echo Installed to: !INSTALL_PATH!
echo Executable:   !INSTALL_PATH!\1wireHID.exe
echo Version:      v%SCRIPT_VERSION%
echo.
echo To run as terminal mode:
echo   "!INSTALL_PATH!\1wireHID.exe"
echo.
echo To install as Windows Service:
echo   sc create OneWireHID binPath= "!INSTALL_PATH!\1wireHID.exe -service"
echo   sc start OneWireHID
echo.
echo NOTE: You may need to copy IBFS64.dll to the installation folder
echo       if it's not already there. Get it from the 1-Wire drivers package.
echo.
echo ============================================================
echo Press any key to close this window...
echo ============================================================
pause >nul
endlocal
exit