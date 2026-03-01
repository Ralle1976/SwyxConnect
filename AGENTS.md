# AGENTS.md — SwyxConnect

> Kontext-Dokument für AI-Agenten, die an diesem Projekt arbeiten.
> Enthält Architektur, Konventionen, COM-API-Details und aktuelle Projektstand.

---

## Projekt-Überblick

**SwyxConnect** ist ein moderner Electron-basierter Desktop-Softphone-Client, der SwyxIt! als primäre Benutzeroberfläche für Swyx/Enreach-Telefonie ersetzt. SwyxIt! läuft dabei **unsichtbar im Hintergrund** als Tunnel-Provider (WindowHook unterdrückt alle Fenster). Die Anwendung nutzt das **Swyx Client SDK** (`Swyx.Client.ClmgrAPI` v14.21.0 NuGet) für typisierte COM-Interop über eine C#-Bridge im Attach-Modus.

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
│  ┌─────▼─────────────────────────────────────┐  │
│  │  SwyxBridge.exe (.NET 8, x86)             │  │
│  │  WindowHook: SwyxIt!-Fenster versteckt    │  │
│  │  SwyxItLauncher: Auto-Start hidden         │  │
│  │  COM Attach → CLMgr → SwyxIt! (hidden)    │  │
│  └─────────────────────────────────────────┘  │
│        │ COM Interop (Attach-Modus)            │
│  ┌─────▼─────────────────────────────────────┐  │
│  │  SwyxIt!.exe (VERSTECKT, kein UI)         │  │
│  │  CLMgr.exe (COM-Server)                   │  │
│  │  RemoteConnector-Tunnel → Swyx Server     │  │
│  └───────────────────────────────────────────┘  │
└─────────────────────────────────────────────────┘
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

- **Nur Windows x86 (32-Bit)** — kein x64, kein ARM64, kein macOS, kein Linux (CLMgr COM ist 32-Bit)
- **Swyx Client SDK v14.21.0** — NuGet-Paket `Swyx.Client.ClmgrAPI`
- **Nur Attach-Modus**: SwyxIt!.exe läuft versteckt im Hintergrund (DispInit = E_NOTIMPL)
---

## Technische Entscheidungen (VERBINDLICH)

```
DECISION: IPC = Newline-delimited JSON mit JSON-RPC 2.0 Envelope
DECISION: C# Bridge = [STAThread] + Application.Run() Message-Pump (WinForms-Dependency)
DECISION: Ship x86 Windows only (32-Bit wegen CLMgr COM)
DECISION: Bridge-Prozess kill = taskkill /PID {pid} /F (nicht child.kill())
DECISION: COM-Erstellung = Type.GetTypeFromProgID + Activator.CreateInstance, NICHT new ClientLineMgrClass() (hängt auf STA)
DECISION: SDK NuGet = Swyx.Client.ClmgrAPI v14.21.0 (typed COM interop)
DECISION: WSL2 Dev = Bridge-Files auf C:\temp\SwyxBridge\ kopieren (UNC-Pfad-Bug mit .NET Assembly-Caching)
DECISION: Standalone-Architektur = SIP-UA (SIPSorcery) + CDS WCF Client + eigener Kestrel-Host
DECISION: CDS-Verbindung = WCF net.tcp auf Port 9094, Login via ConfigDataStore/CLoginImpl.none
DECISION: Auth-Modus = JWT (JasonWebToken) nach AcquireToken mit Username/Password
```
DECISION: DispInit = E_NOTIMPL — Standalone-COM nicht möglich, nur Attach-Modus
DECISION: SwyxIt! = versteckter Tunnel-Provider (WindowHook + SwyxItLauncher)
DECISION: Deployment = Komplettpaket in dist/SwyxConnect/ zum Kopieren

### MUST

- `Console.OutputEncoding = Encoding.UTF8` in C# `Main()`
- Alle COM Event-Sink-Objekte in static/long-lived Fields speichern
- Electron baut und startet über `electron-vite`

### MUST NOT

- `.Result` oder `.Wait()` auf Tasks im STA-Thread (Deadlock!)
- COM-Objekte auf Background-Threads erstellen
- `as any`, `@ts-ignore`, `@ts-expect-error` in TypeScript
- Secrets in Code oder Commits

### KRITISCHE ERKENNTNIS: DispInit = E_NOTIMPL

