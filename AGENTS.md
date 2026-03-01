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

### KRITISCHE ERKENNTNIS: Standalone-Init = UNMÖGLICH

**Alle drei Init-Methoden des CLMgr COM-Objekts sind E_NOTIMPL:**

| Methode | Interface | Ergebnis |
|---|---|---|
| `PubInit(server)` | `IClientLineMgrPub` (IUnknown vtable) | `NotImplementedException` |
| `PubInitEx(server, backup)` | `IClientLineMgrPub2` (IUnknown vtable) | `NotImplementedException` |
| `DispInit(server)` | `IClientLineMgrDisp` (IDispatch) | `E_NOTIMPL (0x80004001)` |

Der Standalone-Modus ist komplett deaktiviert — sowohl IUnknown als auch IDispatch Level.
COM-Zugriff funktioniert NUR im Attach-Modus mit SwyxIt!.exe als Host-Prozess.

**Konsequenz**: SwyxIt!.exe wird temporär gestartet, baut den Tunnel auf, und wird dann gekillt.
CLMgr.exe hält den Tunnel eigenständig. SwyxConnect nutzt COM im Attach-Modus.

```
DECISION: PubInit/PubInitEx/DispInit = ALLE E_NOTIMPL — Standalone unmöglich
DECISION: Kill-after-tunnel = SwyxIt! starten → Tunnel warten → SwyxIt! killen → CLMgr hält Tunnel
```

### Tunnel-Architektur (Verifiziert)

```
1. SwyxBridge startet SwyxIt!.exe hidden (WindowHook unterdrückt alle Fenster)
2. SwyxIt! initialisiert CLMgr.exe mit PbxServer-Adresse
3. CLMgr liest Registry-Konfiguration + Client-Zertifikat aus Windows Cert Store
4. CLMgr authentifiziert via HTTPS beim RemoteConnector-Auth-Server
5. CLMgr öffnet TCP-Tunnel zum RemoteConnector (proprietäres Binärprotokoll)
6. Tunnel proxied: CDS auf :9094, SIP auf :5060, RTP auf :40000-40009
7. SwyxBridge killt SwyxIt!.exe — CLMgr hält Tunnel eigenständig
8. SwyxConnect nutzt COM im Attach-Modus über CLMgr
```

### Registry-Konfiguration (CLMgr Tunnel)

```
HKCU\Software\Swyx\SwyxIt!\CurrentVersion\Options:
  PbxServer          = "{SWYX_SERVER}"        (interner Server)
  PublicAuthServerName = "{REMOTE_CONNECTOR}:8021"  (Auth HTTPS)
  PublicServerName   = "{REMOTE_CONNECTOR}:15021"   (Tunnel TCP)
  ConnectorUsage     = 0|1|2                   (0=Auto, 1=Immer, 2=Nie)

Windows Certificate Store (CurrentUser\My):
  Client-Zertifikat erforderlich (Issuer: CN=SwyxRoot, O=SwyxWare)
```

### Decompilierung-Ergebnisse

| DLL | Typ | Ergebnis |
|---|---|---|
| ComSocket.dll | .NET (SignalR) | Kein Tunnel-Code — nur Wrapper über CLMgr COM |
| IpPbxCDSWrap.dll | C++/CLI mixed-mode | Native Stubs — kein Tunnel-Code zugänglich |
| IpPbxCDSClientLib.dll | .NET (WCF) | RC-Config lesen — kein Tunnel-Aufbau |
| Interop.CLMgr.dll | .NET (COM Interop) | Vollständige COM-Schnittstelle — keine Tunnel-Methoden |
| CLMgr.exe | Native C++ (14MB) | **Tunnel ist hier** — nicht decompilierbar mit .NET-Tools |

---

## Reverse Engineering: CLMgr.exe (Native x86 Disassembly)

### Übersicht

**Ziel**: CLMgr.exe ist die zentrale native Komponente, die den RemoteConnector-Tunnel,
CDS-Verbindungen und SIP/CSTA verwaltet. Da alle drei COM-Init-Methoden E_NOTIMPL sind,
wurde CLMgr.exe mit radare2 disassembliert, um die interne Architektur zu verstehen und
langfristig eine Standalone-Lösung ohne SwyxIt!.exe zu ermöglichen.

