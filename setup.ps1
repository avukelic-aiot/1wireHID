Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$RepoOwner = 'avukelic-aiot'
$RepoName = '1wireHID'
$RawSetupUrl = "https://raw.githubusercontent.com/$RepoOwner/$RepoName/master/setup.ps1"
$ReleaseApiUrl = "https://api.github.com/repos/$RepoOwner/$RepoName/releases/latest"
$UserAgent = '1wireHID-Setup'
$DefaultInstallPath = 'C:\Program Files\1wireHID'
$TempRoot = Join-Path $env:TEMP '1wireHID-setup'
$RunId = "$(Get-Date -Format 'yyyyMMdd-HHmmss-fff')-$PID"
$LogFile = Join-Path $TempRoot "setup-$RunId.log"
$TranscriptFile = Join-Path $TempRoot "setup-$RunId.transcript.txt"
$DriverAssetName = 'OneWireDrivers_x64.msi'
$AppAssetName = '1wireHID-win-x64.zip'

function Write-Info([string]$Message) { Write-Host "[INFO] $Message" -ForegroundColor Cyan }
function Write-Warn([string]$Message) { Write-Host "[WARN] $Message" -ForegroundColor Yellow }
function Write-Err([string]$Message) { Write-Host "[ERROR] $Message" -ForegroundColor Red }

function Write-Log([string]$Message) {
    Add-Content -LiteralPath $LogFile -Value "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') $Message"
}

function Write-Step([string]$Message) {
    Write-Info $Message
    Write-Log $Message
}

