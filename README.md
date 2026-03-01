# SwyxConnect

**Moderner Desktop-Softphone-Client für Swyx/Enreach — Version 1.0.0**

SwyxConnect ersetzt die SwyxIt!-Oberfläche vollständig durch eine moderne, reaktive Anwendung. SwyxIt! wird dabei automatisch im Hintergrund gestartet, baut die Serververbindung auf und wird anschließend wieder beendet — der Benutzer sieht nur SwyxConnect.

---

## Features

**Telefonie**
- Anrufen, Annehmen, Auflegen über visuelles Dialpad
- Halten, Weiterleiten und Makeln
- DTMF-Tastentöne während des Gesprächs
- Bis zu 8 konfigurierbare Leitungen mit Echtzeit-Statusanzeige
- Eingehende Anrufe mit Popup-Banner
- Tastaturkürzel: F5 Annehmen, F6 Auflegen, F7 Halten, F8 Stumm, Esc Beenden
- Integrierte Tastenkürzel-Hilfe (F1 oder ?)

**Kontakte & Verlauf**
- Firmen-Kontaktbuch mit Schnellsuche (Echtdaten vom Swyx-Server)
- Kontaktdetails mit Direktwahl
- Vollständiger Anrufverlauf mit Richtungsfilter
- Verpasste Anrufe auf einen Blick

**Voicemail**
- Übersicht aller Sprachnachrichten
- Neue Nachrichten hervorgehoben
- Wiedergabe und Verwaltung

**Präsenz**
- Eigener Status: Verfügbar, Beschäftigt, Abwesend, Nicht stören
- Echtzeit-Präsenz aller Kollegen
- Bidirektionale Synchronisierung mit Microsoft Teams

**Callcenter**
- Agent-Dashboard mit Warteschlangen-Übersicht
- Supervisor-Ansicht mit Statistiken
- Echtzeit-Auslastungsanzeige

**Benutzeroberfläche**
- Helles und dunkles Farbschema (automatisch oder manuell)
- Klappbare Sidebar mit Badge-Zählern
- Custom Titlebar mit Verbindungsstatus
- System-Tray-Integration
- Audiogeräte-Auswahl mit Testfunktion
- Vollständig deutsche Benutzeroberfläche
- Alle Einstellungen werden dauerhaft gespeichert

---

## Systemanforderungen

| Anforderung | Details |
|---|---|
| **Betriebssystem** | Windows 10/11 (x86/x64) |
| **Swyx-Server** | SwyxWare / Enreach On-Premises (v14.x) |
| **Swyx-Client** | SwyxIt! 14.x installiert |
| **.NET Runtime** | .NET 8 Desktop Runtime (x86) |
| **Optional** | Microsoft Teams (für Präsenz-Sync) |

---

## Installation

### Download (empfohlen)

1. [GitHub Releases](https://github.com/Ralle1976/SwyxConnect/releases) öffnen
2. `SwyxConnect-1.0.0-win-ia32.zip` herunterladen (ca. 120 MB)
3. Zip in einen beliebigen Ordner entpacken (z.B. `C:\SwyxConnect\`)
4. `SwyxConnect.exe` starten

### Voraussetzungen

- **SwyxIt! 14.x** muss installiert und einmalig am Swyx-Server angemeldet sein
- **.NET 8 Desktop Runtime (x86)** — [Download](https://dotnet.microsoft.com/download/dotnet/8.0)

### So funktioniert es

1. SwyxConnect startet SwyxIt! automatisch im Hintergrund
2. SwyxIt! baut den RemoteConnector-Tunnel zum Server auf
3. Nach erfolgreichem Tunnel-Aufbau wird SwyxIt! beendet
4. CLMgr.exe hält die Serververbindung eigenständig
5. SwyxConnect kommuniziert über COM mit CLMgr

**Der Benutzer sieht zu keinem Zeitpunkt ein SwyxIt!-Fenster.**

---

## Aus Quellcode bauen

```bash
git clone https://github.com/Ralle1976/SwyxConnect.git
cd SwyxConnect
npm install
```

**C# Bridge bauen** (.NET 8 SDK erforderlich):

```bash
dotnet publish bridge/SwyxBridge/SwyxBridge.csproj -c Release -r win-x86 --self-contained false -o out/bridge
```

**Entwicklungsmodus:**

```bash
npm run dev
```

**Produktions-Build:**

```bash
npx electron-vite build
npx electron-builder --win --ia32
```

---

## Technologie

| Komponente | Technologie |
|---|---|
| Desktop-Anwendung | Electron 35 |
| Benutzeroberfläche | React 19, TypeScript, Tailwind CSS v4 |
| Swyx-Anbindung | C# COM Bridge (.NET 8, x86) |
| State Management | Zustand |
| Build-System | electron-vite |
| IPC-Protokoll | JSON-RPC 2.0 über stdin/stdout |

### Architektur

```
┌─────────────────────────────────────────────────┐
│               Electron App                      │
│  ┌───────────┐  ┌──────────┐  ┌──────────────┐ │
│  │   Main    │  │ Preload  │  │   Renderer   │ │
│  │ Process   │◄─┤  Bridge  ├─►│ React + TS   │ │
│  │ (IPC Hub) │  │          │  │ Tailwind v4  │ │
│  └─────┬─────┘  └──────────┘  └──────────────┘ │
│        │ stdin/stdout (JSON-RPC 2.0)            │
│  ┌─────▼─────────────────────────────────────┐  │
│  │  SwyxBridge.exe  (.NET 8, x86)            │  │
│  │  → Startet SwyxIt! hidden                 │  │
│  │  → Wartet auf Tunnel (Port 9094)          │  │
│  │  → Killt SwyxIt! nach Tunnel-Aufbau       │  │
│  │  → COM Attach auf CLMgr.exe               │  │
│  └─────────────────────────────────────────┘  │
│        │ COM Interop (Attach-Modus)            │
│  ┌─────▼─────────────────────────────────────┐  │
│  │  CLMgr.exe (hält Tunnel eigenständig)     │  │
│  │  RemoteConnector → Swyx Server            │  │
│  └───────────────────────────────────────────┘  │
└─────────────────────────────────────────────────┘
```

---

## Dokumentation

Die vollständige Dokumentation befindet sich im [Wiki](https://github.com/Ralle1976/SwyxConnect/wiki):

- [Funktionsübersicht](https://github.com/Ralle1976/SwyxConnect/wiki/Home) — Alle Features im Detail
- [Installation](https://github.com/Ralle1976/SwyxConnect/wiki/Installation) — Einrichtung Schritt für Schritt
- [Telefonie](https://github.com/Ralle1976/SwyxConnect/wiki/Telefonie) — Anrufe, Leitungen, Weiterleitung
- [Kontakte & Verlauf](https://github.com/Ralle1976/SwyxConnect/wiki/Kontakte-und-Verlauf) — Kontaktbuch, Anrufhistorie
- [Präsenz & Teams](https://github.com/Ralle1976/SwyxConnect/wiki/Praesenz) — Status, Kollegen, Teams-Sync
- [Callcenter](https://github.com/Ralle1976/SwyxConnect/wiki/Callcenter) — Dashboard, Warteschlangen
- [Einstellungen](https://github.com/Ralle1976/SwyxConnect/wiki/Einstellungen) — Theme, Audio, Konfiguration

---

## Lizenz

Proprietär — Alle Rechte vorbehalten.

---

## Autor

**Ralle1976** — [GitHub](https://github.com/Ralle1976)