**Tool**: radare2 6.1.0 (manuell installiert auf WSL2, kein sudo verfügbar)
**Binary**: CLMgr.exe — 14.149.528 Bytes, PE32 x86, 1520 Imports, 0 Exports
**PDB-Pfad**: `C:\a\1\b\Win32\Release\CLMgr.pdb` (Azure DevOps Build, November 2025)
**Kompiliert**: 25. November 2025

### Analysemethodik

- `aaa` (Full Analysis) ist bei 14MB nicht praktikabel (Timeout)
- Stattdessen: `pd N @ addr` für gezieltes Disassembly + `/x` für Hex-Pattern-Suche
- Methodennamen extrahiert über String-Suche in PE-Strings (250+ Methoden gefunden)
- Funktionsadressen gefunden via `push <address>` Binary Search (zuverlässigste Methode)
- `scr.color=0` für saubere Ausgabe ohne ANSI-Escapes

### Disassemblierte Funktionen

#### CCLineMgr::Init (0x4c225f) — Server-Erkennung
- Prüft ob Server-Adresse gesetzt, sonst ruft `GetServerFromAutoDetection`
- Setzt `[edi+0x34]` = CDS-Adresse
- Delegiert an `CCLineMgr::InitEx`

#### CCLineMgr::InitEx (0x4d3d7c) — Haupt-Initialisierung
- Initialisiert inneres CLineMgr-Objekt `[edi+0x70]`
- Ruft `CClientConfigBase::CDSConnect` für CDS-Verbindung
- Bei Fehler: HRESULT in `[edi+0x68]` gespeichert
- Delegiert an `CLineMgr::InitUser` für Session-Aufbau

#### CClientConfigBase::CDSConnect (0x62b5a2) — CDS-Verbindung
- Baut WCF-Verbindung zum CDS auf
- Nutzt `ClientCDS::Connect` (0x602b3b) als Kern-Routine

#### ClientCDS::Connect (0x602b3b) — Vollständiger Auth-Flow (~7KB)

**Die zentrale CDS-Verbindungsfunktion. Vollständig disassembliert.**

Funktionssignatur: `ret 0x1c` = 7 DWORD-Parameter:
- `[ebp+8]` = Trusted-Flag (Byte) — steuert zwei verschiedene Auth-Pfade
- `[ebp+0xc]` = Servername (BSTR)
- `[ebp+0x10]` = Username (BSTR)
- `[ebp+0x14..0x20]` = weitere Parameter

**Trusted Path (Windows Auth):**
1. Ruft `LoginWithFederatedAccount` → Ergebnis-Codes:
   - 2: "LoginWithFederatedAccount succeeded" ✅
   - 1: E_ABORT (0x80004004)
   - 3: "No IpPbx user found" (0x80090322)
   - 4: "User is locked" (0x80070533)
   - 5: "ServiceUnavailable" (0x800706ba)
   - 6: "Not Allowed" (0x80070032)
   - 7: "Not Licensed" (0x80040112)

**Non-Trusted Path (Username/Password):**
1. Ruft `LoginWithCurrentWindowsAccount` → zusätzliche Codes:
   - 2: "Invalid Credentials" (0x80070005)
   - 3: "User's password expired"
   - 6: "Login with user login and password is not allowed." (0x8007052f)
   - 7: "Missing credentials. Password Reset Request." (0x80097019)
   - 8: "Login with display name ist not supported." (0x80070523)

**Post-Auth Sequenz:**
1. "Get PhoneClient Facade" → `[edi+0xec]`
2. "Get Files Facade" → `[edi+0xf0]`
3. "Get UserPhoneBook Enum" → `[edi+0x100]`
4. Port 0x7d00 (32000) — Verbindung zum "Client Line Manager" Service
5. "CheckCDSVersion" / "###### Skipping CheckClientVersion!"
6. "CheckClientVersion"
7. "TrimmingWorkingSet" → `SetProcessWorkingSetSize(-1,-1)`
8. "Done" → return `al=1` (Erfolg) oder `al=0` (Fehler)

