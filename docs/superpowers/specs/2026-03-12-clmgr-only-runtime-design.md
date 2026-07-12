# CLMgr-Only Runtime Design (ohne SwyxIt!.exe)

## Ziel

SwyxConnect soll ohne laufende `SwyxIt!.exe` funktionieren und trotzdem das volle Swyx-Potenzial nutzen.
Die technische Basis bleibt: Electron + C# Bridge + COM (`CLMgr.ClientLineMgr`).

## Entscheidung

Wir führen einen **CLMgr-only Betriebsmodus als Standard** ein:

- `SwyxIt!.exe` wird nicht mehr aktiv gestartet oder versteckt.
- Bridge verbindet sich direkt über COM-ProgID `CLMgr.ClientLineMgr`.
- Anmeldung erfolgt explizit über `RegisterUserEx` (LAN) und optional `RegisterUserConnector4UC` nur für echte RemoteConnector-Szenarien.
- Alle bestehenden und neuen Features werden auf diese Basis gehoben.

## Warum diese Entscheidung

- `CLMgr.exe` ist der eigentliche COM-Server; `SwyxIt!.exe` ist primär GUI-Host.
- Die aktuelle Bridge deckt bereits viele COM-Funktionen über Handler ab.
- Damit erreichen wir dein Ziel "ohne SwyxIt!.exe", ohne Swyx-Kernkomponenten zu verlieren.

## Architektur

### 1) Startup und COM-Aktivierung

- Main Process startet Bridge-Prozess.
- Bridge erstellt COM-Objekt per `Type.GetTypeFromProgID("CLMgr.ClientLineMgr")`.
- Kein Prozess-Scan/Start für `SwyxIt!.exe`, keine Fensterunterdrückung.

### 2) Login/Session

- Neuer expliziter Login-Flow in der UI: Server, Benutzer, Passwort, AuthMode.
- JSON-RPC `login` startet Session (`RegisterUserEx`).
- `ReleaseUserEx` beim Logout/Shutdown.
- Reconnect mit Backoff nach COM-/Netzfehlern.

### 3) Feature-Ebenen

- **Layer A (Stabilisieren):** vorhandene Bridge-Methoden vollständig in UI/Store nutzbar machen (Conference, Recording, Audio, Callback, DTMF).
- **Layer B (Parity):** fehlende COM-Bereiche ergänzen (Chat/IM, CTI, Presence-Subscriptions, Contact-Plugin-Suche, NameKeys, Media Links).
- **Layer C (Erweitern):** ComSocket als optionaler Echtzeit-/Detailkanal ergänzen, ohne COM zu ersetzen.

### 4) RemoteConnector

- Standard lokal bleibt `RegisterUserEx`.
- Remote-Betrieb als konfigurierbarer Modus mit `RegisterUserConnector4UC` und Zertifikatsfluss.
- RC nur aktivieren, wenn wirklich remote erforderlich.

## Nicht-Ziele

- Kein Neuschreiben des kompletten Swyx-Stacks (SIP/Media komplett selbst implementieren).
- Keine Nutzung proprietärer Daten in GitHub.

## Risiken und Gegenmaßnahmen

- **COM-Registrierung fehlt**: Vorbedingung dokumentieren, Installer-Prüfung einbauen.
- **Session-Konflikte bei parallelem SwyxIt-Betrieb**: Warnung und klarer Fehlerdialog.
- **Reconnect-Lücken**: zentraler Reconnect-State-Machine in Bridge.
- **Funktionsabweichungen ohne GUI-Host**: gezielte E2E-Matrix pro Feature.

## Akzeptanzkriterien

- SwyxConnect funktioniert bei Kaltstart ohne `SwyxIt!.exe`-Launch.
- Ein- und ausgehende Calls, Hold, Transfer, DTMF, Presence, History, Voicemail laufen im CLMgr-only Modus.
- Conference, Callback-on-Busy, Audio-Device-Steuerung und Recording sind nutzbar.
- Reconnect nach Netzunterbruch funktioniert automatisch.
- Keine RE-Artefakte werden committed/published.
