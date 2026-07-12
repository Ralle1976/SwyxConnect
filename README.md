# SwyxConnect

> **Modern Electron-based softphone client for Swyx/Enreach telephony.**
> Replaces SwyxIt! as the primary phone interface with a clean, modern UI.

[![License: Proprietary](https://img.shields.io/badge/License-Proprietary-blue.svg)](LICENSE)
[![Platform: Windows x86](https://img.shields.io/badge/Platform-Windows%20x86-lightgrey.svg)]()
[![SwyxIt: v14.x](https://img.shields.io/badge/SwyxIt!-v14.x-orange.svg)]()
[![.NET: 8.0](https://img.shields.io/badge/.NET-8.0-purple.svg)]()

---

## Overview

SwyxConnect is a desktop softphone client that communicates with the Swyx Client Line Manager (CLMgr) via a C# COM Bridge. It provides a modern React-based UI for all telephony functions — calls, presence, contacts, voicemail, and more.

### Key Features

- **Full Telephony:** Dial, answer, hold, transfer, conference, DTMF, recording
- **Colleague Presence:** Real-time status of team members (Available, Busy, Away, DND)
- **Call History:** Incoming, outgoing, and missed calls with duration and contacts
- **Voicemail:** Visual voicemail with message count and remote inquiry
- **Contacts:** Phonebook search and speed dial management
- **Modern UI:** German interface, dark mode, system tray integration
- **Call Forwarding:** Configure unconditional, busy, and no-reply forwarding
- **Notifications:** Desktop notifications for incoming calls and voicemails

### Architecture

```
Electron App (Node.js)
  Main Process <-> Preload Bridge <-> Renderer (React + TS + Tailwind v4)
       |
       | stdin/stdout (JSON-RPC 2.0)
       v
  C# Bridge (SwyxStandalone.dll, .NET 8 x86)
  COM Interop -> CLMgr.exe (32-bit COM Server)
  13 JSON-RPC Handlers
       |
       v
  CLMgr.exe — Swyx Client Line Manager
  Auto-Attach: connects to running SwyxIt! session
```

### Auto-Attach Mode

SwyxConnect automatically detects a running SwyxIt! session and attaches to it — no manual login required. If SwyxIt! is running and logged in, SwyxConnect connects instantly.

## Requirements

| Component | Requirement |
|---|---|
| Operating System | Windows 10/11 (32-bit or 64-bit) |
| Swyx Client | SwyxIt! v14.x installed and logged in |
| .NET Runtime | .NET 8 Desktop Runtime (x86) |
| RAM | 512 MB |
| Disk Space | 200 MB |

## Installation

### Download Installer

1. Download `SwyxConnect-Setup-1.0.0.exe` from [Releases](../../releases)
2. Run the installer (administrator privileges required)
3. Launch SwyxConnect — it auto-connects to your SwyxIt! session

### Build from Source

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

## Usage

- **Call:** Open dialpad, enter number or search contact, click "Anrufen"
- **Presence:** View colleague status in the "Prasenz" tab
- **Voicemail:** Check messages in the "Voicemail" tab
- **Settings:** Configure audio, theme, trunk prefix, Teams integration

## Microsoft Teams Integration

Bidirectional Teams presence synchronization is a **server-side SwyxWare feature** (v14.20+), configured in the Swyx Control Center. SwyxConnect displays Teams status read-only via Microsoft Graph API.

## Technical Details

### JSON-RPC Protocol
Newline-delimited JSON-RPC 2.0 over stdin/stdout between Electron and C# Bridge.

### Bridge Handlers (13)
System, Call, Presence, Audio, Contact, History, Voicemail, Forwarding, Conference, Recording, SystemInfo, Chat, CTI

### LineState Mapping
0=Inactive, 3=Ringing, 4=Dialing, 8=Active, 9=OnHold, 12=Terminated

## Known Limitations

- Colleague presence shows speed dial contacts only (full directory requires server-side User Appearance Publishing)
- Teams write not supported from client (use server-side SwyxWare sync)
- Windows only (requires Swyx COM components)

## Roadmap

- ComSocket/SignalR integration (full company directory + live presence)
- Plugin system
- Callcenter dashboard with live statistics
- Keyboard shortcuts

## License

Proprietary (c) Ralle1976. All rights reserved.