`DispInit("serverName")` gibt `E_NOTIMPL (0x80004001)` zurück. Der Standalone-Modus des CLMgr COM-Objekts ist NICHT implementiert. COM-Zugriff funktioniert NUR im Attach-Modus, wenn SwyxIt!.exe als COM-Host läuft.

**Konsequenz**: SwyxIt!.exe MUSS laufen (wird automatisch versteckt gestartet). Der RemoteConnector-Tunnel auf Port 15021 ist ein proprietäres Binärprotokoll — nur SwyxIt!/CLMgr kann ihn aufbauen.

**Option 2 (parallel)**: Decompilierung von `IpPbx.Client.Plugin.ComSocket.dll` (.NET Assembly) könnte den Tunnel-Client offenlegen und SwyxIt! langfristig ersetzen.

---

## COM-API Referenz (SDK-typisiert)

### CLMgr Root-Objekt (`ClientLineMgrClass`, ProgID: `CLMgr.ClientLineMgr`)

```
DispInit(string)              Method    int       ← Standalone-Verbindung zum Server
PubInit(string)               Method    void      ← Alternative Standalone-Init
UnInit()                      Method    void      ← Verbindung trennen
PubGetServerFromAutoDetection(...) Method void    ← Server auto-erkennen im Netzwerk
DispNumberOfLines             Property  int (get)
DispGetLine(int)              Method    object → IClientLineDisp
DispSelectedLine              Property  object → IClientLineDisp (get)
DispSelectedLineNumber        Property  int (get)
DispSetNumberOfLines(int)     Method    int
DispSimpleDialEx3(string, int, int, string)  Method uint
DispClientConfig              Property  object (get) → dynamic cast nötig
FulltextSearchInContactsEx(string, int, int, int, out object) Method int
DispResolveNumber(string)     Method    string
DispIsLoggedIn                Property  int (get)  ← 0=nein, 1=ja
DispIsServerUp                Property  int (get)
DispGetCurrentServer          Property  string (get)
DispGetCurrentUser            Property  string (get)
EnableNotifyUserAppearanceChanged() Method void
```

### Line-Objekt (`IClientLineDisp` — typed Interface)

```
DispState                     Property  int (get)  ← 0=Inactive..15=DirectCall
DispDial(string)              Method    void
DispHookOn()                  Method    void
DispHookOff()                 Method    void
DispHold()                    Method    void
DispActivate()                Method    void
DispForwardCall(string)       Method    void
DispCalledName                Property  string (get)
DispCallId                    Property  int (get)

# ACHTUNG: Folgende Properties NICHT auf IClientLineDisp typed interface!
# Müssen via dynamic cast gelesen werden: ((dynamic)line).DispPeerName
DispPeerName                  Property  string (get)   ← nur via dynamic
DispPeerNumber                Property  string (get)   ← nur via dynamic
DispCallerName                Property  string (get)   ← nur via dynamic
DispCallerNumber              Property  string (get)   ← nur via dynamic
```

### History (`CallerCollectionClass` / `CallerItemClass` — typed)

