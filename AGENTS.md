# AGENTS.md — SwyxConnect

> Kontext-Dokument für AI-Agenten, die an diesem Projekt arbeiten.
> Enthält Architektur, Konventionen, COM-API-Details und aktuelle Projektstand.

---

## Projekt-Überblick

**SwyxConnect** ist ein moderner Electron-basierter Desktop-Softphone-Client, der SwyxIt! als primäre Benutzeroberfläche für Swyx/Enreach-Telefonie ersetzt. Die Anwendung kommuniziert über eine C#-COM-Bridge mit dem lokalen Swyx Client Line Manager (CLMgr).

- **Repo**: https://github.com/Ralle1976/SwyxConnect
- **Wiki**: https://github.com/Ralle1976/SwyxConnect/wiki
- **Autor**: Ralle1976 (`Ralle1976@users.noreply.github.com`)
- **Lizenz**: Proprietär

---

## Architektur

```
┌─────────────────────────────────────────────────┐
│               Electron App (Node.js)            │
│  ┌───────────┐  ┌──────────┐  ┌──────────────┐ │
│  │   Main    │  │ Preload  │  │   Renderer   │ │
│  │ Process   │◄─┤  Bridge  ├─►│ React + TS   │ │
│  │ (IPC Hub) │  │ (ctx)    │  │ Tailwind v4  │ │
│  └─────┬─────┘  └──────────┘  └──────────────┘ │
│        │ stdin/stdout (JSON-RPC 2.0)            │
│  ┌─────▼─────────────────────────────────┐      │
│  │  C# Bridge (SwyxBridge.exe)           │      │
│  │  .NET 8 | [STAThread] | WinForms Pump │      │
│  │  COM Interop → CLMgr.exe              │      │
│  └───────────────────────────────────────┘      │
└─────────────────────────────────────────────────┘
         │
         ▼
  ┌──────────────┐
  │  CLMgr.exe   │  Swyx Client Line Manager (COM-Server)
  │  (headless)  │  Registriert als COM-Objekt {f8e5536b-...}
  └──────────────┘
```

### IPC-Protokoll

- **Format**: Newline-delimited JSON mit JSON-RPC 2.0 Envelope
- **Transport**: stdin/stdout zwischen Electron Main Process und SwyxBridge.exe
- **Events**: Bridge emittiert JSON-RPC Notifications (kein `id`) für `lineStateChanged`, `presenceNotification`, `voicemailNotification`, etc.

---

## Zielgruppe

- Callcenter-Agenten
- Admins / Supervisoren
- Windows-PC Büronutzer

---

## Verbindliche Regeln

### Git & Commits

- **Immer als Ralle1976 committen** — nie andere Autoren
- **Keine Secrets hochladen** — `.env` ist in `.gitignore`, niemals Tokens/Keys committen
- **gh CLI** ist als Ralle1976 authentifiziert

### UI & Dokumentation

- **Vollständig deutsche Benutzeroberfläche** — alle Labels, Buttons, Texte auf Deutsch
- **Wiki konzentriert sich auf App-Funktionalität** — keine technischen Interna, keine Hinweise auf KI-Erstellung
- **README** beschreibt Features und Installation — keine internen Implementierungsdetails

### Plattform

- **Nur Windows x64** — kein ARM64, kein macOS, kein Linux (macOS auf later verschoben)
- **SwyxIt! 14.x** muss lokal installiert und angemeldet sein

---

## Technische Entscheidungen (VERBINDLICH)

```
DECISION: IPC = Newline-delimited JSON mit JSON-RPC 2.0 Envelope
DECISION: C# Bridge = [STAThread] + Application.Run() Message-Pump (WinForms-Dependency)
DECISION: Ship x64 Windows only. Kein ARM64, kein macOS, kein Linux.
DECISION: Bridge-Prozess kill = taskkill /PID {pid} /F (nicht child.kill())
```

