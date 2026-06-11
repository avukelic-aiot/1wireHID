# Agents.md - 1wireHID Development Notes

## Secrets Configuration

All secrets (GitHub tokens, API keys, etc.) are stored in `PRIVATE.md` - this file is NOT committed to git.

**GitHub Account: avukelic-aiot**

Always verify the correct account is active before pushing:
```batch
gh auth status
```

If a different account is active, switch to avukelic-aiot:
```batch
gh auth switch -h github.com -u avukelic-aiot
```

To update GitHub credentials:
1. Edit `PRIVATE.md` with your GitHub token
2. Verify account is active: `gh auth status`
3. Use `github.com` authentication when pushing

## Project Overview

1wireHID is a .NET 8 tray application that reads iButton/1-Wire devices and emits their ROM as keyboard input (human-readable hex string followed by Enter).

## Key Technical Decisions

### TMEX API vs Compact.NET API

The SDK provides two APIs:
1. **TMEX API** - Low-level native DLL interface (IBFS32.dll / IBFS64.dll)
2. **Compact.NET API** - Higher-level managed wrapper (OneWireLinkLayer.dll)

For Windows x64 deployment, TMEX API was chosen because:
- Runtime-only deployment with a small compiled release
- Direct P/Invoke to IBFS64.dll provides full control
- Compact.NET is deprecated (Microsoft discontinued .NET CF support)

### Adapter Types

| Port Type | Value | Adapter |
|-----------|-------|---------|
| USB | 6 | DS9490R, DS9490B |
| Serial | 5 | DS9097U, DS9480 |
| PassiveSerial | 1 | DS9097 (legacy) |
| Parallel | 2 | DS1410E (legacy) |

Used USB (type 6) as default for DS9490R adapter.

### Key Files

- `TMEX64.cs` - All P/Invoke declarations for IBFS64.dll
- `KeyboardSimulator.cs` - SendInput wrapper for keyboard emulation
- `Program.cs` - Main logic + tray host and console debug mode

### IBFS64.dll Location

The driver DLL must be accessible:
- Same folder as executable
- Or in Windows\System32

The 1-Wire drivers come as an MSI installer stored at `.1Wire/OneWireDrivers_x64.msi`.

Local builds copy the MSI into `bin/Release/net8.0-windows/win-x64/` and `publish/` so manual installers can pick it up.

### Keyboard Input Implementation

Uses Windows `SendInput` API with `KEYEVENTF_UNICODE` mode:
- Sends hex characters (uppercase, no separators) as Unicode keyboard events
- Followed by VK_RETURN for Enter key
- No external dependencies (not even Win32 SendKeys)

### ROM Format

1-Wire ROM is 8 bytes: [Family Code][Serial Number x6][CRC]

Displayed as 16 hex characters: FamilyCode + SerialNumber (reversed byte order in display)

Example: Bytes `01 23 45 67 89 AB CD EF` displays as `EFCDAB8967452301`

## Build Instructions

```powershell
dotnet build 1wireHID.csproj -c Release -r win-x64
```

Publish output:
```powershell
dotnet publish 1wireHID.csproj -c Release -r win-x64
```

## Installation Flow

Default install entrypoint is the PowerShell bootstrapper:
```powershell
powershell -NoProfile -ExecutionPolicy Bypass -Command "iwr 'https://raw.githubusercontent.com/avukelic-aiot/1wireHID/master/setup.ps1' | iex"
```

Bootstrapper responsibilities:
- install 1-Wire driver MSI if missing
- install .NET 8 runtime only if missing
- download the latest compiled release
- create startup shortcut
- launch tray app

## SDK Reference

Documentation: `.1Wire/onewiresdkver410/Docs/1-Wire_SDK_Help.html`

Key TMEX functions used:
- `TMExtendedStartSession` - Open session with adapter
- `TMSetup` - Verify adapter communication
- `TMTouchReset` - Send 1-Wire reset pulse
- `TMSearch` - Find devices on network
- `TMRom` - Read current device ROM
- `TMEndSession` / `TMClose` - Close session

## Debugging

For verbose output showing raw driver responses:
```batch
1wireHID.exe -v
```

For testing without hardware:
```batch
1wireHID.exe -rom 1800000012345678
```

Tray mode is the default runtime mode. `-console` is only for diagnostics.

## ROM Detection Logic

The application detects iButton touches by monitoring the reset response. When a device is touched:
1. Reset returns Presence (1) or Alarm (2) instead of NoPresence (0)
2. Search is performed to get the ROM
3. Only new (different) ROMs trigger keyboard output
4. This prevents duplicate sends on continuous touch
