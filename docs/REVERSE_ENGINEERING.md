# SwyxIt! Reverse Engineering Analyse

> ⚠️ **NICHT COMMITTEN** — Nur für lokale Entwicklungszwecke.
> Ziel: Vollständiges Verständnis aller Binaries, DLLs, COM-Interfaces und des Tunnel-Mechanismus.

---

## 1. Installation: Vollständige Binary-Inventur

### Pfad: `C:\Program Files (x86)\Swyx\SwyxIt!\`

### Executables (EXEs)

| Datei | Größe | Funktion |
|---|---|---|
| **CLMgr.exe** | 14.1 MB | Client Line Manager — COM-Server, Kern der Telefonie |
| **SwyxIt!.exe** | 10.3 MB | GUI-Hauptanwendung (.NET 4.5.1) |
| **SwyxMessenger.exe** | 159 MB | Moderner Messenger (Electron/WebView2-basiert!) |
| **CallRoutingMgr.exe** | 2.0 MB | Call-Routing-Verwaltung |
| **IMClient.exe** | 3.6 MB | Instant Messaging Client |
| **IpPbxOutlookAccess.exe** | 0.2 MB | Outlook-Integration Bridge |
| **CoreAudioConfig.exe** | 0.05 MB | Audio-Gerätekonfiguration |
| **SwyxIt!.Setup.exe** | 84.9 MB | Installer |

### Kern-DLLs (Telefonie & COM)

| Datei | Größe | Funktion |
|---|---|---|
| **Interop.CLMgr.dll** | 209 KB | .NET COM-Interop-Assembly (32-bit!) — **Vollständige API-Definition** |
| **IpPbx.Client.Plugin.ComSocket.dll** | 5.5 MB | Modernes COM-Socket-Plugin (ASP.NET Core, SignalR) |
| **IpPbx.Client.Plugin.ComSocket.tlb** | 65 KB | TypeLibrary für ComSocket |
| **IpPbxCDSClientLib.dll** | 5.5 MB | Config Data Store Client — Auth, RemoteConnector, SIP |
| **IpPbxCDSWrap.dll** | 11.3 MB | CDS Wrapper (größte DLL) |
| **IpPbxCDSSharedLib.dll** | 295 KB | Shared CDS Definitionen |
| **ClientShare.dll** | 2.0 MB | Geteilte Client-Bibliothek |
| **CallRoutingMgr.dll** | 3.2 MB | Call-Routing-Logik |
| **CallRoutingMgrRes.dll** | 1.3 MB | Call-Routing-Ressourcen |

### Sicherheit & Kryptographie

| Datei | Größe | Funktion |
|---|---|---|
| **secman.dll** | 172 KB | Security Manager (32-bit) |
| **secman64.dll** | 187 KB | Security Manager (64-bit) |
| **SecurityManager.2005.dll** | 84 KB | AddinExpress Security Manager |
| **SecurityManager.2005.Design.dll** | 62 KB | Design-Time Security |
| **IpPbxTracing.dll** | 57 KB | Tracing/Logging |
| **IpPbxWin32.dll** | 56 KB | Win32-Hilfsfunktionen |

### Plugins (Audio-Geräte)

| Datei | Funktion |
|---|---|
| Plugins/JabraAudioDevice.dll + JabraSDK.dll + libjabra.dll | Jabra Headset-Integration |
| Plugins/PlantronicsDevice.dll + PlantronicsDeviceEventSink.dll | Plantronics/Poly Headset |
| Plugins/H360DevicePlugin.dll | Logitech H360 |
| Plugins/P230DevicePlugin.dll / P250 / P280 | Weitere Telefone |
| Plugins/AudioVolumeControl.dll | Lautstärkeregelung |
| Plugins/GenericDevicePlugin.dll | Generisches Audio-Plugin |
| Plugins/IpPbx.Client.Office.Uc.*.dll | Office UC-Integration |
| Plugins/Interop.AudioDevicePlugin.dll | Audio-Plugin COM-Interop |
| Plugins/Interop.AudioVolumeControl.dll | Volume COM-Interop |

### Drittanbieter-DLLs

| Datei | Funktion |
|---|---|
| AddinExpress.MSO/OL.2005.dll | Office-Addin-Framework |
| NAudio.dll | .NET Audio-Bibliothek |
| Newtonsoft.Json.dll | JSON-Serialisierung |
| ffmpeg.dll | Audio/Video-Codec |
| libsndfile-1.dll | Audiodatei-Verarbeitung |
| WebView2Loader.dll | Chromium WebView2 |
| d3dcompiler_47.dll + libEGL/libGLESv2 + vk_swiftshader + vulkan-1 | GPU/Rendering |
| Xceed.Compression/FileSystem/Zip.dll | ZIP-Komprimierung |
| Microsoft.Identity.Client*.dll | MSAL Authentifizierung |
| Microsoft.IdentityModel.*.dll | JWT Token-Verarbeitung |
| SPLicense.dll | Lizenzierung |
| uhs02.dll | USB HID Support |

### Common Files: `C:\Program Files\Common Files\Swyx\`

| Datei | Größe | Funktion |
|---|---|---|
| **CLMgrPs64.dll** | 654 KB | CLMgr Proxy/Stub DLL (64-bit COM-Marshalling) |
| **IpPbxScriptSign.dll** | 4.5 MB | Script-Signierung |
| **IpPbxSrvPs.dll** | 330 KB | Server Proxy/Stub |

---

## 2. COM-Registrierung

### TypeLibraries

| TypeLib ID | Version | Name | Pfad |
|---|---|---|---|
| `{F8E552F7-4C00-11D3-80BC-00105A653379}` | 2.0 | **CLMgr 2.0 Type Library** | CLMgr.exe |
| `{C6BB14C2-E69E-4CEB-923C-15F869F8C685}` | 1.0 | SwyxWebExtensionLib | IpPbx.VisualGroups.WebExtension.ocx |
| `{F8E5533F-4C00-11D3-80BC-00105A653379}` | 1.0 | IpPbxScriptSign | IpPbxScriptSign.dll |

### Haupt-ProgIDs (30+)