### MUST

- `Console.OutputEncoding = Encoding.UTF8` in C# `Main()`
- Alle COM Event-Sink-Objekte in static/long-lived Fields speichern
- Electron baut und startet über `electron-vite`

### MUST NOT

- `.Result` oder `.Wait()` auf Tasks im STA-Thread (Deadlock!)
- COM-Objekte auf Background-Threads erstellen
- `as any`, `@ts-ignore`, `@ts-expect-error` in TypeScript
- Secrets in Code oder Commits

---

## COM-API Referenz

### CLMgr Root-Objekt (`{f8e5536b-4c00-11d3-80bc-00105a653379}`)

```
DispNumberOfLines          Property  int (get)         ← NICHT Method!
DispGetLine(int)           Method    IDispatch
DispSelectedLine           Property  IDispatch (get)
DispSelectedLineNumber     Property  int (get)
DispSelectLineNumber(int)  Method    int
DispSwitchToLineNumber(int) Method   int
DispSetNumberOfLines(int)  Method    int
DispSimpleDialEx3(string, int, int, string)  Method void
DispClientConfig           Property  IDispatch (get)
DispHookOn()               Method    void
DispHookOff()              Method    void
```

### Line-Objekt (von DispGetLine / DispSelectedLine)

```
DispState                  Property  int (get)         ← 0=Inactive..15=DirectCall
DispPeerName               Property  string (get)
DispPeerNumber             Property  string (get)
DispCallerName             Property  string (get)
DispCallerNumber           Property  string (get)
DispCallId                 Property  int (get)
DispIsOutgoingCall         Property  int (get)
DispConnectionStartTime    Property  Date (get)
DispHookOn()               Method    void
DispHookOff()              Method    void
DispHold()                 Method    void
DispActivate()             Method    void
DispDial(string)           Method    void
DispForwardCall(string)    Method    void
DispSendDtmf(string,int)   Method    void
```

### LineState Mapping (COM int → TypeScript string)

```
 0 = Inactive          8  = Active
 1 = HookOffInternal   9  = OnHold
 2 = HookOffExternal   10 = ConferenceActive
 3 = Ringing           11 = ConferenceOnHold
 4 = Dialing           12 = Terminated
 5 = Alerting          13 = Transferring
 6 = Knocking          14 = Disabled
 7 = Busy              15 = DirectCall
```

### DispClientConfig (Präsenz)

```
cfg.Away                        Property  bool (get/set)
cfg.DoNotDisturb                Property  bool (get/set)
cfg.SetRichPresenceStatus(str)  Method    void
cfg.PublicateDetectedAwayState(bool) Method void
cfg.CallerEnumerator            Property  IEnumerable (get)  ← Anrufhistorie
cfg.VoiceMessagesEnumerator     Property  IEnumerable (get)
cfg.NumberOfNewVoicemails       Property  int (get)
```

### COM Events

```
PubOnLineMgrNotification(int msg, int param)
  msg 0-3  → Leitungsstatus-Änderung
  msg 9    → Voicemail
  msg 10   → Präsenz
```

---

## Verzeichnisstruktur

