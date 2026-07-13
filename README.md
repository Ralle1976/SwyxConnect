# SwyxConnect

> **Modern Electron-based softphone client for Swyx/Enreach telephony.**
> Replaces SwyxIt! as the primary phone interface with a clean, modern UI.

[![Version: 1.2.0](https://img.shields.io/badge/Version-1.2.0-brightgreen.svg)]()
[![Platform: Windows x86](https://img.shields.io/badge/Platform-Windows%20x86-lightgrey.svg)]()
[![SwyxIt: v14.x](https://img.shields.io/badge/SwyxIt!-v14.x-orange.svg)]()
[![.NET: 8.0](https://img.shields.io/badge/.NET-8.0-purple.svg)]()

---

## Overview

SwyxConnect is a desktop softphone client that communicates with the Swyx Client Line Manager (CLMgr) via a dual-protocol C# Bridge (COM + ComSocket). It provides a modern React-based UI for all telephony functions — calls, presence, contacts, voicemail, and more.

### Key Features

- **Telephony:** Dial, answer, hold, transfer, conference, DTMF, recording (COM-based — implemented, see [verification status](#verified-vs-untested-features))
- **Complete Colleague Presence:** All colleagues from the company phonebook with live status (Available, Busy, Away, DND, Offline) — verified working with 30 entries via ComSocket
- **Detailed Call Journal:** Incoming, outgoing, and missed calls with duration, date, and contact name — verified working with 20 entries via ComSocket
- **Voicemail:** Visual voicemail with message count and remote inquiry (implemented, playback not live-tested)
- **Contacts:** Phonebook search and speed dial management
- **Call Forwarding:** Configure unconditional, busy, and no-reply forwarding rules — verified config reads correctly
- **Audio Configuration:** Headset/speakerphone mode, microphone/speaker volume, mute
- **Modern UI:** German interface, dark mode, system tray integration
- **Auto-Attach:** Detects running SwyxIt! session and connects automatically — verified working
- **Real-time Push Events:** ComSocket (SignalR) delivers instant line-state and presence updates (wiring verified, live events not observed during testing)

### Architecture (Hybrid: COM + ComSocket)

```
Electron App (Node.js + React 19 + TypeScript + Tailwind v4)
  Main Process ←→ Preload (contextBridge) ←→ Renderer (Zustand stores)
       │
       │ stdin/stdout (JSON-RPC 2.0, newline-delimited)
       ▼
  C# Bridge — SwyxMessenger.exe (.NET 8, x86, UseAppHost=true)
  ├── COM Interop → CLMgr.exe (32-bit COM Server)
  │   └── Login, Call Control, Line State, Audio, Voicemail, Forwarding
  └── ComSocket (SignalR) → SwyxItHub @ ws://localhost:PORT/swyxIt
      └── PhoneBook, CallJournal, SpeedDials, VoiceMessages, live Push Events
       │
       ▼
  CLMgr.exe — Swyx Client Line Manager (requires running SwyxIt! session)
```

### Why Hybrid?

| Protocol | Strength | Use |
|---|---|---|
| **COM** | Reliable call control, line state, audio | Login, Dial, Answer, Hold, Transfer, Audio devices |
| **ComSocket** | Rich data, all colleagues, real-time push | PhoneBook (all entries), CallJournal, Presence updates |

COM alone only exposes speed-dial contacts (6-10 entries). ComSocket delivers the **complete company phonebook (30+ colleagues)** with live presence. The bridge combines both for maximum coverage.

### Auto-Attach Mode

SwyxConnect automatically detects a running SwyxIt! session and attaches to it — no manual login required. If SwyxIt! is running and logged in, SwyxConnect:
1. Attaches to the existing COM session
2. Connects to the ComSocket SignalR hub
3. Loads all colleagues, call history, and configuration
4. Lands directly in the main UI (skips the login form)

## Requirements

- **Windows 10/11** (x86 or x64, runs as 32-bit)
- **.NET 8 Desktop Runtime (x86)** — [download](https://dotnet.microsoft.com/download/dotnet/8.0)
- **SwyxIt! v14.x** installed and running with an active Swyx session
- **Swyx CLMgr** running (started automatically with SwyxIt!)

## Download & Install

### Option 1: Installer (recommended)

1. Download `SwyxConnect-Setup-1.2.0.exe` from the [latest release](https://github.com/Ralle1976/SwyxConnect/releases/latest)
2. Run the installer
3. Launch SwyxConnect — it auto-connects to your SwyxIt! session

### Option 2: Build from Source

```bash
git clone https://github.com/Ralle1976/SwyxConnect.git
cd SwyxConnect
npm install

# Build C# Bridge (produces SwyxMessenger.exe)
powershell -ExecutionPolicy Bypass -File scripts/build-bridge.ps1

# Build Electron app (main + preload + renderer)
npx electron-vite build

# Run in dev mode
npx electron out/main/index.js

# Create installer
npx electron-builder --win --ia32
```

## Usage

### Phone View (`/`)
- Dialpad with country code support
- Line status (Line 1/2 with state)
- DTMF, hold, transfer, conference controls

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
- System tray behavior (minimize on close, start minimized)

## Microsoft Teams Integration

Bidirectional Teams presence synchronization is a **server-side SwyxWare feature** (v14.20+), configured in the Swyx Control Center:

1. Configure `MsTeamsStatusSyncMode = 2` on the Swyx Server
2. Register an Azure AD App with `Presence.ReadWrite.All` Graph permission
3. When a Swyx user is on a call, the server sets their Teams status to "In a call"

SwyxConnect displays the Teams status read-only via Microsoft Graph API for information purposes. The server handles the bidirectional sync — no client-side write required.

See: [Enreach Documentation — Teams Presence Sync](https://service-de.enreach.com/hc/en-gb/articles/25585335166492)

## Technical Details

### JSON-RPC Protocol
Newline-delimited JSON-RPC 2.0 over stdin/stdout between Electron and C# Bridge.
- **Requests:** `{jsonrpc:"2.0", id:N, method:"...", params:{...}}`
- **Responses:** `{jsonrpc:"2.0", id:N, result:{...}}`
- **Events (push):** `{method:"...", params:{...}}` (no id)

### Bridge Handlers (14)
System, Call, Presence, Audio, Contact, History, Voicemail, Forwarding, Conference, Recording, SystemInfo, Chat, CTI, **ComSocket**

### ComSocket Methods (cs.*)
- `cs.getPhoneBook` — All colleagues with live presence
- `cs.getCallJournal` — Detailed call history (part-based paging)
- `cs.searchContacts` — Full-text contact search
- `cs.getSpeedDials` — Speed-dial entries
- `cs.getVoiceMessages` — Voicemail list
- `cs.getForwardingConfig` — Forwarding rules
- `cs.getAudioModes` / `cs.getAudioVolumes` — Audio configuration
- `cs.getVersionInfo` — Swyx version details
- `cs.getStatus` / `cs.reconnect` — Connection management

### ComSocket Push Events
- `cs.lineStateChanged` — Real-time line state updates
- `cs.lineDetailsChanged` — Detailed call info (caller ID, duration)
- `cs.userDataChanged` — Colleague presence changes
- `cs.notificationCallsChanged` — Missed call notifications

### LineState Mapping
0=Inactive, 3=Ringing, 4=Dialing, 8=Active, 9=OnHold, 12=Terminated

## Verified vs. Untested Features

To be transparent about what's actually been tested vs. what's implemented but not yet verified:

### ✅ Verified Working (live-tested via CDP automation, 2026-07-13)
- **Auto-Attach:** Login form skipped, main UI loads directly
- **ComSocket (SignalR):** Connects to SwyxItHub on port 12042
- **Presence View:** 30 colleagues from company phonebook with presence states
- **Call History:** 20 journal entries with All/Incoming/Outgoing/Missed tabs
- **Phone Dialer:** Keypad, line status (Line 1/2), Anrufen button
- **Settings:** Audio mode, forwarding config, theme, line count
- **API calls:** `cs.getPhoneBook`, `cs.getCallJournal`, `cs.getForwarding`, `getLines`, `getSystemInfo` all return correct data

### ⚠️ Implemented but NOT Live-Tested
- **Outbound calls (Dial → Answer → Hangup):** The COM `dial` method is wired up and `getSystemInfo` confirms `serverUp=true`, but no actual phone call was made during testing
- **Inbound calls (Incoming call notification):** Event handlers exist, but no test call was received
- **Voicemail playback:** The view renders, but audio playback was not tested
- **Conference/Transfer/Hold during active call:** COM methods exist (`createConference`, `transfer`, `hold`), not tested with a live call
- **DTMF during call:** Implemented, not tested
- **Recording start/stop:** Implemented, not tested
- **Real-time push events (lineStateChanged, userDataChanged):** Event wiring exists, but no status change was observed during testing
- **Teams Graph API login:** The OAuth flow is implemented, but the login button was not clicked during testing

### Known Limitations
- Windows only (requires Swyx COM components + CLMgr)
- Requires a running SwyxIt! session (Auto-Attach mode)
- Teams write not supported from client (use server-side SwyxWare sync)
- Call journal limited to 20 most recent entries (ComSocket paging)
- `teams.local.getTeamsPresence` bridge method is not yet implemented (frontend will show an error in the Teams settings section)

## Build Notes

### x86 Requirement
The bridge must be compiled as **x86** because CLMgr COM is a 32-bit server. The `PlatformTarget=x86` setting in the csproj enforces this. The Electron installer is also built for `ia32`.

### SwyxMessenger.exe Process Name
The bridge assembly is named `SwyxMessenger` (not `SwyxStandalone`) because the ComSocket authentication whitelist checks the **process name**, and `SwyxMessenger.exe` is on the whitelist. `UseAppHost=true` produces a native exe with this name.

## Roadmap

- [ ] Plugin system
- [ ] Callcenter dashboard with live statistics
- [ ] Keyboard shortcuts
- [ ] Multi-language support (English/German)
- [ ] Call recording playback in-app

## License

Proprietary (c) Ralle1976. All rights reserved.

## Acknowledgments

- Swyx/Enreach for the CLMgr COM API and ComSocket SignalR hub
- Electron, React, TypeScript, Tailwind CSS, Zustand
- .NET 8, SignalR Client