```
KERN-OBJEKTE:
  CLMgr.ClientLineMgr         {F8E552F8-...}  ← Das Root-COM-Objekt
  CLMgr.ClientLine            {F8E552F9-...}  ← Leitungs-Objekt
  CLMgr.ClEventTarget         {F8E552FA-...}  ← Event-Target
  CLMgr.ClientConfig          {F8E554CC-...}  ← Konfiguration/Präsenz
  CLMgr.ClientLineMgrQA       {F8E555FB-...}  ← QA-Variante

ANRUF-DATEN:
  CLMgr.CallerCollection      {F8E554EB-...}  ← Anrufer-Liste
  CLMgr.CallerItem            {F8E554E9-...}  ← Einzelner Anrufer
  CLMgr.ClCallHistCollection  {F8E553E3-...}  ← Anruf-Historie
  CLMgr.ClCallHistItem        {F8E553E5-...}  ← Historie-Eintrag
  CLMgr.RedialCollection      {F8E554EF-...}  ← Wahlwiederholung
  CLMgr.RedialItem            {F8E554ED-...}  ← Wahlwiederholungs-Eintrag

CHAT:
  CLMgr.ChatClient            {F8E5558D-...}  ← Chat-Client
  CLMgr.ChatClientList        {F8E5558C-...}  ← Chat-Teilnehmer
  CLMgr.ChatMessage           {F8E5558E-...}  ← Chat-Nachricht
  CLMgr.ClChatMessageItem     {F8E55424-...}  ← Chat-Nachricht (alt)

NUMMERN & SUCHE:
  CLMgr.ClientNumber          {F8E554CF-...}  ← Rufnummer-Objekt
  CLMgr.ClientNumberCollection {F8E554D1-...} ← Rufnummer-Liste
  CLMgr.NameNumberSearchResult {F8E55544-...} ← Suchergebnis
  CLMgr.NameNumberSearchResultCollection      ← Suchergebnis-Liste

SIP:
  CLMgr.SIPAccountItem        {F8E554D9-...}  ← SIP-Konto
  CLMgr.SIPAccountCollection  {F8E554DB-...}  ← SIP-Konten-Liste
  CLMgr.SIPProviderItem       {F8E554D5-...}  ← SIP-Provider
  CLMgr.SIPProviderCollection {F8E554D7-...}  ← SIP-Provider-Liste

VIDEO:
  CLMgr.VideoSession          {f8e555ee-...}  ← Video-Sitzung
  CLMgr.VideoLink             {f8e555f1-...}  ← Video-Link

SONSTIGES:
  CLMgr.CollaborationLink     {4A816D68-...}  ← Collaboration
  CLMgr.FileEntry/Collection  {F8E5552E/30}   ← Datei-Verwaltung
  CLMgr.LoggedInDeviceItem    {F8E55596-...}  ← Angemeldetes Gerät
  CLMgr.LoggedInDevicesCollection              ← Geräte-Liste
  CLMgr.NameKeyDataEntryWrapper                ← Namenstas ten-Wrapper
```

---

## 3. Vollständige COM-Interface-Hierarchie (aus Interop.CLMgr.dll)

### IClientLineMgrDisp (Dispatch/Automation Interface — was SwyxConnect nutzt)