```
SwyIt-byRalle1976/
├── .env                          # Secrets (NIEMALS committen)
├── .github/workflows/ci.yml     # GitHub Actions CI
├── README.md
├── AGENTS.md                     # Dieses Dokument
├── package.json                  # Electron + React Dependencies
├── electron.vite.config.ts
├── electron-builder.yml
├── tsconfig.json / .web.json / .node.json
│
├── bridge/
│   ├── SwyxBridge/               # C# COM Bridge (.NET 8)
│   │   ├── Program.cs            # Entry: [STAThread] + Message-Pump
│   │   ├── Com/
│   │   │   ├── SwyxConnector.cs  # COM-Verbindung und Lifecycle
│   │   │   ├── LineManager.cs    # Multi-Line: Dial, Hangup, GetAllLines
│   │   │   └── EventSink.cs     # PubOnLineMgrNotification → JSON-RPC
│   │   ├── Handlers/
│   │   │   ├── CallHandler.cs    # JSON-RPC → LineManager Routing
│   │   │   ├── PresenceHandler.cs # Away/DND/Available via DispClientConfig
│   │   │   ├── HistoryHandler.cs  # CallerEnumerator (Property "Time")
│   │   │   ├── VoicemailHandler.cs
│   │   │   └── ContactHandler.cs
│   │   ├── JsonRpc/              # Request/Response/Emitter
│   │   └── Utils/                # Logging
│   ├── SwyxStandalone/           # Standalone Bridge (RegisterUserEx) [experimental]
│   └── SwyxSpike/                # COM Connectivity Spike
│
├── src/
│   ├── shared/
│   │   ├── types.ts              # LineState enum, LineInfo, BridgeState, etc.
│   │   └── constants.ts          # IPC_CHANNELS
│   ├── main/
│   │   ├── index.ts              # Electron main: Window, Bridge, Tray
│   │   ├── tray.ts               # System Tray Integration
│   │   ├── bridge/
│   │   │   ├── BridgeManager.ts  # Spawnt SwyxBridge.exe, JSON-RPC I/O
│   │   │   ├── BridgeProtocol.ts # JSON-RPC Framing
│   │   │   └── BridgeReconnect.ts
│   │   ├── ipc/handlers.ts       # IPC Main↔Renderer: dial, getLines, etc.
│   │   └── services/
│   │       ├── SettingsStore.ts
│   │       └── NotificationService.ts
│   ├── preload/index.ts          # contextBridge: swyxApi + windowControls
│   └── renderer/
│       ├── src/App.tsx            # Root: Event-Listener, Routing
│       ├── stores/
│       │   ├── useLineStore.ts   # Zustand: Lines, selectedLineId, auto-select
│       │   ├── usePresenceStore.ts
│       │   ├── useHistoryStore.ts
│       │   ├── useSettingsStore.ts
│       │   └── useCallHistoryTracker.ts
│       ├── hooks/
│       │   ├── useCall.ts        # dial/hangup/hold/transfer + getLines-Polling
│       │   ├── usePresence.ts
│       │   └── useBridge.ts
│       ├── types/swyx.ts         # Re-exports aus shared/types
│       └── components/
│           ├── phone/            # PhoneView, Dialpad, ActiveCallPanel, LineButtons
│           ├── contacts/
│           ├── history/
│           ├── voicemail/
│           ├── presence/
│           ├── settings/
│           ├── callcenter/
│           ├── layout/           # TitleBar, Sidebar, MainContent
│           └── common/           # Avatar
│
├── out/                          # Build Output
│   ├── main/index.js
│   ├── preload/index.js
│   ├── renderer/                 # Vite-built React bundle
│   └── bridge/                   # Deployed SwyxBridge.exe + DLLs
│
├── scripts/
│   └── test-bridge.mjs           # Node.js Test-Script (spawnt Bridge via PowerShell)
│
├── resources/                    # App-Icons
├── plugins/                      # Plugin-Verzeichnis (Erweiterbarkeit)
└── tests/
```

---

## Build & Deploy

### Voraussetzungen

- **Node.js** v22.x, npm 10.x
- **.NET 8 SDK** (v8.0.418) — Pfad: `$HOME/.dotnet`
- **electron-vite** v5.x, **Electron** v35.x

### Build-Befehle

```bash
# .NET SDK verfügbar machen
export PATH="$HOME/.dotnet:$PATH"

# C# Bridge bauen
cd bridge/SwyxBridge
dotnet publish -c Release -r win-x64 --self-contained false

# Bridge deployen
powershell.exe -Command "Get-Process SwyxBridge -ErrorAction SilentlyContinue | Stop-Process -Force"
cp bridge/SwyxBridge/bin/x64/Release/net8.0-windows/win-x64/publish/* out/bridge/

# Electron App bauen
npx electron-vite build

# App starten (Produktion)
npx electron out/main/index.js

# App starten (Entwicklung)
npm run dev
```

