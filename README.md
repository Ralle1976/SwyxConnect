# SwyxConnect

> **Modern desktop softphone for Swyx/Enreach telephony — fully standalone, no SwyxIt! required.**

[![Version: 1.6.0](https://img.shields.io/badge/Version-1.6.0-brightgreen.svg)]()
[![Platform: Windows x86](https://img.shields.io/badge/Platform-Windows%20x86-lightgrey.svg)]()
[![Swyx: v14.x](https://img.shields.io/badge/Swyx-v14.x-orange.svg)]()
[![.NET: 8.0](https://img.shields.io/badge/.NET-8.0-purple.svg)]()
[![License: Proprietary](https://img.shields.io/badge/License-Proprietary-blue.svg)]()

---

## What is SwyxConnect?

SwyxConnect is a custom softphone client that replaces the original SwyxIt! application. It connects directly to the Swyx Server via the Client Line Manager (CLMgr) COM API and ComSocket (SignalR), providing a modern Electron-based UI for all telephony functions.

**Key differentiator:** SwyxConnect runs **completely standalone** — it builds its own TLS tunnel to the Swyx Server via `RegisterUserConnector4UC`, initializes audio devices directly via COM, and does not require SwyxIt! to be running at all.

### Features

- **Full Telephony:** Dial, answer, hold, transfer, conference, DTMF, recording
- **Complete Colleague Presence:** All colleagues from the company phonebook with live status (Available, Busy, Away, DND, Offline)
- **Detailed Call Journal:** Incoming, outgoing, and missed calls with duration, date, and contact name
- **Voicemail:** Visual voicemail with message count and remote inquiry
- **Contacts:** Phonebook search and speed dial management
- **Call Forwarding:** Configure unconditional, busy, and no-reply forwarding rules
- **Audio Configuration:** Headset/speakerphone mode, microphone/speaker volume, mute
- **Modern UI:** German interface, dark mode, system tray integration
- **Real-time Events:** COM event subscription (PubOnLineMgrNotification + DispOnLineMgrNotification)
- **ComSocket Integration:** SignalR hub provides all-colleague phonebook, call journal, and push events

---

## Architecture

```
Electron App (Node.js + React 19 + TypeScript + Tailwind v4)
  Main Process ←→ Preload (contextBridge) ←→ Renderer (Zustand stores)
       │
       │ Reads .env (SWYX_USERNAME, SWYX_PASSWORD, SWYX_SERVER, SWYX_PUBLIC_SERVER)
       │ stdin/stdout (JSON-RPC 2.0, newline-delimited)
       ▼
  C# Bridge — SwyxMessenger.exe (.NET 8, x86, UseAppHost=true)
  ├── COM Interop → CLMgr.exe (32-bit COM Server)
  │   ├── RegisterUserConnector4UC → TLS Tunnel to public Swyx Server
  │   ├── DispClientConfig → LoginDeviceType = 0 (standalone client)
  │   ├── ApplyAudioDevices → DispHandsfreeDevice etc. (audio binding)
  │   └── Call Control, Line State, Audio, Voicemail, Forwarding
  └── ComSocket (SignalR) → SwyxItHub @ ws://localhost:PORT/swyxIt
      └── PhoneBook (all colleagues), CallJournal, live Push Events
       │
       ▼
  CLMgr.exe — Swyx Client Line Manager
  ├── Owns full telephony stack (SIP, RTP, TLS Tunnel, Audio Plugins)
  ├── AudioDevicePluginLib → GenericDevicePlugin.dll → AudioVolumeControl.dll → DirectSound
  └── No SwyxIt! required — SwyxConnect is the only client
```

### Connection Modes

| Mode | When | How | SwyxIt! needed? |
|---|---|---|---|
| **Standalone (v1.6.0 — default)** | `.env` present with credentials | `RegisterUserConnector4UC` + `ApplyAudioDevices` via late binding | ❌ **No!** |
| **HIDE (v1.4.0 fallback)** | No `.env` | SwyxIt! hidden (SW_HIDE), bridge Auto-Attaches | ✅ But invisible |

### Why Hybrid COM + ComSocket?

| Protocol | Strength | Use |
|---|---|---|
| **COM** | Reliable call control, line state, audio | Login, Dial, Answer, Hold, Transfer, Audio devices |
| **ComSocket** | Rich data, all colleagues, real-time push | PhoneBook (all entries), CallJournal, Presence updates |

COM alone only exposes speed-dial contacts (6-10 entries). ComSocket delivers the **complete company phonebook** with live presence. The bridge combines both for maximum coverage.

---

## Requirements

- **Windows 10/11** (x86 or x64, runs as 32-bit)
- **.NET 8 Desktop Runtime (x86)** — [download](https://dotnet.microsoft.com/download/dotnet/8.0)
- **Swyx Client SDK** installed (provides CLMgr.exe and COM components)
- **SwyxIt! installed** (provides CLMgr.exe, Plugins, and Interop DLLs — but does **not** need to be running)

---

## Download & Install

### Option 1: Installer (recommended)

1. Download `SwyxConnect-Setup-1.6.0.exe` from the [latest release](https://github.com/Ralle1976/SwyxConnect/releases/latest)
2. Run the installer (administrator rights recommended)
3. Create a `.env` file in `%APPDATA%\SwyxConnect\` with your credentials (see below)
4. Launch SwyxConnect — it connects via RC Tunnel, no SwyxIt! needed

### Option 2: Build from Source

```bash
git clone https://github.com/Ralle1976/SwyxConnect.git
cd SwyxConnect
npm install

# Build C# Bridge
powershell -ExecutionPolicy Bypass -File scripts/build-bridge.ps1

# Build Electron app
npx electron-vite build

# Run
npx electron out/main/index.js

# Create installer
npx electron-builder --win --ia32
```

---

## Configuration (.env)

Create a `.env` file in one of these locations:
- `%APPDATA%\SwyxConnect\.env` (installed app — recommended, no admin needed)
- Project root (dev mode)
- Next to the executable (packaged)

```ini
# Swyx Server Login (CLMgr-only mode — standalone without SwyxIt!)
SWYX_USERNAME=your_username
SWYX_PASSWORD=your_password
SWYX_SERVER=192.168.x.x
SWYX_PUBLIC_SERVER=yourserver.example.com:15021

# Optional
SWYX_BACKUP_SERVER=
SWYX_PUBLIC_BACKUP_SERVER=
SWYX_AUTH_MODE=1
```

**Where to find these values:**
- Check SwyxIt!'s registry: `HKCU\SOFTWARE\Swyx\SwyxIt!\CurrentVersion\Options`
  - `PbxServer` → `SWYX_SERVER` (internal IP)
  - `PublicServerName` → `SWYX_PUBLIC_SERVER` (public hostname:port)
- `SWYX_USERNAME` / `SWYX_PASSWORD` are your Swyx PBX credentials

> ⚠️ **Security:** The `.env` file is listed in `.gitignore` and never committed. Keep it secure.

---

## Usage

### Phone View (`/`)
- Dialpad with trunk prefix support
- Line status (Line 1/2 with state)
- Call controls: Dial, Hangup, Hold, Transfer, Conference, DTMF, Recording

### Contacts (`/contacts`)
- Search across company phonebook
- Speed-dial management
- Click-to-dial from any contact

### Call History (`/history`)
- **All / Incoming / Outgoing / Missed** tabs
- 20 most recent calls with duration, timestamp, contact name
- Redial or callback directly from history

### Presence (`/presence`)
- **All colleagues** from company phonebook (not just speed dials)
- Live presence status: Available, Busy, Away, DND, Offline
- Search/filter colleagues
- Click-to-dial
- Set your own status (Available, Away, Busy, DND, Offline)

### Voicemail (`/voicemail`)
- Message list with timestamp and caller
- Playback controls
- Delete messages

### Settings (`/settings`)
- Audio mode (Headset / Speakerphone / Hands-free)
- Microphone/speaker volume
- Call forwarding rules (unconditional, busy, no-reply)
- Number of phone lines (1/2/4/8)
- Theme (light/dark)
- Trunk prefix configuration
- System tray behavior
- System info (CTI Master status, server connectivity, audio mode)

---

## Microsoft Teams Integration

Bidirectional Teams presence synchronization is a **server-side SwyxWare feature** (v14.20+). SwyxConnect does not manage Teams status directly — the Swyx Server handles the Graph API sync.

To enable Teams presence sync:
1. Configure `MsTeamsStatusSyncMode = 2` on the Swyx Server (Admin task)
2. Register an Azure AD App with `Presence.ReadWrite.All` permission
3. When you change your status in SwyxConnect, the server mirrors it to Teams

---

## Technical Details

### JSON-RPC Protocol
Newline-delimited JSON-RPC 2.0 over stdin/stdout between Electron and C# Bridge.
- **Requests:** `{jsonrpc:"2.0", id:N, method:"...", params:{...}}`
- **Responses:** `{jsonrpc:"2.0", id:N, result:{...}}`
- **Events (push):** `{method:"...", params:{...}}` (no id)

### Bridge Handlers (14+)
System, Call, Presence, Audio, Contact, History, Voicemail, Forwarding, Conference, Recording, SystemInfo, Chat, CTI, ComSocket

### ComSocket Methods (cs.*)
`cs.getPhoneBook`, `cs.getCallJournal`, `cs.searchContacts`, `cs.getSpeedDials`, `cs.getVoiceMessages`, `cs.getForwardingConfig`, `cs.getAudioModes`, `cs.getAudioVolumes`, `cs.getVersionInfo`, `cs.getStatus`, `cs.reconnect`

### ComSocket Push Events
`cs.lineStateChanged`, `cs.lineDetailsChanged`, `cs.userDataChanged`, `cs.notificationCallsChanged`

### How Standalone Audio Works (v1.6.0)

```
SwyxMessenger.exe starts
  → Creates CLMgr COM object
  → Calls DispInit (returns E_NOTIMPL — CLMgr already initialized internally)
  → Sets DispClientConfig.LoginDeviceType = 0 (tells CLMgr "I'm a phone client")
  → Reads audio device names from ClientConfig (Speakers, Microphone, Headset)
  → Sets DispHandsfreeDevice / DispHandsfreeCaptureDevice (binds devices)
  → RegisterUserConnector4UC (builds TLS tunnel, logs in as CTI master)
  → Subscribes to PubOnLineMgrNotification + DispOnLineMgrNotification
  → CLMgr now treats us as a full phone client → audio works → calls connect
```

### Reverse Engineering Documentation

Full technical analysis (1000+ lines) in [`docs/REVERSE_ENGINEERING.md`](docs/REVERSE_ENGINEERING.md):
- CLMgr COM API signatures (all DispIds, vtable methods)
- RemoteConnector tunnel mechanism
- Audio plugin architecture (GenericDevicePlugin → AudioVolumeControl → DirectSound)
- ComSocket SignalR hub internals
- CTI-slave architecture and HandleCallPopup mechanism

---

## Known Limitations

- Windows only (requires Swyx COM components + CLMgr)
- Requires Swyx Client SDK installed (for CLMgr.exe and Plugins)
- Teams write not supported from client (use server-side SwyxWare sync)
- ComSocket auth requires process name `SwyxMessenger.exe` (whitelist match)
- Call journal limited to 20 most recent entries (ComSocket paging)
- Audio device names must match Windows DirectSound device names

---

## Build Notes

### Google Drive / Cloud-Sync Folders
The .NET SDK's `CreateAppHost` task uses MemoryMappedFiles, which fail on certain cloud-sync filesystems. The included `scripts/build-bridge.ps1` automatically copies the bridge source to a local temp directory before building.

### x86 Requirement
The bridge must be compiled as **x86** because CLMgr COM is a 32-bit server. The `PlatformTarget=x86` setting in the csproj enforces this.

### SwyxMessenger.exe Process Name
The bridge assembly is named `SwyxMessenger` because the ComSocket authentication whitelist checks the **process name**. `SwyxMessenger.exe` is on the whitelist. `UseAppHost=true` produces a native exe with this name.

---

## Roadmap

- [ ] Plugin system
- [ ] Callcenter dashboard with live statistics
- [ ] Keyboard shortcuts
- [ ] Multi-language support (English/German)
- [ ] Call recording playback in-app
- [ ] Inbound call answer (live test pending)
- [ ] Voicemail playback (live test pending)

---

## License

Proprietary (c) Ralle1976. All rights reserved.

## Acknowledgments

- Swyx/Enreach for the CLMgr COM API, ComSocket SignalR hub, and Audio Plugin SDK
- Electron, React, TypeScript, Tailwind CSS, Zustand
- .NET 8, SignalR Client, COM Interop