```
CallerItemClass.Name          Property  string
CallerItemClass.Number        Property  string
CallerItemClass.Time          Property  DateTime
CallerItemClass.CallDuration  Property  int
CallerItemClass.CallState     Property  int
CallerItemClass.DialedNumber  Property  string
CallerItemClass.DialedName    Property  string
CallerItemClass.ConnectedName Property  string
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

### DispClientConfig (Präsenz — via dynamic cast)

```
cfg.Away                        Property  bool (get/set)   ← dynamic
cfg.DoNotDisturb                Property  bool (get/set)   ← dynamic
cfg.SetRichPresenceStatus(str)  Method    void             ← dynamic
cfg.PublicateDetectedAwayState(bool) Method void           ← dynamic
cfg.CallerEnumerator            Property  IEnumerable (get)← dynamic
cfg.VoiceMessagesEnumerator     Property  IEnumerable (get)← dynamic
cfg.NumberOfNewVoicemails       Property  int (get)        ← dynamic
```

### COM Events (typed delegate)

```
IClientLineMgrEventsPub_PubOnLineMgrNotificationEventHandler
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
│   ├── SwyxBridge/               # C# COM Bridge (.NET 8 + SDK v14.21.0)
│   │   ├── SwyxBridge.csproj     # Swyx.Client.ClmgrAPI v14.21.0 NuGet
│   │   ├── Program.cs            # Entry: [STAThread] + Message-Pump + connect/disconnect dispatch
│   │   ├── Com/
│   │   │   ├── SwyxConnector.cs  # COM Attach-Modus (DispInit = E_NOTIMPL)
│   │   │   ├── LineManager.cs    # Multi-Line: Dial, Hangup, GetAllLines (IClientLineDisp)
│   │   │   └── EventSink.cs     # Typed PubOnLineMgrNotification → JSON-RPC
│   │   │   ├── WindowHook.cs     # SwyxIt!-Fenster dreistufig verstecken (Hook + Timer + Dialog-Killer)
│   │   │   └── SwyxItLauncher.cs # SwyxIt! automatisch hidden starten, Tunnel-Port abwarten
│   │   ├── Handlers/
│   │   │   ├── CallHandler.cs    # JSON-RPC → LineManager Routing
│   │   │   ├── PresenceHandler.cs # Away/DND/Available via dynamic DispClientConfig
│   │   │   ├── HistoryHandler.cs  # Typed CallerCollectionClass/CallerItemClass
│   │   │   ├── VoicemailHandler.cs # dynamic für VoiceMessages Enumeration
│   │   │   └── ContactHandler.cs  # FulltextSearchInContactsEx (SDK-Methode)
│   │   ├── JsonRpc/              # Request/Response/Emitter
│   │   └── Utils/                # Logging, StaDispatcher
│
├── src/
│   ├── shared/
│   │   ├── types.ts              # LineState enum, LineInfo, BridgeState, etc.
│   │   └── constants.ts          # IPC_CHANNELS
│   ├── main/
│   │   ├── index.ts              # Electron main: Window, Bridge, Tray
│   │   ├── tray.ts               # System Tray Integration
│   │   ├── bridge/
│   │   ├── BridgeManager.ts  # Spawnt SwyxBridge.exe, WSL2→Win copy, JSON-RPC I/O
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
│   ├── test-bridge.mjs           # Node.js Test-Script (spawnt Bridge via PowerShell)
│   └── test-dispinit.mjs         # DispInit Standalone-Test (bewiesener E_NOTIMPL)
│
├── resources/                    # App-Icons
├── plugins/                      # Plugin-Verzeichnis (Erweiterbarkeit)
└── tests/
│
├── dist/
│   └── SwyxConnect/              # Fertiges Deployment-Paket
│       ├── SwyxConnect.bat       # Starter-Script
│       ├── LIESMICH.txt          # Anleitung
│       ├── app/                  # Electron-App (main, preload, renderer)
│       ├── bridge/               # SwyxBridge.exe + DLLs
│       └── resources/            # Icons
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

### WSL2 Dev-Environment (WICHTIG — FÜR AGENTEN)

Die Entwicklungsumgebung läuft auf **WSL2 (Ubuntu)**. Windows-Interop ist aktiv:
- SwyxIt!.exe + CLMgr.exe laufen auf der Windows-Seite
- SwyxBridge.exe (Windows .NET 8) wird von WSL2 direkt gestartet
- Electron läuft auf dem WSLg-Display (:0)

**VOLLSTÄNDIGE STARTPROZEDUR:**

```bash
# 1. Projekt (falls nicht vorhanden)
gh repo clone Ralle1976/SwyxConnect /tmp/SwyxConnect
cd /tmp/SwyxConnect

# 2. Dependencies
npm install
npm install ws --no-save   # für CDP-Screenshots

# 3. Electron-App bauen
npx electron-vite build

# 4. C# Bridge für Windows cross-kompilieren
export PATH="$HOME/.dotnet:$PATH"
dotnet publish bridge/SwyxBridge/SwyxBridge.csproj -c Release -r win-x64 --self-contained false -o out/bridge

# 5. Prüfen ob SwyxIt! + CLMgr auf Windows laufen
powershell.exe -Command "Get-Process 'SwyxIt!','CLMgr' -ErrorAction SilentlyContinue | Select Id,ProcessName"

# 6. App starten (Bridge verbindet automatisch mit CLMgr via COM)
pkill -f electron 2>/dev/null
DISPLAY=:0 npx electron out/main/index.js --disable-gpu --no-sandbox --remote-debugging-port=9222 &
sleep 6

# 7. Screenshot via CDP
PAGE_ID=$(curl -s http://localhost:9222/json/list | node -e "process.stdin.on('data',d=>console.log(JSON.parse(d)[0].id))")
node -e "
const WebSocket = require('ws');
const fs = require('fs');
const ws = new WebSocket('ws://127.0.0.1:9222/devtools/page/$PAGE_ID');
ws.on('open', () => ws.send(JSON.stringify({id:1, method:'Page.captureScreenshot', params:{format:'png'}})));
ws.on('message', (data) => {
  const msg = JSON.parse(data);
  if (msg.result?.data) {
    fs.writeFileSync('/tmp/screenshot.png', Buffer.from(msg.result.data, 'base64'));
    console.log('Screenshot saved');
    ws.close();
  }
});
setTimeout(() => process.exit(0), 5000);
"

# 8. App stoppen
pkill -f electron
```

