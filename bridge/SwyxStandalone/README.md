# SwyxStandalone — Swyx COM Bridge without SwyxIt!

A .NET 8 console application that connects directly to the Swyx Server via the
Client Line Manager COM API (`CLMgr.ClientLineMgr`) **without** requiring SwyxIt!.exe
to be running. It speaks the same JSON-RPC stdin/stdout protocol as the existing
`SwyxBridge` project.

## Prerequisites

- Windows (x64)
- .NET 8 Runtime
- Swyx Client SDK installed (provides the `CLMgr.ClientLineMgr` COM object)
- Network access to the Swyx Server

> **SDK vs SwyxIt!**  
> The Swyx Client SDK is a separately installable component. The `Swyx.Client.ClmgrAPI`
> NuGet package provides the interop assemblies; the actual COM server DLLs are installed
> by the SDK setup (or by a SwyxIt! installation).

## Build

```powershell
dotnet build -c Release
```

Output: `bin/Release/net8.0-windows/SwyxStandalone.exe`

## Usage

### Option A — Command-line arguments (auto-login at startup)

```powershell
SwyxStandalone.exe `
    --server   my-swyx-server.example.com `
    --user     alice `
    --password s3cr3t `
    --domain   CORP `
    --auth-mode 1
```

| Argument | Description |
|---|---|
| `--server` | Hostname or IP of the Swyx Server |
| `--user` / `--username` | Swyx username |
| `--password` / `--pass` | Password |
| `--domain` | Windows domain (optional, leave empty for local accounts) |
| `--auth-mode` | `0`=None, `1`=Password (default), `2`=WebServiceTrusted |

### Option B — JSON-RPC `login` method (runtime login via stdin)

Start the bridge without credentials, then send a `login` request:

```json
{"jsonrpc":"2.0","id":1,"method":"login","params":{"server":"my-swyx","username":"alice","password":"s3cr3t","domain":"","authMode":1}}
```

## JSON-RPC Protocol

### Requests (stdin → bridge)

| Method | Params | Description |
|---|---|---|
| `login` | `{server, username, password, domain?, authMode?}` | Login to Swyx Server |
| `logout` | — | Logout and disconnect |
| `getStatus` | — | Connection/login status |
| `ping` | — | Liveness check |
| `setLines` | `{count}` | Set number of lines (1–16) |
| `dial` | `{number}` | Dial a number |
| `answer` | `{lineId}` | Answer incoming call |
| `hangup` | `{lineId?}` | Hangup (optional lineId) |
| `hold` | `{lineId}` | Hold a line |
| `activate` | `{lineId}` | Activate a held line |
| `transfer` | `{lineId, target}` | Transfer call |
| `getLines` | — | Get all line states |
| `getLineState` | `{lineId}` | Get single line state |
| `getLineDetails` | `{lineId}` | Get line details |
| `getPresence` | — | Get own presence status |
| `setPresence` | `{status}` | Set presence: `Available`, `Away`, `DND`, `Offline` |
| `getColleaguePresence` | — | Get colleague presence list |
| `getAudioDevices` | — | Get audio device names |
| `setAudioDevice` | `{deviceType, playback, capture?}` | Set audio device (`handsfree`, `headset`, `speaker`) |
| `getVolume` | — | Get volume levels |
| `setVolume` | `{deviceType, volume}` | Set volume 0–100 (`handsfree`, `headset`, `ring`) |

### Events (bridge → stdout)

| Event | Payload | Trigger |
|---|---|---|
| `bridgeState` | `{state, server?, username?, error?}` | Login/logout/error |
| `loginSucceeded` | `{server, username}` | Server confirmed login |
| `loginFailed` | `{server, username, errorCode}` | Login rejected |
| `logoutSucceeded` | — | Logout confirmed |
| `lineStateChanged` | `{lines:[{id,state,callerName,callerNumber,isSelected}]}` | Any line change |
| `presenceNotification` | `{msg, param}` | Presence changed |
| `voicemailNotification` | `{msg, param}` | New voicemail |
| `configChanged` | `{msg, param}` | Server config changed |
| `heartbeat` | `{uptime}` | Every 5 seconds |

## Authentication Modes (`authMode`)

| Value | Name | Description |
|---|---|---|
| `0` | None | Anonymous / SwyxIt!Now (server must allow it) |
| `1` | Password | Standard username + password login |
| `2` | WebServiceTrusted | Single Sign-On (specific server configurations) |

## Registry: EnablePowerDialMode

For standalone mode (no SwyxIt! running), the following registry key controls
whether the CLMgr operates in "power dial" mode (no UI, no audio mixing with SwyxIt!):

```
HKEY_LOCAL_MACHINE\SOFTWARE\Swyx\Client Line Manager\CurrentVersion\Options
Value: EnablePowerDialMode  (DWORD)
  0 = Standard mode (may show SwyxIt! UI elements)
  1 = Power dial / headless mode (recommended for standalone)
```

Create/set with PowerShell:
```powershell
$path = "HKLM:\SOFTWARE\Swyx\Client Line Manager\CurrentVersion\Options"
if (-not (Test-Path $path)) { New-Item -Path $path -Force }
Set-ItemProperty -Path $path -Name "EnablePowerDialMode" -Value 1 -Type DWord
```

## Architecture

```
stdin  ──► [JsonRpcServer / Background Thread]
                  │  Post()
                  ▼
           [STA Main Thread / Message Pump]
                  │
          ┌───────┼──────────────────────┐
          │       │                      │
    SystemHandler CallHandler     PresenceHandler
          │                      AudioHandler
          ▼
    StandaloneConnector  ◄──►  CLMgr COM Object
          │
     EventSink (PubOnLineMgrNotification)
          │
          ▼
       stdout  ──► JSON-RPC Events
```

**Why `[STAThread]` + `Application.Run()`?**  
The Swyx COM object is an STA (Single-Threaded Apartment) COM server. All COM calls
and event callbacks must happen on the same thread that created the COM object. The
WinForms message pump (`Application.Run`) provides the required STA message dispatch.
All JSON-RPC requests are marshalled from the background stdin-reader thread to the
STA thread via `SynchronizationContext.Post()`.

**Why static fields for COM objects?**  
.NET's garbage collector may collect objects that appear unreferenced even though
COM still holds a reference. Keeping static references prevents the GC from collecting
the EventSink delegate and COM wrapper prematurely, which would cause sporadic crashes.

## Differences from SwyxBridge

| | SwyxBridge | SwyxStandalone |
|---|---|---|
| SwyxIt! required | Yes (must be running) | **No** |
| Login method | Automatic (connects to running instance) | `RegisterUserEx()` / JSON-RPC `login` |
| Window suppression | Yes (hides SwyxIt! popup) | Not needed |
| Audio control | Limited (SwyxIt! manages audio) | Full (`DispHandsfreeDevice` etc.) |