#### CLineMgr::InitUser (0x56b553) — Session-Aufbau
- Ruft SIP/CSTA-Session-Initialisierung (0x5ca9eb) — noch nicht vollständig disassembliert
- Konfiguriert Leitungen und Event-Handling

#### CLineMgr::ReInit (0x5705a5) — Tunnel-Neustart
- Loggt "CLineMgr::ReInit" und "Try to re-initialize line manager after a server down phase"
- Prüft `[edi+0x10b0]` und `[edi+0x10b1]` Flags
- Ruft Worker-Funktion `0x56f5cb` wenn Flag gesetzt
- Ruft vtable `[eax+0xbc]` mit Parameter -1
- Kleine Funktion, `ret 8` (2 Parameter)

#### CCLineMgr::GetSelectedLine — Leitungsauswahl
- Liest aktuelle Leitungsnummer aus Objekt-Offset
- Einfache Accessor-Funktion

### Tunnel-Architektur (aus Strings + RTTI)

#### Klassen-Hierarchie
```
CTunnelConnector (TryStart, Stop, OnStatusChange)
├── CTunnelConnCfg (GetCertFromCDS, StoreCertificate)
├── CConnectorClient (TunnelConnected, TunnelStarted, TunnelDisconnected,
│                     OnTunnelConnectEvent, OnTunnelDiscEvent)
├── CClientTunnel (Start, Stop, StartTunnelMgr, OnOutConnected,
│                  OnTunnelConnectedEvent, OnDisconnected, OnTLSConnectionTimeout)
│   └── CClientTunnelMgr (Start, Stop, SendMsg, DecodeMsg, ProcessMsg)
│       ├── ProcessKeepAlive, ProcessDisableData, ProcessEnableData
│       ├── ProcessConfigClient, ProcessConToMux, ProcessDiscFromMux
│       ├── ProcessDiscTCP, ProcAllocRTPPortPairs
│       ├── ProcessConnectRTP, ProcessDisconnectRTP
│       ├── OnNewTCPConnection, OnTCPInConnected, OnTCPConnectEvent
│       └── OnDisconnected, OnDisconnectEvent, OnKeepAliveTimeout
├── CReactorSocket → CTCPReactorSocket, CTLSReactorSocket, CUDPReactorSocket
├── CMultiplexer (CMuxConnector)
└── CStreamModule
```

#### Tunnel-Zustände
```
Idle → Disconnected → Connecting → Connected → Started (oder Failed/NoConfig)
```

#### Tunnel-Nachrichtentypen (15+ proprietäres Binärprotokoll)
```
SCTunnelMsg, SCKeepAliveMsg, SCDisableDataMsg, SCEnableDataMsg,
SCClientVersionMsg, SCTCPConnectMsg, SCTCPDisconnectMsg,
SCRTPPortPairsMsg, SCConnectToMuxMsg, SCDisconnectFromMuxMsg,
SCConfigureClientMsg, SCConnectRTPMsg, SCUpdateRTPMsg,
SCDisconnectRTPMsg, SCAllocateRTPPortPairsMsg, SCDisconnectTCPMsg
```

#### CTunnelConnector::TryStart (~0x5b46f7)
- Nimmt `[ebp+8]` und `[ebp+0xc]` als Parameter
- Ruft Validierung via `0x48e896`
- Erstellt Lock auf `[edi+0x64]`, liest Tunnel-Config von `[edi+0x58]`
- Ruft `CTunnelConnCfg::StoreCertificate` — speichert Zertifikat-Thumbprint
- Delegiert an `0x973920` (CClientTunnel::Start)

#### CClientTunnel::Start (0x974ee0 Bereich)
- Prüft `[edi+0x8310]` — wenn null, allokiert 0xC4 Bytes → neues Tunnel-Objekt via `0x6bf1f1`
- Erstellt ReactorContainer (Fehler loggt "Could not create ReactorContainer")
- `ret 0x18` = 6 DWORD-Parameter

### Netzwerk-Layer (aus IAT-Analyse)