```
KERN-FUNKTIONEN:
  DispInit(ServerName) → int
  DispInitEx(ServerName, BackupServer) → int
  DispRegisterUser(UserName) → int
  DispReleaseUser(userId) → void
  DispNumberOfLines → int (Property)
  DispSetNumberOfLines(n) → int
  DispGetLine(iLineNumber) → IDispatch
  DispSelectedLineNumber → int (Property)
  DispSelectLineNumber(n) → int
  DispSelectedLine → IDispatch (Property)
  DispSelectLine(pLine) → int
  DispSwitchToLineNumber(n) → int

WÄHLEN:
  DispSimpleDial(dialstring) → void
  DispSimpleDialEx(dialstring) → uint
  DispSimpleDialEx2(dialstring, lineNum) → uint
  DispSimpleDialEx3(dialstring, lineNum, bProcessNumber, name) → uint

KONFERENZ:
  DispCreateConference(iConferenceLine) → void
  DispJoinAllToConference(bCreate) → void
  DispConferenceRunning → int (Property)
  DispConferenceLine → IDispatch (Property)
  DispConferenceLineNumber → int (Property)
  DispJoinLineToConference(iLine) → void

AUDIO:
  DispMicroEnabled → int (Property, get/set)
  DispSpeakerEnabled → int (Property, get/set)
  DispAudioMode → PubCLMgrAudioMode (Property, get/set)
  DispOpenListening → int (Property, get/set)
  DispVolume[iMode] → int (Property, get/set)
  DispCaptureVolume[iMode] → int (Property, get)
  DispHandsetAvailable/HeadsetAvailable/HandsfreeAvailable → int
  DispHandsetDevices/HeadsetDevices/... → Object (Gerätelisten)
  DispPreferredHandsetDevice/HeadsetDevice/... → string (get/set)
  DispStartSoundFile/StopSoundFile → set-only
  DispStartRecording/StopRecording → set-only
  DispRecordingLevel/Dimension → int (get)

RTP-AUDIO:
  DispPlayToRtp(fullPath, bLoop, dwPause) → void
  DispStopPlayToRtp() → void
  DispRecordFromRtp(fullPath, bAppend, bAddLocal) → void
  DispStopRecordFromRtp() → void
  PlaySoundFileDxEx/Pause/Continue/Stop/Rewind/GetPosition/SetPosition/Skip

NOTIFICATION:
  DispPickupGroupNotificationCall(lineNum) → void
  DispNotificationCallPeerNumber/Name → string
  DispNotificationCallCalledExtension/Name → string
  DispNotificationCallRedirectedFromNumber/Name → string
  DispNotificationCallWasRedirected → int
  GetNotificationCallByRefId(callRefId, details) → void
  GetNotificationCallRefIds() → Object

CALLBACK ON BUSY:
  DispGetCallbackOnBusyNotifyPeerName/Number → string
  DispPickupCallbackOnBusyNotification(lineNum) → void
  DispRejectCallbackOnBusyNotification() → void
  DispRequestCallbackOnBusy(peerNumber, peerName) → void

CHAT:
  DispRegisterChatMessageReader() → uint
  DispSendChatMessage(messageId, text, peerName, peerIP) → void
  DispReadChatMessage(readerID) → Object
  DispAcknowledgeChatMessage(messageId, peerName, peerIP) → void
  DispUnRegisterChatMessageReader(readerID) → void

SKIN/UI-STEUERUNG:
  DispExternalHookStateChanged(hookState, hookDeviceId) → void
  DispSkinPhoneCommand(commandId, buttonId) → void
  DispSkinGetActionAreaState(commandId, buttonId) → uint
  DispSkinGetInfoDetail(detailIndex) → string
  DispPostMessage(message, wParam, lParam) → void

SERVER:
  DispIsServerUp → int
  DispAutoDetectionEnabled/ServerAvailable → int
  DispAutoDetectionPrimaryServer/BackupServer → string
  DispGetCurrentServer → string
  DispGetCurrentUser → string
  DispCountryCode/AreaCode/PublicAccessPrefix/... → string
  DispBlockDialString → string (get/set)

EXTENSIONS & SPEED-DIALS:
  DispNumberOfExtensions → int
  DispGetExtension(index) → string
  DispNumberOfSpeedDials → int
  DispSpeedDialName/Number/State(index) → string/int

NUMMERN:
  DispResolveNumber(number) → string (name)
  DispConvertNumber(style, numberFrom) → string

VOICEMAIL:
  DispVoicemailRemoteInquiry() → void

KONTAKTE:
  GetContactDataPlugIns() → Object
  GetContactByID(pluginID, contactID) → Object
  SearchContacts(pluginID, searchString) → Object
  GetSearchResultCount(pluginID, searchString) → uint
  ShowContactEx(contactId, pluginID, name, number, createIfNotExists) → int
  FulltextSearchInContactsEx(text, phonebook, plugins, numbers) → Object

NAMEKEYS:
  MaxNameKeyCount → uint (get/set)
  GetNameKey(index, useCache) → Object
  SetNameKey(index, apply, val) → void

MEDIA STREAMING:
  DispCreateMediastreamingLink() → string
  DispDeleteMediastreamingLink(linkId) → void

CLIENT CONFIG:
  DispClientConfig → IDispatch (Property) ← Präsenz/Voicemail/History

LOGIN & REGISTRIERUNG:
  RegisterUserEx(server, backup, user, pass, authMode, ctiMaster, usernames) → void
  RegisterUserEx4UC(server, backup, user, pass, authMode, ctiMaster, usernames, statusNames) → void
  RegisterUserConnector4UC(connConfig, certConfig, publicServer, publicBackup,
                           thumbprint, server, backup, user, pass, authMode,
                           ctiMaster, usernames, statusNames) → void  ← REMOTE CONNECTOR!
  ReleaseUserEx() → void
  ChangePbxPassword(oldPass, newPass) → void
  DispIsLoggedIn → int
  DispIsLoggedInAsCtiMaster → int
  DispGetCurrentAuthMode → int
  DispDeviceSessionID → string

REMOTE CONNECTOR:
  GetRcEndpoints(authServer, fallbackAuth, rcServer, fallbackRc) → void
  SaveRcClientCertificate(certificate, password) → void
  GetCertificateThumbprint(thumbprint) → void
  CloudConnectorServer → string
  CloudConnectorStatus → int

CTI:
  CstaPhonePairingList(listName, listId) → void
  StartCstaSession() → int
  StopCstaSession() → void
  StartCstaMonitor(deviceId) → void
  StopCstaMonitor() → void
  IsCstaMonitorStarted() → int
  GetCstaPairing() → string
  SaveCstaPairing(deviceId) → void
  IsDcf2CstaPairing() → int
  SaveCtiPairing(deviceId) → void
  get_DispCtiSettings() → CTI-Einstellungen
  put_DispCtiSettings() → CTI-Einstellungen setzen
  SaveCtiSettings() → void
  ValidateCtiSettings(number) → PubCLMgrCtiValidation

PRESENCE (via IClientPresenceInformation):
  GetUserPresenceInfo(id, userId, siteId, username, status, statusText) → void
  SubscribeUserPresence(cookie, userId, siteId) → void
  UnsubscribeUserPresence(cookie, userId, siteId) → void
  UnsubscribeAll(cookie) → void
  GetOwnEmailAddress() → string
  GetStatusNames() → Object
  LookupUserEmailAddress(id) → void

INSTANT MESSAGING (via IClientLineMgrInstantMessaging):
  CanChatTo(remoteSiteId, remoteUserId) → void
  OpenChatTo(remoteSiteId, remoteUserId) → void
  ShowChatApplication() → void
  Prepare(startIM) → void
  StartModernApp() → void
  PopUpModernApp() → void

FEDERATION / CLOUD:
  GetTenantFederationInfo(server, federationType, header, connected, passwordResetUrl) → void
  GetFederatedAccessToken(json, accessToken, accountId) → void
  GetFederatedAccessTokenEx(json, trySilent, accessToken, accountId) → void
  GetAccessTokens(userId, currentToken, swyxwareToken) → void

SONSTIGES:
  SwyxItVersionInfo → Object
  IsTerminalServer() → int
  UseBlockDialingOnly() → int
  CloseSwyxIt() → void
  LoadModernSkin(skinType) → void
  GetInstalledLanguage() → string
  IsMessagingAvailable() → int
  SwitchToClassic() → void
  OpenCallRouting() → void
  ShowCallControl() → void
  DispSetDialerMode(enable, deviceUri) → void
  LoggedInDevicesEnumerator → Object
  InvokeVoicemailAction() → void
  EnableNotifyUserAppearanceChanged() → void
  OnUserChanging/OnUserChanged() → void
  IsClientPopUpAndNotificationAllowed() → int
  OpenClientUiDialog(dialogId) → void
  DoneWithModalUiDialog(dialogId) → void
  SendClientRegisterRequest(entity) → void
  GetUserPhoneBookStatus(siteId, userId) → (status, freeText)
  GetUserPhoneCallbackEnumerator() → Object
  GetUserIdByPhoneNumber(phone) → (siteId, userId)
  GetUserAppearances() → Object
  AnalyticsAddEvent/AddEventText() → void
```