**WICHTIG:**
- ffmpeg X11-Grab liefert schwarzes Bild (Weston-Compositor) → NUR CDP-Screenshots!
- Bridge-Logs erscheinen als `[Bridge Error] Bridge stderr:` — das ist KEIN Fehler, nur stderr-Weiterleitung
- SwyxBridge.exe braucht .NET 8 Runtime auf Windows (installiert: .NET 10)
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
- **UI startet auch auf Linux** (DISPLAY=:0, --disable-gpu, CDP-Screenshots)
- **Forced-Show Fallback**: Fenster erscheint spätestens nach 5s
- **Tastaturkürzel**: F5=Annehmen, F6=Auflegen, F7=Halten, F8=Stumm, Esc=Auflegen
- **Callcenter-Dashboard**: 8 KPI-Karten, Mein Status, Team-Übersicht, Letzte Anrufe
- **Theme Hell/Dunkel/System**: Tailwind v4 @custom-variant dark
- **Kontakte**: DispSearchPhoneBookEntries("") lädt alle
- **Leitungsanzahl**: IPC-Kette Settings → Bridge → DispSetNumberOfLines(n)
- **Audio-Test**: 440Hz Sinuston + Mikrofon-Pegel-Meter
- **SwyxItLauncher**: Auto-Start von SwyxIt!.exe hidden, Port-9094-Polling
- **Deployment-Paket**: dist/SwyxConnect/ mit Starter und Anleitung
- **Version 1.0.0**: Alle Mock-Daten entfernt, 15 deutsche Leitungsstatus-Labels
- **Teams-Präsenz**: Bidirektional via MS Graph API (Azure AD, Device Code Flow)
- **Einstellungen**: Vollständige Persistenz (Settings → IPC → Disk)

### SwyxIt!-Fensterunterdrückung ✅

Dreistufige Eliminierung aller SwyxIt!-Fenster über `WindowHook.cs`:

1. **PROAKTIV**: Beim Start → `RefreshSwyxPids()` findet alle Swyx-Prozesse (SwyxIt!, CLMgr, IpPbxSrv, SkinPhone, etc.) → `ExileAllSwyxWindows()` verschiebt alle Fenster auf (-32000,-32000) mit Größe 0×0.
2. **REAKTIV**: `SetWinEventHook` (EVENT_OBJECT_CREATE, EVENT_OBJECT_SHOW, EVENT_SYSTEM_FOREGROUND) → jedes neue Swyx-Fenster wird sofort via `NukeWindow()` off-screen verschoben + SW_HIDE + WS_VISIBLE entfernt + WS_EX_TOOLWINDOW/WS_EX_NOACTIVATE gesetzt.
3. **DIALOG-KILLER**: Modale Dialoge (Win32 Klasse `#32770`, Titel enthält error/fehler/javascript/script/warnung/warning) werden per `PostMessage(WM_CLOSE)` geschlossen + off-screen verschoben + versteckt. Verhindert dass "JavaScript error occurred"-Dialoge den SwyxIt!-Prozess blockieren.
4. **TIMER-FALLBACK**: WinForms Timer (500ms) ruft `ExileAllSwyxWindows()` auf + `RefreshSwyxPids()` für neu gestartete Prozesse.

**Key Details:**
- `WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS` — Hooks laufen im eigenen Prozesskontext, keine DLL-Injection nötig
- Off-screen Position (-32000,-32000) + Größe 0 ist effektiver als nur SW_HIDE, da auch bei kurzzeitigem WS_VISIBLE das Fenster auf keinem Monitor sichtbar ist
- `PostMessage(WM_CLOSE)` statt `SendMessage` für non-blocking Dialog-Schließung
- PID-Tracking über Prozessnamen-Muster (swyxit, clmgr, ippbxsrv, skinphone, swyx)