function Test-Admin {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = [Security.Principal.WindowsPrincipal]::new($identity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Ensure-Admin {
    if (Test-Admin) { return }

    Write-Step 'Requesting administrator privileges...'
    $self = Join-Path $TempRoot 'setup.ps1'
    New-Item -ItemType Directory -Path $TempRoot -Force | Out-Null
    Invoke-WebRequest -Uri $RawSetupUrl -OutFile $self -Headers @{ 'User-Agent' = $UserAgent } -UseBasicParsing
    Start-Process -FilePath 'powershell.exe' -Verb RunAs -ArgumentList @(
        '-NoProfile',
        '-ExecutionPolicy', 'Bypass',
        '-File', $self
    ) | Out-Null
    exit
}

function Invoke-GitHubJson([string]$Uri) {
    return Invoke-RestMethod -Uri $Uri -Headers @{ 'User-Agent' = $UserAgent; 'Accept' = 'application/vnd.github+json' }
}

function Ensure-Directory([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) {
        New-Item -ItemType Directory -Path $Path -Force | Out-Null
    }
}

function Get-AssetUrl($Release, [string]$AssetName) {
    $asset = $Release.assets | Where-Object { $_.name -eq $AssetName } | Select-Object -First 1
    if (-not $asset) {
        throw "Release asset '$AssetName' not found."
    }
    return $asset.browser_download_url
}

function Download-File([string]$Uri, [string]$Destination) {
    Write-Step "Downloading $(Split-Path -Leaf $Destination)..."
    Invoke-WebRequest -Uri $Uri -Headers @{ 'User-Agent' = $UserAgent } -OutFile $Destination -UseBasicParsing
}

function Get-LocalDriverMsi {
    if ($PSScriptRoot) {
        $candidate = Join-Path -Path ([string]$PSScriptRoot) -ChildPath '.1Wire\OneWireDrivers_x64.msi'
        if (Test-Path -LiteralPath $candidate) {
            return $candidate
        }
    }

    $currentPath = (Get-Location).Path
    $candidate = Join-Path -Path $currentPath -ChildPath '.1Wire\OneWireDrivers_x64.msi'
    if (Test-Path -LiteralPath $candidate) {
        return $candidate
    }

    return $null
}

function Test-DriverInstalled {
    $paths = @(
        Join-Path $env:WINDIR 'System32\IBFS64.dll',
        Join-Path $env:WINDIR 'SysWOW64\IBFS64.dll'
    )
    foreach ($p in $paths) {
        if (Test-Path -LiteralPath $p) { return $true }
    }
    return $false
}

function Install-Driver([string]$MsiPath) {
    Write-Step 'Installing 1-Wire driver package...'
    $proc = Start-Process -FilePath 'msiexec.exe' -ArgumentList @('/i', $MsiPath, '/qn', '/norestart') -Wait -PassThru
    if ($proc.ExitCode -ne 0) {
        throw "1-Wire driver MSI installation failed with exit code $($proc.ExitCode)."
    }
    if (-not (Test-DriverInstalled)) {
        throw '1-Wire driver installation finished, but IBFS64.dll was not found.'
    }
}

function Ensure-DriverInstalled($Release) {
    if (Test-DriverInstalled) {
        Write-Info '1-Wire driver already installed.'
        return
    }

    $localMsi = Get-LocalDriverMsi
    $msiPath = $null

    if ($localMsi) {
        Write-Step "Using local driver MSI: $localMsi"
        $msiPath = $localMsi
    }
    else {
        $driverUrl = Get-AssetUrl -Release $Release -AssetName $DriverAssetName
        $msiPath = Join-Path $TempRoot $DriverAssetName
        Download-File -Uri $driverUrl -Destination $msiPath
    }

    Install-Driver -MsiPath $msiPath
}

function Test-DotNetRuntime8 {
    $dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
    if (-not $dotnet) { return $false }

    $runtimes = & $dotnet.Source --list-runtimes 2>$null
    return [bool]($runtimes | Select-String -Pattern '^Microsoft\.NETCore\.App\s+8\.')
}

function Ensure-DotNetRuntime {
    if (Test-DotNetRuntime8) {
        Write-Info '.NET 8 runtime already installed.'
        return
    }

    Write-Step '.NET 8 runtime not found, installing runtime only...'
    Ensure-Directory $TempRoot

    $runtimeInstallDir = Join-Path $env:ProgramFiles 'dotnet'
    $installScript = Join-Path $TempRoot 'dotnet-install.ps1'
    Download-File -Uri 'https://dot.net/v1/dotnet-install.ps1' -Destination $installScript
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $installScript -Runtime dotnet -Channel 8.0 -InstallDir $runtimeInstallDir | Out-Host

    $env:DOTNET_ROOT = $runtimeInstallDir
    if ($env:Path -notlike "*$runtimeInstallDir*") {
        $env:Path = "$runtimeInstallDir;$env:Path"
    }
    [Environment]::SetEnvironmentVariable('DOTNET_ROOT', $runtimeInstallDir, 'Machine')
    $machinePath = [Environment]::GetEnvironmentVariable('Path', 'Machine')
    if ($machinePath -notlike "*$runtimeInstallDir*") {
        [Environment]::SetEnvironmentVariable('Path', "$runtimeInstallDir;$machinePath", 'Machine')
    }

    if (-not (Test-DotNetRuntime8)) {
        throw '.NET 8 runtime installation failed.'
    }

    Write-Step '.NET 8 runtime installed successfully.'
}

function Install-App([string]$InstallPath, $Release) {
    Ensure-Directory $InstallPath

    $zipUrl = Get-AssetUrl -Release $Release -AssetName $AppAssetName
    $zipPath = Join-Path $TempRoot $AppAssetName
    Download-File -Uri $zipUrl -Destination $zipPath

    $extractPath = Join-Path $TempRoot 'app'
    if (Test-Path -LiteralPath $extractPath) {
        Remove-Item -LiteralPath $extractPath -Recurse -Force
    }
    Ensure-Directory $extractPath
    Expand-Archive -LiteralPath $zipPath -DestinationPath $extractPath -Force

    Get-ChildItem -LiteralPath $extractPath -Force | ForEach-Object {
        Copy-Item -LiteralPath $_.FullName -Destination $InstallPath -Recurse -Force
    }

    $versionFile = Join-Path $InstallPath 'version.ini'
    @(
        "Version=$($Release.tag_name.TrimStart('v'))"
        "InstallPath=$InstallPath"
        "BuildDate=$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
        "GitCommit=$($Release.target_commitish)"
        "ReleaseUrl=$($Release.html_url)"
    ) | Set-Content -LiteralPath $versionFile -Encoding ASCII
}

function Install-StartupShortcut([string]$InstallPath) {
    $shell = New-Object -ComObject WScript.Shell
    $startupDir = [Environment]::GetFolderPath('Startup')
    $shortcutPath = Join-Path $startupDir '1wireHID.lnk'
    $target = Join-Path $InstallPath '1wireHID.exe'

    $shortcut = $shell.CreateShortcut($shortcutPath)
    $shortcut.TargetPath = $target
    $shortcut.Arguments = '-tray'
    $shortcut.WorkingDirectory = $InstallPath
    $shortcut.Description = '1wireHID tray app'
    $shortcut.Save()

    return $shortcutPath
}

function Select-InstallPath {
    $answer = Read-Host "Install path [default: $DefaultInstallPath]"
    if ([string]::IsNullOrWhiteSpace($answer)) { return $DefaultInstallPath }
    return $answer.Trim('"')
}

function Main {
    Ensure-Admin
    Ensure-Directory $TempRoot

    Write-Host '============================================================'
    Write-Host '1wireHID Installer'
    Write-Host '============================================================'

    $installPath = Select-InstallPath
    Write-Step "Install path: $installPath"

    $release = Invoke-GitHubJson $ReleaseApiUrl
    Write-Step "Latest release on GitHub: $($release.tag_name)"

    Ensure-DriverInstalled -Release $release
    Write-Step '1-Wire driver ready.'

    Ensure-DotNetRuntime

    Install-App -InstallPath $installPath -Release $release
    $shortcut = Install-StartupShortcut -InstallPath $installPath

    Write-Step 'Starting tray app now...'
    Start-Process -FilePath (Join-Path $installPath '1wireHID.exe') -ArgumentList '-tray' -WorkingDirectory $installPath | Out-Null

    Write-Host ''
    Write-Host '============================================================'
    Write-Host 'Installation complete.'
    Write-Host '============================================================'
    Write-Host "Installed to: $installPath"
    Write-Host "Startup shortcut: $shortcut"
    Write-Host "Version: $($release.tag_name)"
    Write-Host 'Tray app started.'
    Write-Host ''
    Write-Log 'Installation complete.'
    Read-Host 'Press Enter to close installer'
}

try {
    Ensure-Directory $TempRoot
    Start-Transcript -LiteralPath $TranscriptFile -Append | Out-Null
    Write-Log 'Installer started.'
    Main
}
catch {
    Write-Err $_.Exception.Message
    Write-Log "ERROR: $($_.Exception.ToString())"
    Write-Host ''
    Read-Host "Installer failed. Log saved to $LogFile. Press Enter to close"
    exit 1
}
finally {
    try { Stop-Transcript | Out-Null } catch { }
}