### IClientLineMgr2..14 (Native C++ Interface-Versionen)

Die gleichen Methoden wie oben, aber als C++-Interfaces statt COM Automation:
- `IClientLineMgr2` — Basis: Init, GetLine, SelectLine, RegisterUser
- `IClientLineMgrEx` — Audio-Geräte
- `IClientLineMgrEx2` — ResolveNumber, ConvertNumber
- `IClientLineMgrEx3` — InitEx (Backup-Server), AutoDetection
- `IClientLineMgrEx4` — Konferenz
- `IClientLineMgrEx5` — Skin-Steuerung
- `IClientLineMgrEx6` — Callback-on-Busy, Chat, CTI-Pairing
- `IClientLineMgrEx7` — GroupNotification erweitert
- `IClientLineMgrEx8` — Volume, WaveDevicesEx, Audio PnP
- `IClientLineMgrEx9` — Journal/Contact AddIn
- `IClientLineMgrEx10` — PlaySoundFileDxEx
- `IClientLineMgrEx11` — ClientConfig, TrialMode
- `IClientLineMgrEx12` — FulltextSearch
- `IClientLineMgrEx13` — CoreAudioApi
- `IClientLineMgrEx14` — NamekeyStatesEx, StopRingingSound

### Events

```
IClientLineMgrEventsDisp:
  DispOnLineMgrNotification(int msg, int param)

IClientLineMgrEventsPub:
  PubOnLineMgrNotification(int msg, int param)

IClientPresenceInformationEvents:
  OnSubscribedUserPresenceStateChanged(userId, siteId, username, status, statusText)
```

---

## 4. RemoteConnector / Tunnel-Mechanismus

### Architektur

```
┌──────────────────────────────────────────────────────────────┐
│  SwyxIt! / SwyxConnect                                       │
│  ┌──────────────┐     ┌────────────────────┐                │
│  │  CLMgr.exe   │────►│ IpPbxCDSClientLib  │                │
│  │  (COM-Server)│     │ (CDS Client)       │                │
│  │  OpenSSL     │     │ Auth0Client        │                │
│  │  TLS Tunnel  │     │ RemoteConnectorCfg │                │
│  └──────┬───────┘     └────────┬───────────┘                │
│         │                      │                             │
│    Port 9101               Port 9094 (TCP)                   │
│    (Auth TLS)              Port 8094 (HTTP)                  │
│         │                      │                             │
└─────────┼──────────────────────┼─────────────────────────────┘
          │                      │
          ▼                      ▼
┌──────────────────────────────────────────────────────────────┐
│  SwyxServer ([SWYX_SERVER])                                   │
│  ┌────────────────┐   ┌──────────────────┐                  │
│  │ Authentication │   │  CDS Service     │                  │
│  │ Service        │   │  (Config Data    │                  │
│  │ Port 9101      │   │   Store)         │                  │
│  └────────────────┘   └──────────────────┘                  │
└──────────────────────────────────────────────────────────────┘
```

### Beteiligte Komponenten

1. **CLMgr.exe** — Enthält embedded OpenSSL (`SSL_*`, `TLSConnectTimer`)
   - Baut den eigentlichen TLS-Tunnel auf
   - Verwaltet den Tunnel-Lifecycle (`TLSConnectTimer`)

2. **IpPbxCDSClientLib.dll** — Konfiguration & Authentifizierung
   - `RemoteConnectorConfig` — Tunnel-Konfiguration
   - `Auth0.App` / `Auth0Client` / `Auth0ClientApp` — OAuth2/OIDC via Auth0
   - `LoginWithCredentials` — Benutzername/Passwort-Login
   - `LoginWithCurrentWindowsAccount` — Windows-Auth (nur LAN)
   - `CertificateThumbprint` — Zertifikat-Identifikation
   - `SIP*` — SIP-Stack-Konfiguration
   - `ChannelFactory` — WCF-Kanäle

3. **secman.dll / secman64.dll** — Zertifikats-/Schlüsselverwaltung

4. **CLMgrPs64.dll** — 64-bit Proxy/Stub für Cross-Process COM-Marshalling

### Schlüssel-Methoden für RemoteConnector

```csharp
// 1. RC-Endpunkte vom Server abfragen
GetRcEndpoints(
    out string publicAuthServer,      // z.B. "connect.firma.de:9101"
    out string publicFallbackAuth,
    out string publicRcServer,        // RC-Tunnel-Server
    out string publicFallbackRcServer
);

// 2. Zertifikat speichern
SaveRcClientCertificate(
    object Certificate,   // X509-Zertifikat
    string Password
);

// 3. Zertifikat-Thumbprint abrufen
GetCertificateThumbprint(out object thumbprint);

// 4. Volle RC-Registrierung (KERNMETHODE)
RegisterUserConnector4UC(
    int connectorConfig,           // Verbindungsmodus
    int certificateConfig,         // Zertifikats-Modus
    string PublicServerName,       // Öffentlicher Auth-Server
    string PublicBackupServerName,
    ref object thumbprint,         // Zertifikat-Thumbprint
    string ServerName,             // Interner Server
    string BackupServerName,
    string PbxUserName,
    string Password,
    int authenticationMode,        // 0=Windows, 1=UserPass
    int ctiMaster,
    out string Usernames,
    object statusNames
);

// 5. Status prüfen
CloudConnectorServer → string   // Aktiver Cloud/RC-Server
CloudConnectorStatus → int      // 0=Offline, 1=Connected, ...
```

### Authentifizierungs-Flow