### Geplant 📋

- ComSocket.dll Decompilierung → Tunnel-Client ohne SwyxIt!
- electron-builder Packaging (.exe Installer)
- Plugin-System (Erweiterbarkeit)
---

## Umgebung

- **SwyxIt! v14.25.8537.0** (Deutsch, On-Premises CPE)
- **Swyx SDK**: `Swyx.Client.ClmgrAPI` v14.21.0 (NuGet, 291 exportierte Typen)
- **Benutzer**: `Ralf.Arnold@oneqrew.com`, SiteID 1, EntityID 23
- **Swyx Server**: `172.18.3.202` (intern, REST-Ports blockiert)
- **COM CLSID**: `{f8e552f8-4c00-11d3-80bc-00105a653379}` (CLMgr.ClientLineMgr)
- **Entwicklung**: WSL2 + Windows, Bridge-Files auf `C:\temp\SwyxBridge\` kopiert
- **WICHTIG**: `new ClientLineMgrClass()` hängt auf STA-Thread — NUR `Type.GetTypeFromProgID` + `Activator.CreateInstance` verwenden!

---

## Externe Referenzen

- [Swyx Client SDK](https://clientsdk.swyx.engineering/)
- [CLMgrPubTypes.h](https://clientsdk.swyx.engineering/_c_l_mgr_pub_types_8h.html)
- [Swyx CPE Hilfe](https://help.enreach.com/cpe/14.25/App/Swyx/de-DE/index.html)

---

## Standalone-Architektur (OHNE SwyxIt!.exe)

### Überblick

SwyxConnect soll langfristig ohne SwyxIt!.exe funktionieren. Stattdessen:
- **Eigener Kestrel-Host** mit SignalR Hubs (`/hubs/swyxit`, `/hubs/comsocket`)
- **SIPSorcery v10.0.3** als SIP-UA für REGISTER/INVITE/BYE
- **WCF-Client** für CDS (Configuration Data Store) auf net.tcp Port 9094
- **JSON-RPC** Methoden: `startStandaloneHost`, `stopStandaloneHost`, `getStandaloneHostStatus`, `probeNetwork`

### Standalone-Dateien

```
bridge/SwyxBridge/Standalone/
├── Interfaces.cs              # DI-Interfaces: ILineManagerProvider, ILineManagerFacade, IClientConfig, etc.
├── SipLineManagerProvider.cs   # In-Memory Line Management + SipClientConfig
├── SipUserAgent.cs            # SIPSorcery SIP UA Wrapper (REGISTER, INVITE, BYE)
├── StandaloneKestrelHost.cs   # ASP.NET Core Kestrel + DI + SignalR Hubs
├── StubServices.cs            # Stub-Implementierungen für Phase 1
├── SwyxConnectHub.cs          # SignalR Hub + ComSocket-Compat Hub
└── NetworkProbe.cs            # 359 Zeilen, TCP/UDP/SIP/HTTPS/WCF/RemoteConnector-Analyse
```

### CDS-Protokoll (aus Decompilation IpPbxCDSClientLib.dll)

#### Transport
- **Protokoll**: WCF `net.tcp` Binding
- **Standard-Port**: 9094 (CDS), 9100 (Windows-Login)
- **Lokal**: `net.pipe://localhost/ConfigDataStore/...` (Named Pipes)
- **Remote**: `net.tcp://{host}:9094/ConfigDataStore/...`

#### Authentifizierungs-Modi (URL-Suffixe)
```
Kerberos/Trusted/TrustedLocal/TrustedPlain → .wnd
UsernamePassword                           → .upwd
Plain                                      → .wupwd
JasonWebToken (JWT)                        → .jwt2
```

#### Login-Flow
1. Login-Channel erstellen: `net.tcp://{host}:9094/ConfigDataStore/CLoginImpl.none`
   - NetTcpBinding, SecurityMode=Transport, TcpClientCredentialType=None
   - ProtectionLevel=EncryptAndSign
   - EndpointIdentity: DNS "IpPbx"
   - Zertifikat: SCertificateManager.CertificateValidator (custom X509)