### Test-Script

```bash
node scripts/test-bridge.mjs
```
Spawnt Bridge über `powershell.exe`, sendet JSON-RPC Requests, prüft Responses.

---

## Datenfluss: Anruf

```
User klickt "Anrufen"
  → useCall.dial(number)
    → window.swyxApi.dial(number)
      → ipcRenderer.invoke('DIAL', number)
        → ipcMain.handle → bridgeManager.sendRequest('dial', {number})
          → stdin → SwyxBridge.exe
            → CallHandler.HandleDial()
              → LineManager.Dial(number)
                → COM: DispSimpleDialEx3(number, 0, 0, "")
                  (Fallback: DispSelectedLine.DispDial / DispGetLine(0).DispDial)
                → SuppressSwyxWindow() — minimiert SwyxIt! sofort

COM feuert PubOnLineMgrNotification(msg=0..3)
  → EventSink.OnLineMgrNotification()
    → LineManager.GetAllLines()  — liest alle Leitungen mit DispGetLine(i)
    → JsonRpcEmitter.EmitEvent("lineStateChanged", {lines: [...]})
      → stdout → BridgeManager parst JSON-RPC
        → handlers.ts: case 'lineStateChanged' → webContents.send(LINE_STATE_CHANGED, lines)
          → preload: onLineStateChanged callback
            → App.tsx: setLines(updatedLines)
              → useLineStore: auto-select erste aktive Leitung
                → PhoneView: isCallActive=true → <ActiveCallPanel />
```

---

## Verifizierter Status

### Funktioniert ✅

- COM-Verbindung und Event-Subscription
- Präsenz: Available, Away, DND (via DispClientConfig)
- Anrufhistorie: CallerEnumerator mit "Time"-Property
- Voicemail: VoiceMessagesEnumerator + NumberOfNewVoicemails
- E2E App-Start: Bridge verbindet, UI zeigt "Verbunden"
- Multi-Line COM API: DispNumberOfLines, DispGetLine(int)
- GitHub CI Pipeline (Build + Security Audit)

### In Arbeit 🔧

- **Anruf-Flow End-to-End**: Bridge-Code ist korrigiert (multi-line), Frontend aktualisiert — noch nicht live getestet
- **SwyxIt!-Fensterunterdrückung**: Code vorhanden, Timing-abhängig

### Geplant 📋

- Microsoft Teams V2 WebSocket Präsenz-Sync (bidirektional)
- electron-builder Packaging (.exe Installer)
- Plugin-System (Erweiterbarkeit)
- Callcenter-Dashboard mit Live-Daten
- Keyboard Shortcuts
- macOS-Support (via REST/Remote Bridge — Server-Ports aktuell blockiert)

---

## Umgebung

- **SwyxIt! v14.25.8537.0** (Deutsch, On-Premises CPE)
- **Benutzer**: `Ralf.Arnold@oneqrew.com`, SiteID 1, EntityID 23
- **Swyx Server**: `172.18.3.202` (intern, REST-Ports blockiert)
- **CLMgr.exe**: `C:\Program Files (x86)\Swyx\SwyxIt!\CLMgr.exe` (headless, kein GUI)
- **Entwicklung**: WSL2 + Windows, PowerShell-Interop für Bridge-Tests

---

## Externe Referenzen

- [Swyx Client SDK](https://clientsdk.swyx.engineering/)
- [CLMgrPubTypes.h](https://clientsdk.swyx.engineering/_c_l_mgr_pub_types_8h.html)
- [Swyx CPE Hilfe](https://help.enreach.com/cpe/14.25/App/Swyx/de-DE/index.html)