```
1. Client holt RC-Endpunkte (GetRcEndpoints)
2. Client präsentiert Zertifikat (SaveRcClientCertificate) oder
   nutzt Auth0 (cloud-basiert)
3. TLS-Handshake auf Port 9101 mit Authentifizierungs-Service
4. Bei Erfolg: Tunnel aufgebaut, CLMgr registriert sich beim Server
5. RegisterUserConnector4UC() statt RegisterUserEx()
6. Ab hier: normale COM-API funktioniert durch den Tunnel hindurch

Authentifizierungsmodi:
  - Zertifikat-basiert (traditionell, On-Premises)
  - Auth0/OIDC (cloud-basiert, neuere Versionen)
  - Username + Password (immer verfügbar)
  - Windows-Auth (NUR im LAN, nicht über RC)
```

### Ports

| Port | Protokoll | Funktion |
|---|---|---|
| **9101** | TCP/TLS | RemoteConnector Authentication Service |
| **9094** | TCP | CDS (Config Data Store) Kommunikation |
| **8094** | HTTP | CDS HTTP-Zugang |
| **9092** | TCP | Reporting Service |

### Config-Einstellungen (CLMgr.exe.config)

```xml
<setting name="ClientCertFile" value="IpPbx.cer" />
<setting name="DefaultTCPPort" value="9094" />
<setting name="DefaultHTTPPort" value="8094" />
<setting name="DefaultReportingTCPPort" value="9092" />
<setting name="RequestTimeOut" value="7500" />
```

### Security-Einstellungen (runtime)

```xml
<!-- Erzwingt starke Kryptographie -->
<AppContextSwitchOverrides value="
  Switch.System.ServiceModel.DisableUsingServicePointManagerSecurityProtocols=false;
  Switch.System.Net.DontEnableSchUseStrongCrypto=false
" />
```

---

## 5. ComSocket Plugin — Die moderne API

Das `IpPbx.Client.Plugin.ComSocket.dll` ist eine **ASP.NET Core-Anwendung**, die als Plugin in CLMgr läuft. Es bietet eine **modernere, JSON-basierte API** über SignalR/WebSocket.

### Architektur

```
CLMgr.exe
  └── lädt ComSocket Plugin
        ├── ASP.NET Core Host (Kestrel)
        ├── SignalR Hub (WebSocket)
        ├── Event-Distribution (ClMgrEventDistributor)
        ├── Services:
        │   ├── CallRecordings
        │   ├── SwyxMessenger
        │   └── VisualContactsService (DATEV-Integration!)
        └── COM-Interop ← → CLMgr COM-API
```

### Verfügbare Datenmodelle

```
LineInfo           — Id, Label, State, IsDefaultLine, OutgoingExtension
LineDetails        — Voller Anruf-Detail (PeerName/Number, CallId, Encryption, HD-Audio, ...)
ForwardingConfig   — Unconditional/Busy/NoReply Weiterleitung
AudioModes         — Handset/Headset/Handsfree Verfügbarkeit
AudioVolumes       — Lautstärke pro Modus
CallJournalItemDto — Anruf-Journal mit Callback-Status
VoiceMessageItemDto — Voicemail mit Transcription-Status
CallRecording      — Aufnahme mit Datei-Pfad
SpeedDialDto       — Kurzwahl mit Entity/Bitmap/DirectCall
PhoneBookEntryDto  — Telefonbuch mit Status/EntityType
NotificationCallDetails — Gruppenruf-Details
VersionInfo        — SwyxIt/ComSocket Versionen
NetPromoterScoreConfig — NPS-Konfiguration
UserQuotaInfo      — Voicemail-Quota
MissedCallId       — Verpasste Anrufe
ContactData        — Vollständiger Kontakt (70+ Felder!)
DatevCallData      — DATEV-Integration
SwyxUserAppearance — Benutzer-Avatar/Präsenz
```

### Events (über SignalR)

```
NotifyLineStateChanged          — Leitungsstatus
NotifyLineDetailsChanged        — Anruf-Details
NotifyLineSelectionChanged      — Leitung ausgewählt
NotifyNumberOfLinesChanged      — Anzahl Leitungen
NotifyAudioModeChanged          — Audio-Modus
NotifyAudioVolumeChanged        — Lautstärke
NotifyCallRecordingAdded        — Neue Aufnahme
NotifyCallRecordingChanged      — Aufnahme geändert
NotifyCallRecordingRemoved      — Aufnahme gelöscht
NotifyCallRecordingPathChanged  — Aufnahme-Pfad
NotifyCtiStateChanged           — CTI-Status
NotifyNotificationCallsChanged  — Gruppenrufe
NotifyPlaySoundFileStateChanged — Sound-Wiedergabe
NotifyUserDataChanged           — Benutzer-Daten
SwyxServerConnectionStateChanged — Server-Verbindung
UnreadChatMessageCountChanged   — Chat-Nachrichten
```

### RemoteConnector-Awareness

```csharp
// ContactStoreApiSettings enthält:
bool IsRemoteConnectorActive    // Ob RC aktiv ist
bool IgnoreRemoteConnectorState // RC-Status ignorieren
```

---

## 6. Was unsere Bridge nutzt vs. was verfügbar ist

### ✅ Aktuell genutzt (Bruchteil)

```
COM-Objekt-Erstellung:
  Type.GetTypeFromCLSID({F8E552F8-...}) + Activator.CreateInstance()
  ← CLSID: {f8e5536b-4c00-11d3-80bc-00105a653379} (aus AGENTS.md)
  ← Tatsächlich: {F8E552F8-4C00-11D3-80BC-00105A653379}

Leitungen:
  DispNumberOfLines, DispGetLine(i), DispSelectedLine
  DispSelectLineNumber, DispSwitchToLineNumber

Anrufe:
  DispSimpleDialEx3, DispHookOn, DispHookOff
  Line: DispState, DispPeerName, DispPeerNumber
  Line: DispCallerName, DispCallerNumber
  Line: DispCallId, DispIsOutgoingCall, DispConnectionStartTime
  Line: DispHold, DispActivate, DispDial, DispForwardCall, DispSendDtmf

Präsenz:
  DispClientConfig.Away, .DoNotDisturb
  cfg.SetRichPresenceStatus(str)
  cfg.PublicateDetectedAwayState(bool)

History:
  cfg.CallerEnumerator (Property "Time")

Voicemail:
  cfg.VoiceMessagesEnumerator
  cfg.NumberOfNewVoicemails

Events:
  PubOnLineMgrNotification(msg, param)
```