#### Statisch gelinkte Bibliotheken
- **OpenSSL**: TLS-Verschlüsselung für Tunnel. Quellpfad: `..\\openssl\\ssl\\rio\\rio_notifier.c`
- **reSIProcate**: SIP/CSTA Stack. Quellpfad: `C:\\a\\1\\s\\rutil\\FdPoll.cxx`
- **WinHTTP**: NUR für Proxy-Konfiguration (5 Funktionen: WinHttpOpen, GetProxyForUrl, etc.)

#### IAT Call-Trace
```
WSASocketA (0xd30904): 2 Aufrufe bei 0x7d9626 und 0x7d9645
  Quelle: openssl/ssl/rio/rio_notifier.c — OpenSSL RIO Notifier
  Erster Aufruf mit flags=0x80 (WSA_FLAG_OVERLAPPED), Retry mit flags=0

WSASend (0xd30950): 2 Aufrufe bei 0xbc4933 und 0xc0143e
  Generischer Socket-Send-Wrapper, nutzt [edi+0x2c] als Socket-Handle

WSARecv (0xd30928): 1 Aufruf bei 0xc0116e
  Nutzt [edi+0x48] als Socket-Handle, Error 997 (WSA_IO_PENDING) ist erwartet

WSAPoll (0xd308fc): 1 Aufruf bei 0x77053c
  Quelle: C:\a\1\s\rutil\FdPoll.cxx — reSIProcate FdPoll
```

### COM Factory Pattern

CLMgr verwendet ATL Singleton Class Factory:
```
CComClassFactorySingleton<CClientLineMgr>  — Singleton-Pattern
CComClassFactorySingleton<CClientLineMgrQA> — QA-Variante
CComCoClass<CClientLineMgr, &CLSID_ClientLineMgr>
CComAggObject<CClientLineMgr>              — unterstützt Aggregation
CComObject<CClientLineMgr>                 — Standard-Objekt
```

### Objekt-Layout (CLineMgr / CCLineMgr / CClientTunnel)

```
# CCLineMgr/CClientLineMgr Objekt (this = edi)
[edi+0x34]    — CDS-Adresse (String)
[edi+0x68]    — HRESULT Fehlercode (gesetzt von CDSConnect bei Fehler)
[edi+0x70]    — Inneres CLineMgr-Objekt (Pointer)
[edi+0xb4]    — Connected-Flag (Byte)
[edi+0xb5]    — Auth-Flag (Byte, geprüft bei Federated Login)
[edi+0xb8]    — Sub-Objekt (geschrieben nach erfolgreichem Login)
[edi+0xd0]    — Sub-Objekt (Login-Ergebnis)
[edi+0xe8]    — CDS-Client Sub-Objekt (LibManager)
[edi+0xec]    — PhoneClient Facade
[edi+0xf0]    — Files Facade
[edi+0xf4]    — Weiteres Facade
[edi+0x100]   — UserPhoneBook Enum
[edi+0x10b0]  — ReInit Flag 1
[edi+0x10b1]  — ReInit Flag 2
[edi+0x10c]   — Logging ID
[edi+0x1588]  — Critical Section (ReInit)
[edi+0x1c0]   — Verbindungsinfo Sub-Objekt

# CClientTunnel Objekt
[edi+0x74]    — Tunnel Logging ID
[edi+0x8310]  — Tunnel Manager Pointer (CClientTunnelMgr)

# CTunnelConnector Objekt
[edi+0x58]    — Tunnel-Konfiguration
[edi+0x60]    — Tunnel-ID (Logging)
[edi+0x64]    — Lock-Objekt
```

### Quellpfad-Hinweise
```
C:\a\1\b\Win32\Release\CLMgr.pdb        — Azure DevOps Build
C:\a\1\s\rutil\FdPoll.cxx               — reSIProcate Library
..\openssl\ssl\rio\rio_notifier.c        — OpenSSL RIO
C:\a\1\s\Shared Components\uaCSTASipConnector\CstaSessionHandler.cpp
```

### Machbarkeitsanalyse: Standalone ohne SwyxIt!.exe

#### Fazit

Der Tunnel ist tief in CLMgr.exe eingebettet:
- OpenSSL TLS (statisch gelinkt, ~300KB Code)
- reSIProcate SIP/CSTA Stack (statisch gelinkt)
- Proprietäres binäres Tunnel-Protokoll (15+ Nachrichtentypen)
- Zertifikatsverwaltung über CDS
- Multiplexer für TCP/UDP/RTP
- Reactor Pattern mit asynchronen Events