2. `ILogin.AcquireToken(Credentials{UserName, Password})` → `AuthenticationResult{AccessToken, RefreshToken, UserId}`
3. AccessToken wird als JWT in allen weiteren Anfragen verwendet
4. Refresh: `ILogin.RefreshToken(refreshToken)` → neues `AuthenticationResult`
5. Weitere Operationen: AuthenticationMode=JasonWebToken, URL-Suffix=`.jwt2`

#### Windows-Login (Port 9100)
- WSHttpBinding, SecurityMode=TransportWithMessageCredential
- URL: `https://{host}:9100/ippbx/CLoginWindowsImpl`
- MessageCredentialType=Windows (NTLM/Kerberos)
- EstablishSecurityContext=false, NegotiateServiceCredential=false

#### CDS Service-Endpunkte
```
ConfigDataStore/CLoginImpl                  (Login, AcquireToken, RefreshToken)
ConfigDataStore/CPhoneClientFacadeImpl       (Phone Client Operations)
ConfigDataStore/CCallbackFacadeImpl           (Change Notifications, Subscriptions)
ConfigDataStore/CUserEnumImpl                 (User Management)
ConfigDataStore/CGlobalConfigEnumImpl          (Global Config, RemoteConnector Config)
ConfigDataStore/CAdminFacadeImpl               (Admin Operations)
ConfigDataStore/CIppbxServerFacadeImpl         (Server Operations)
ConfigDataStore/CFilesFacadeImpl               (File Operations)
ConfigDataStore/CPublicNumberEnumImpl          (Public Numbers)
ConfigDataStore/CInternalNumberEnumImpl        (Internal Numbers)
ConfigDataStore/CPortManagerFacadeImpl          (Port Manager)
ConfigDataStore/CGroupEnumImpl                  (Groups)
ConfigDataStore/CLocationEnumImpl               (Locations)
ConfigDataStore/CFeatureProfileEnumImpl         (Feature Profiles)
ConfigDataStore/CUserPhoneBookEnumImpl           (Phone Book)
ConfigDataStore/CEditablePhonebookEnumImpl       (Editable Phone Book)
ConfigDataStore/CDcfFacadeImpl                   (DCF)
ConfigDataStore/CReportingFacadeImpl             (Reporting)
ConfigDataStore/CRoleEnumImpl                    (Security Roles)
```

#### ILogin WCF-Interface (vollständig)
```csharp
interface ILogin {
  void Ping();
  string[] GetSupportedClientVersions();
  void CheckVersion();
  UserCredentialsAuthenticationResult Login(Credentials credentials);
  AuthenticationResult AcquireToken(Credentials credentials);
  FirstFactorAuthenticationResult AcquireFirstFactorToken(Credentials credentials);
  AuthenticationResult AcquireTokenByTwoFactors(TwoFactorCredentials credentials);
  AuthenticationResult RefreshToken(string token);
  ValidateAccessTokenResponse ValidateAccessToken(ValidateAccessTokenRequest request);
  FederatedAccessTokenValidationResult GetFederatedAccessTokenValidationResult(string accessToken);
  TenantFederationInfo GetTenantInfo();
  UserPasswordResetRequestResult CreateUserPasswordResetRequest(...);
}
```

#### RemoteConnector-Konfiguration
```csharp
class TRemoteConnectorConfig {
  TEndPoint AuthenticationEndpoint;       // z.B. RC0321.axxess.de:8021
  TEndPoint AuthenticationFallbackEndpoint;
  TEndPoint ConnectorEndpoint;            // z.B. RC0321.axxess.de:15021
  TEndPoint ConnectorFallbackEndpoint;
  bool Enabled;
  int CertificateMode;
  bool SystemManagedCertificateSecurity;
  string RootCertificateThumbprint;
  string ServerCertificateThumbprint;
  string ClientCertificateThumbprint;
}

class TEndPoint {
  string Protocol;  // "https", "net.tcp"
  string Host;      // Server-Hostname
  string Path;      // URL-Pfad
  int Port;         // Port-Nummer
}
```

#### TenantInfo (aus ComSocket)
```csharp
class TenantInfo {
  int TenantId;
  int UserId;
  string ClientID;
  string TenantDomain;
  string AppUriId;
  string ConnectorEndpoint;      // RemoteConnector tunnel
  int[] ConnectorPorts;
  string FallbackConnectorEndpoint;
  string AuthenticationEndpoint;  // RemoteConnector auth
  int[] AuthenticationPorts;
  string OemId;
  bool TwoFactorAuthEnabledRequired;
  string TenantAuthenticationDomain;
  string TenantName;
  string UserPasswordResetUrl;
}
```