### ❌ Nicht genutzt (MASSIVES ungenutztes Potenzial)

```
KONFERENZ:
  DispCreateConference, DispJoinAllToConference, DispJoinLineToConference

AUDIO-STEUERUNG:
  DispAudioMode, DispVolume, DispMicroEnabled, DispSpeakerEnabled
  Alle Device-Enumeratoren und -Selektoren

RTP PLAYBACK/RECORDING:
  DispPlayToRtp, DispRecordFromRtp, PlaySoundFileDxEx

CALLBACK ON BUSY:
  DispRequestCallbackOnBusy, DispPickupCallbackOnBusyNotification

CHAT / INSTANT MESSAGING:
  DispRegisterChatMessageReader, DispSendChatMessage
  ShowChatApplication, OpenChatTo, CanChatTo

CTI:
  StartCstaSession, StartCstaMonitor, CstaPhonePairingList
  SaveCtiPairing, DispCtiSettings

KONTAKTE / TELEFONBUCH:
  SearchContacts, FulltextSearchInContactsEx, ShowContactEx
  GetContactDataPlugIns, GetContactByID

SPEED-DIALS / NAMEKEYS:
  DispNumberOfSpeedDials, DispSpeedDialName/Number/State
  MaxNameKeyCount, GetNameKey, SetNameKey

NUMMERN:
  DispResolveNumber, DispConvertNumber
  DispCountryCode, DispAreaCode, ...

SERVER:
  DispIsServerUp, DispAutoDetection*
  DispGetCurrentServer, DispGetCurrentUser

REMOTE CONNECTOR:
  RegisterUserConnector4UC (!!!)
  GetRcEndpoints, SaveRcClientCertificate
  CloudConnectorServer, CloudConnectorStatus

PRESENCE-SUBSCRIPTIONS:
  SubscribeUserPresence, GetUserPresenceInfo
  OnSubscribedUserPresenceStateChanged (Event!)

FEDERATION / CLOUD:
  GetFederatedAccessToken, GetAccessTokens

GROUP NOTIFICATIONS:
  DispPickupGroupNotificationCall
  GetNotificationCallByRefId

MEDIA STREAMING:
  DispCreateMediastreamingLink

LOGGED-IN DEVICES:
  LoggedInDevicesEnumerator

VERSION/STATUS:
  SwyxItVersionInfo, IsTerminalServer
```

---

## 7. Dependency-Graph

```
SwyxConnect (Electron)
  └── JSON-RPC stdin/stdout
        └── SwyxBridge.exe (.NET 8)
              └── COM Interop (late binding via IDispatch)
                    └── CLMgr.exe (.NET 4.5.1, 32-bit COM-Server)
                          ├── Interop.CLMgr.dll (COM Type Definitions)
                          ├── IpPbxCDSClientLib.dll (Server-Kommunikation)
                          │     ├── WCF Channels → Port 9094 (TCP)
                          │     ├── HTTP → Port 8094
                          │     ├── RemoteConnector → Port 9101 (TLS)
                          │     └── Auth0 → Cloud OAuth2
                          ├── IpPbxCDSWrap.dll (CDS Wrapper)
                          ├── IpPbx.Client.Plugin.ComSocket.dll
                          │     ├── ASP.NET Core / Kestrel
                          │     ├── SignalR Hub
                          │     └── DATEV/VisualContacts Integration
                          ├── ClientShare.dll (Shared Logic)
                          ├── CallRoutingMgr.dll + .exe
                          ├── secman.dll (Zertifikat-Mgmt)
                          ├── CLMgrPs64.dll (64-bit Marshalling)
                          ├── NAudio.dll (Audio)
                          ├── ffmpeg.dll (Codec)
                          └── Plugins/*.dll (Headset-Treiber)

SwyxIt!.exe (GUI)
  └── COM Client von CLMgr.exe (gleiche Interfaces)

SwyxMessenger.exe (Electron/WebView2)
  └── Kommuniziert mit ComSocket Plugin (SignalR?)
```

---

## 8. Strategische Erkenntnisse

### Alternative Integration: ComSocket statt reinem COM

Das `IpPbx.Client.Plugin.ComSocket.dll` bietet eine **modernere API** über SignalR/WebSocket.
SwyxMessenger.exe (159MB Electron-App!) nutzt wahrscheinlich genau diesen Weg.

**Möglichkeit**: Statt COM → JSON-RPC → Electron könnten wir direkt an den
ComSocket SignalR-Hub connecten und hätten:
- Modernere API mit JSON-Datenmodellen
- Native Events (kein Polling nötig)
- Vollständige Anruf-Details (LineDetails mit Encryption, HD-Audio, etc.)
- Call Recordings, Forwarding, Phonebook, DATEV-Integration
- RemoteConnector-Awareness

**Risiko**: Der ComSocket-Port ist nicht dokumentiert und könnte sich ändern.

### RemoteConnector für SwyxConnect

Um SwyxConnect remote-fähig zu machen:
1. `GetRcEndpoints()` aufrufen → öffentliche Server-Adressen
2. `RegisterUserConnector4UC()` statt `RegisterUserEx()` nutzen
3. Zertifikatsfluss implementieren (SaveRcClientCertificate)
4. Port 9101 muss am Server offen sein

### Fehlende CLSID-Korrektur

AGENTS.md referenziert: `{f8e5536b-4c00-11d3-80bc-00105a653379}`
Registry zeigt: `{F8E552F8-4C00-11D3-80BC-00105A653379}` = CLMgr.ClientLineMgr

→ Die CLSID in AGENTS.md könnte veraltet sein oder sich auf eine andere Sub-Schnittstelle bezieht.

---

## 5. Tiefe RE-Erweiterung (2026-07-13)

### CLMgr.exe ist nativ C++ (nicht .NET)

`ilspycmd` kann CLMgr.exe nicht dekompilieren — es ist nativ C++. Stattdessen wurden 115.624 ASCII-Strings extrahiert und systematisch durchsucht.

### Bewiesene Erkenntnis: SwyxIt! ist nur eine GUI-Skin