**Empfehlung**: Kill-after-tunnel bleibt Produktions-Architektur.

**Experimenteller Ansatz (R&D)**:
1. C++ Programm schreiben: `LoadLibrary("CLMgr.exe")` → `GetProcAddress("DllGetClassObject")`
2. ATL Class Factory nutzen um `CClientLineMgr` zu erstellen
3. `Init()` → `InitEx()` → `CDSConnect()` Kette aufrufen
4. Falls Tunnel startet → kein SwyxIt!.exe nötig

### CSTA Session-Architektur (0x5ca9eb und Umgebung)

Die SIP/CSTA-Funktionalität ist in der Klasse `SCstaSession` gekapselt.
88+ Methoden identifiziert, darunter:

**Session-Lifecycle:**
- `SCstaSession::Init` (0x5c0d9e) — Speichert Config-Pointer in `[edi+0x74]`, `ret 4`
- `SCstaSession::SetOptions` (0x5ca9eb) — Setzt CSTA-Optionen, ruft `0x689e49` mit Param 4, delegiert an `[edi+0x78]` via `0x5bbe10`, `ret 4`
- `SCstaSession::StartSession` (0x5cad46) — Prüft Session-State `[ebx+0x1c8]`:
  - State 0 oder 1: Session starten (Hauptpfad)
  - State 5 (e_SessionRetryPending): Loggt "Session is currently in state e_SessionRetryPending --> return"
  - State 3 oder 4 (stopping): Loggt "Session is stopping.", setzt State auf 4
  - Sonst: Loggt "Session cannot be started"
- `SCstaSession::StopSession` — Beendet aktive Session
- `SCstaSession::MakeSessionRetry` — Retry-Logik nach Verbindungsabbruch
- `SCstaSession::GetValidLoginId` — Login-ID für SIP-Registrierung

**Anruf-Steuerung (CSTA-Operationen):**
- `auxMakeCall`, `auxConsultationCall`, `auxClearConnection`
- `auxHoldCall`, `auxRetrieveCall`, `auxAnswerCall`
- `auxDeflectCall`, `auxAlternateCall`, `auxReconnectCall`
- `auxTransferCall`, `auxSingleStepTransferCall`, `auxConferenceCall`
- `auxSnapShotDevice`, `auxGetLineState`, `auxSelectLine`

**Event-Handling (CSTA-Events):**
- `HandleCstaOriginatedEvent`, `HandleCstaDeliveredEvent`
- `HandleCstaEstablishedEvent`, `HandleCstaConnClearedEvent`
- `HandleCstaHeldEvent`, `HandleCstaRetrievedEvent`
- `HandleCstaTransferredEvent`, `HandleCstaConferencedEvent`
- `HandleCstaDivertedEvent`, `HandleCstaNetworkReachedEvent`
- `MapEvent2Line`, `MapDeliveredEvent2NotificationCall`
- `SetLineState`, `FindFreeLine`, `SelectLineOnFirstCall`

**Session-Handler:** `CstaSessionHandler` aus `C:\a\1\s\Shared Components\uaCSTASipConnector\CstaSessionHandler.cpp`
— SIP/CSTA-Connector der über reSIProcate den SIP-Stack anspricht.

### AutoDetection (0x4c9895) — Server-Erkennung

Funktion: `CCLineMgr::LookupServerNamesInitially`

**Ablauf:**
1. **DHCP-Abfrage** via `CCLineMgr::RetrieveServerNamesFromDhcp` (0x4dd0e2)
   - Ergebnis 0: "DHCP supported, retrieved addresses" → Flags `[edi+0x1afd/1afe]` = 1
   - Ergebnis 1: "DHCP supported, but retrieved no addresses" → Flag `[edi+0x1afd]` = 1
   - Ergebnis 0x80070078: "DHCP not supported" → Flags bleiben 0