### Netzwerk-Probe Ergebnisse

#### CLMgr lokale Ports (bei laufendem SwyxIt!)
| Port | Protokoll | Status |
|------|-----------|--------|
| 9094/TCP | CDS (WCF net.tcp) | OPEN — kein Standard-WCF-Preamble |
| 9100/TCP | Windows-Login (WSHttp) | OPEN |
| 5060/UDP | SIP (CLMgr Proxy) | OPEN — ignoriert OPTIONS |
| 5070/UDP | SIP (extern) | OPEN |
| 40000-40009/UDP | RTP Media | OPEN (10 Kanäle) |
| 12042/TCP | Unbekannt | OPEN |

#### CLMgr Tunnel-Verbindung
```
TCP 192.168.178.130:65054 → 193.192.0.54:15021 (RemoteConnector Tunnel)
CLMgr exponiert lokal: CDS auf :9094, SIP auf :5060, RTP auf :40000-40009
```

#### Öffentlicher Server (RC0321.axxess.de)
| Port | Status | Befund |
|------|--------|--------|
| 15021/TCP | OPEN | Proprietärer Tunnel — kein TLS, kein SIP |
| 8021/TCP | OPEN | Microsoft-HTTPAPI/2.0, /IpPbx/* → 503 |

### SIP REGISTER Probe-Ergebnisse

#### Ergebnis (SIP/UDP auf localhost:5060, User 'Ralf Arnold')
```
Responses: 2 (100 Trying → 403 Forbidden)
Status: SIP/2.0 403 Forbidden
Warning: 399 172.18.3.202 "access denied"
User-Agent: Swyx IpPbxSrv/14.25 (Swyx.Core_14.25_20251125.1)
Path: <sip:127.0.0.1:5060;transport=udp;lr>
```

#### Erkenntnis: SIP Auth Flow
SwyxWare nutzt **KEIN** Standard SIP Digest Auth (401 Challenge-Response).
Stattdessen ist der korrekte Ablauf:

1. **CDS Login** (`net.tcp://localhost:9094/ConfigDataStore/CLoginImpl.none`)
   - `AcquireToken(Credentials{UserName, Password})` → `AuthenticationResult{AccessToken, RefreshToken, UserId}`
2. **SIP-Credentials abrufen** (`IPhoneClientFacade.GetSipCredentials(userId)`)
   - Liefert: `SipRealm`, `SipUserID`, `SipUserName`, `SipPasswordHash`
3. **SIP REGISTER** mit pre-shared Credentials aus CDS
   - Ohne vorherige CDS-Auth → `403 Forbidden` (kein Challenge, kein WWW-Authenticate)

#### Decompiled: TUserSipCredentialsShort
```csharp
// Namespace: SWConfigDataClientLib.WSPhoneClientFacade
public class TUserSipCredentialsShort {
    public string SipRealm { get; set; }      // Digest realm
    public string SipUserID { get; set; }      // SIP URI user part
    public string SipUserName { get; set; }    // Display name
    public int UserID { get; set; }            // CDS user ID
}
```

#### Decompiled: IPhoneClientFacade (Auszug)
```csharp
// WCF Endpoint: net.tcp://localhost:9094/ConfigDataStore/PhoneClientFacadeImpl.*
[ServiceContract]
interface IPhoneClientFacade {
    TUserSipCredentialsShort GetSipCredentials(int userId);
    int GetCurrentUserID();
    int GetCurrentUserName(out string UserName);
    ServerInfo GetServerInfo();
    string GetSwyxAccessToken();
    FeatureProfile GetFeatureProfile(int userId);
    // ... 60+ weitere Methoden
}
```

### Nächste Schritte (Standalone)

1. ~~**CDS WCF Login implementieren**~~ ✅ Ping OK, 31 Versionen
2. **CDS AcquireToken** — Login mit Username + Password (Passwort via UI)
3. **GetSipCredentials** — SIP-Credentials vom CDS holen (nach Login)
4. **SIP REGISTER mit Auth** — Pre-shared Credentials aus Schritt 3 verwenden
5. **RemoteConnector Auth** — HTTPS auf :8021 für Zugang ohne VPN
6. **Electron Frontend** an SignalR Hubs anbinden