CLMgr.exe enthält die **komplette Telefonie-Stack**:
- **SIP:** `SwSIP`, `SwSIPReg`, `SwSIPSub`, `SIPEP`
- **Media:** `MSRtp`, `MSCodec`, `MSDTMF`, `MSEcho`, `MSMediaMgr`, `MSJB` (Jitter Buffer)
- **Network:** `TCPSock`, `UDPSock`, `TLSSock`, `STUN`
- **Tunnel:** `Tunnel`, `TunnelMgr`, `TLSConnectTimer`
- **Login:** `DispInit`, `RegisterUserEx`, `RegisterUserConnector4UC`

SwyxIt!.exe enthält **nur OpenSSL-TLS-Strings** (für HTTPS/Cert-Validierung) und UI-Code. Es hat **keine eigene Telefonie-Stack**.

**Schlussfolgerung:** SwyxIt! is NOT needed if DispInit is called correctly. The audio plugins are loaded by CLMgr itself via ReadPlugins.

### RemoteConnector / Tunnel-Mechanismus (bewiesen funktionsfähig)

**Wie der TLS-Tunnel funktioniert:**

```
Client (CLMgr.exe)
  │
  ├── Port 9101 (Auth TLS) ──→ Authentication Service (SwyxServer)
  ├── Port 9094 (TCP)     ──→ CDS Service (Config Data Store)
  ├── Port 8094 (HTTP)    ──→ CDS HTTP-Zugang
  └── Port 9092 (TCP)     ──→ Reporting Service
```

**RegisterUserConnector4UC** baut den Tunnel auf:
1. TLS-Handshake mit Auth-Service auf dem Public-Server
2. Authentifizierung (Password oder Zertifikat)
3. Tunnel wird etabliert
4. CLMgr registriert sich beim Server
5. Ab hier: normale COM-API funktioniert durch den Tunnel

**Signatur (verifiziert 2026-07-13):**
```csharp
RegisterUserConnector4UC(
    int connectorConfig,           // 1 = use RemoteConnector
    int certificateConfig,         // 0 = no cert (password auth)
    string PublicServerName,       // e.g. "server.domain.de:15021"
    string PublicBackupServerName,
    ref object thumbprint,         // null for password auth
    string ServerName,             // internal server
    string BackupServerName,
    string PbxUserName,
    string Password,
    int authenticationMode,        // 1 = Password
    int ctiMaster,                 // 1 = CTI master
    out string Usernames,
    object statusNames);
```

### EnablePowerDialMode (aus CLMgr-Strings)

`EnablePowerDialMode` (Registry: `HKLM\SOFTWARE\Swyx\Client Line Manager\CurrentVersion\Options`):
- **0** = Standard mode (CTI-Slave-Events werden an SwyxIt! verteilt)
- **1** = Power dial mode ("We are CTI master → don't do any line stuff")

CLMgr-String bei `EnablePowerDialMode=1`:
```
"Ignore, we are in power dial mode resp. not logged on"
"We are CTI master / power dial mode -> don't do any line stuff"
```

Das bedeutet: Bei PowerDial-Mode überspringt CLMgr die Line-Event-Verteilung an CTI-Slaves (SwyxIt!). Das ist relevant wenn man SwyxIt! laufen lässt aber dessen Popup verhindern will.

### CTI-Slave-Architektur

CLMgr verteilt Line-Events an registrierte "CTI-Slaves":
```c
enum ECtiSlaveTypes {
    e_CtiSlaveSwyxIt,           // Klassischer SwyxIt! Client
    e_CtiSlaveSwyxPhone,        // SwyxPhone Hardware
    e_CtiSlaveControlledDevice, // Externe Geräte
    e_CtiSlaveControlledNumber, // Externe Nummern
    e_CtiSlaveUaCsta            // UC/CSTA-Monitoring
};
```

Der Server weist jedem User einen CTI-Slave-Typ zu. Bei `e_CtiSlaveSwyxIt` werden Call-Notifications an SwyxIt! geroutet, was das Popup auslöst.

### HandleCallPopup (SwyxIt! Classic)

SwyxIt!-Strings zeigen den Popup-Mechanismus:
```
HandleCallPopup > LineBecameActive: ...
HandleCallPopup > PopUpWhenActive: ...
HandleCallPopup > Client popup on initiating calls is enabled
HandleCallPopup > Client popup for incoming calls is allowed
HandleCallPopup > SwyxIt! Classic is not minimized > no action required
```

**`IsClientPopUpAndNotificationAllowed`** fragt CLMgr, welches `ClientPopupMode` vom Server (`ClientCDS::GetClientPopupMode`) holt. Das ist die offizielle Server-seitige Einstellung.

### ComSocket.dll (dekompiliert — 3.212 .cs-Dateien)

`IpPbx.Client.Plugin.ComSocket.dll` ist **managed .NET** und wurde erfolgreich dekompiliert.

**ModernApp.cs:**
- `ExecutableName = "SwyxIt!.UI"` — startet die neue Electron-basierte Modern UI
- Pfad aus Registry: `HKCU\SOFTWARE\Swyx\SwyxIt!.Modern\InstallPath`

**AppRunner.cs:**
- Watchdog der alle 5 Sekunden prüft ob registrierte Apps laufen
- Startet sie neu falls sie gestorben sind (außer `PreventRestart` ist gesetzt)

### Authentifizierungs-Modi

| Modus | authMode | Beschreibung |
|---|---|---|
| None | 0 | Anonym / SwyxIt!Now |
| Password | 1 | Standard Username + Password |
| WebServiceTrusted | 2 | Single Sign-On (spezifische Server-Konfigurationen) |

### .NET-Bridge als SwyxIt!-Ersatz

Die `SwyxItSuppressor`-Komponente in unserer Bridge:
1. Killt laufendes klassisches SwyxIt! (`Process.GetProcessesByName("SwyxIt!")`)
2. Benennt Windows-Startup-Verknüpfung um (`SwyxIt!.lnk` → `SwyxIt!.lnk.disabled`)
3. 2-Sekunden-Timer verhindert Neustart
4. Bei App-Beenden wird die Verknüpfung zurück-benannt