2. **DNS-Abfrage** via `CCLineMgr::RetrieveServerNamesFromDNS` (0x4dcd42)
   - Ergebnis 0: "DNS supported, retrieved addresses" → Flags `[edi+0x1b00/1b01]` = 1
   - Ergebnis 1: "DNS supported, but retrieved no addresses" → Flag `[edi+0x1b00]` = 1
   - Ergebnis 0x80070078: "DNS not supported" → Flags bleiben 0

**DNS SRV Record Lookup-Kette (aus Strings):**
```
SDnsQuery::lookupNAPTR   → NAPTR Record
SDnsQuery::lookupSRV     → SRV Records:
  _sips._udp.{domain}    → SIPS über UDP
  _sips._tcp.{domain}    → SIPS über TCP
  _sip._dtls.{domain}    → SIP über DTLS
  _sip._tcp.{domain}     → SIP über TCP
  _sip._udp.{domain}     → SIP über UDP
SDnsQuery::lookupARecords → A Records für aufgelöste Hosts
```

**Objekt-Offsets (AutoDetection):**
```
[edi+0x1acc]  — Server-Adress-Objekt (DHCP)
[edi+0x1ae4]  — Server-Adress-Objekt (DNS)
[edi+0x1afc]  — DHCP-Detection aktiviert (Byte)
[edi+0x1afd]  — DHCP erfolgreich Flag (Byte)
[edi+0x1afe]  — DHCP Adressen gefunden Flag (Byte)
[edi+0x1aff]  — DNS-Detection aktiviert (Byte)
[edi+0x1b00]  — DNS erfolgreich Flag (Byte)
[edi+0x1b01]  — DNS Adressen gefunden Flag (Byte)
[edi+0x1b28]  — Detection-Status (DWORD)
[edi+0x1b2c]  — Detection-Ergebnis Flag 1 (Byte)
[edi+0x1b2d]  — Detection-Ergebnis Flag 2 (Byte)
[edi+0x17c]   — Logging ID
```

### Noch zu disassemblieren

| Adresse | Funktion | Status |
|---------|----------|--------|
| 0x973920 | CClientTunnel::Start (vollständig) | Teilweise |
| 0x4dcc49 | RetrieveServerAddressesFromDhcp (innere Funktion) | Ausstehend |
| 0x5cec9b | SCstaSession interne Setup-Funktion (aus StartSession) | Ausstehend |
### radare2 Nutzung (Referenz für Agenten)

```bash
# Wrapper-Script (enthält alle Umgebungsvariablen)
/home/tango/.local/r2/r2.sh -q -e 'bin.relocs=false' -e 'scr.color=0' \
  -c 'BEFEHLE' "/mnt/c/Program Files (x86)/Swyx/SwyxIt!/CLMgr.exe"

# WICHTIG: Full Analysis (aaa) funktioniert NICHT bei 14MB Binary (Timeout)
# Stattdessen: pd N @ addr + /x hex für gezielte Analyse
```
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
- **Benutzer**: `{SWYX_EMAIL}`, SiteID 1, EntityID 23
- **Swyx Server**: `{SWYX_SERVER}` (intern, REST-Ports blockiert)
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
  TEndPoint AuthenticationEndpoint;       // z.B. {REMOTE_CONNECTOR}:8021
  TEndPoint AuthenticationFallbackEndpoint;
  TEndPoint ConnectorEndpoint;            // z.B. {REMOTE_CONNECTOR}:15021
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
TCP {LOCAL_IP}:65054 → {RC_PUBLIC_IP}:15021 (RemoteConnector Tunnel)
CLMgr exponiert lokal: CDS auf :9094, SIP auf :5060, RTP auf :40000-40009
```

#### Öffentlicher Server ({REMOTE_CONNECTOR})
| Port | Status | Befund |
|------|--------|--------|
| 15021/TCP | OPEN | Proprietärer Tunnel — kein TLS, kein SIP |
| 8021/TCP | OPEN | Microsoft-HTTPAPI/2.0, /IpPbx/* → 503 |

### SIP REGISTER Probe-Ergebnisse

#### Ergebnis (SIP/UDP auf localhost:5060, User '{SWYX_USER}')
```
Responses: 2 (100 Trying → 403 Forbidden)
Status: SIP/2.0 403 Forbidden
Warning: 399 {SWYX_SERVER} "access denied"
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