**Bewiesen funktionsfähig** (2026-07-13):
- SwyxIt! wird erfolgreich gekillt
- RemoteConnector-Tunnel wird eigenständig aufgebaut
- `isCtiMaster=true` — wir besitzen die Lines
- Dial/Hangup/Presence/ComSocket alle funktional
- Kein SwyxIt!-Popup mehr

---

## 6. Audio Device Plugin Architektur (RE 2026-07-14)

### 6.1 DispInit (memid=1) ist DER Trigger für das Audio-Plugin-Loading

`DispInit(serverName)` ist die einzige Methode, die die Audio-Plugin-Kette in Gang setzt. Die Aufrufkette ist:

```
DispInit(serverName)
  → CCLineMgr::Init
    → CLineMgr::RegisterPlugins
      → CAudioDeviceManager::ReadPlugins
```

`ReadPlugins` liest den Registry-Key:
```
HKLM\SOFTWARE\WOW6432Node\Swyx\Client Line Manager\CurrentVersion\Options\AudioDevicePlugIns
```

Für jeden Subkey mit `Enabled=1`:
1. `CoCreateInstance(<subkeyName als ProgID>)`
2. `IAudioDeviceCollection.Initialize(BSTR)` auf dem resultierenden Objekt

### 6.2 Die Plugin-Kette (vollständiger Flow)

```
CLMgr.exe
  → CAudioDeviceManager::ReadPlugins
    → CoCreateInstance("GenericDevicePlugin.GenericDeviceCollection")
      → GenericDevicePlugin.dll
        → CGenericDeviceCollection
          → IAudioDeviceCollection.Initialize()
            → CoCreateInstance(CLSID_AudioVolumeControl)
              → AudioVolumeControl.dll
                → DSOUND.dll (DirectSoundCreate + DirectSoundEnumerateA)
                → WINMM.dll  (mixer APIs)
              → Returns enumerated device names
                (e.g. "Speakers (Realtek(R) Audio)")
```

### 6.3 Complete DispId Reference Table (memid 101-148)

| memid | Name | Access |
|---|---|---|
| 101 | DispAudioMode | get/set |
| 103 | DispHandsetAvailable | - |
| 104 | DispHeadsetAvailable | - |
| 105 | DispHandsfreeAvailable | - |
| 106 | DispOpenListeningAvailable | - |
| 107 | DispHandsetDevices | get-only (collection) |
| 108 | DispHandsetCaptureDevices | get-only (collection) |
| 109 | DispHeadsetDevices | get-only (collection) |
| 110 | DispHeadsetCaptureDevices | get-only (collection) |
| 111 | DispHandsfreeDevices | get-only (collection) |
| 112 | DispHandsfreeCaptureDevices | get-only (collection) |
| 113 | DispOpenListeningDevices | get-only (collection) |
| 114 | DispRingingDevices | get-only (collection) |
| 123 | DispHandsetDevice | set-only |
| 124 | DispHandsetCaptureDevice | set-only |
| 125 | DispHeadsetDevice | set-only |
| 126 | DispHeadsetCaptureDevice | set-only |
| 127 | DispHandsfreeDevice | set-only |
| 128 | DispHandsfreeCaptureDevice | set-only |
| 129 | DispOpenListeningDevice | set-only |
| 130 | DispRingingDevice | set-only |
| 131 | DispDefaultAudioMode | set |
| 144 | DispPreferredHandsetDevice | get/set |
| 145 | DispPreferredHeadsetDevice | get/set |
| 146 | DispPreferredHandsfreeDevice | get/set |
| 147 | DispPreferredOpenListeningDevice | get/set |
| 148 | DispPreferredRingingDevice | get/set |

### 6.4 IClientLineMgrEx8 — Native vtable methods

GUID: `{F8E5549A-4C00-11D3-80BC-00105A653379}`

Native vtable-Methoden:
- `GetAvailableWaveDevicesEx`
- `UseWaveDevices(ref voiceDevice, ref handsFreeDevice, ref ringingDevice, int bConfigure, int bPnPEnable)`
- `GetUsedWaveDevices`
- `IsAudioConfigured(out bConfigured, out bIsPnPDevice, out bPnPDevicePresent)`
- `StartAudioPnP(int bForcePnPDevice)`
- `SetAudioMode(int iAudioMode)`

### 6.5 CLSIDs

| ProgID / Rolle | CLSID |
|---|---|
| `GenericDevicePlugin.GenericDeviceCollection` | `{F20146AB-850B-48E4-A732-A92C2C075750}` |
| `AudioVolumeControl.AudioVolumeControl.1` | `{CC74F3E9-514D-4CAF-8AAA-A604EBCD123A}` |
| `CLMgr.ClientLineMgr.2` | `{F8E552F8-4C00-11D3-80BC-00105A653379}` |

### 6.6 Root Cause des Bridge-Bugs

Der Bridge-Bug lag in der Wahl der falschen Init-Methode. Die Bridge rief `DispInitEx` (memid=41) auf, welche `E_NOTIMPL` zurückgibt und das Plugin-Loading **nicht** auslöst.

Stattdessen **muss** `DispInit` (memid=1) aufgerufen werden, da nur diese Methode die oben beschriebene Kette `CCLineMgr::Init → CLineMgr::RegisterPlugins → CAudioDeviceManager::ReadPlugins` triggert.

### 6.7 Fix für den Standalone-Modus

Korrekte Sequenz für den Standalone-Betrieb ohne SwyxIt!:

1. `DispInit(serverName)` aufrufen — **BEVOR** `RegisterUserEx` / `RegisterUserConnector4UC` gerufen wird
2. ~2 Sekunden warten, damit das asynchrone Plugin-Loading abschließen kann
3. `DispHandsfreeDevices` lesen, um zu verifizieren, dass Geräte geladen wurden
4. Erstes Gerät automatisch auswählen via `DispHandsfreeDevice = <deviceName>`

---

*Dokument erstellt: 2026-03-12, erweitert: 2026-07-13, 2026-07-14*
*Autor: AI-Analyse für Entwicklungszwecke*
*NICHT COMMITTEN — .gitignore-Eintrag empfohlen*
