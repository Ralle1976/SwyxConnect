# SwyxConnect

**Moderner Desktop-Softphone-Client für Swyx/Enreach**

SwyxConnect ist ein modernes UI-Replacement für SwyxIt!. Die Anwendung bietet eine intuitive, reaktive Oberfläche für Telefonie, Präsenzverwaltung und Callcenter-Funktionen — optimiert für den täglichen Einsatz in Büro und Callcenter.

---

## Features

**Telefonie**
- Anrufen, Annehmen, Auflegen über visuelles Dialpad
- Halten, Weiterleiten und Makeln
- DTMF-Tastentöne während des Gesprächs
- 4 Leitungen mit Echtzeit-Statusanzeige
- Eingehende Anrufe mit Popup-Banner

**Kontakte & Verlauf**
- Firmen-Kontaktbuch mit Schnellsuche
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
- Vollständig deutsche Benutzeroberfläche

---

## Screenshots

> *Folgt in Kürze*

---

## Systemanforderungen

| Anforderung | Details |
|---|---|
| **Betriebssystem** | Windows 10/11 (x64) |
| **Swyx Client** | SwyxIt! 14.x (lokal installiert und angemeldet) |
| **Optional** | Microsoft Teams (für Präsenz-Sync) |

---

## Installation

### Aus Releases (empfohlen)

1. Neuestes Release von [GitHub Releases](https://github.com/Ralle1976/SwyxConnect/releases) herunterladen
2. Installer ausführen
3. SwyxConnect starten — verbindet sich automatisch mit dem lokalen SwyxIt!-Client

### Aus Quellcode

```bash
git clone https://github.com/Ralle1976/SwyxConnect.git
cd SwyxConnect
npm install
npm run dev
```

Für den Produktions-Build:

```bash
npm run build
npm run build:win
```

---

## Technologie

| Komponente | Technologie |
|---|---|
| Desktop-Anwendung | Electron |
| Benutzeroberfläche | React, TypeScript, Tailwind CSS |
| Swyx-Anbindung | C# COM Bridge (.NET 8) |
| State Management | Zustand |
| Build-System | electron-vite |
| Installer | electron-builder (NSIS) |

---

## Dokumentation

Die vollständige Dokumentation befindet sich im [Wiki](https://github.com/Ralle1976/SwyxConnect/wiki):

- [Funktionsübersicht](https://github.com/Ralle1976/SwyxConnect/wiki/Home) — Alle Features im Detail
- [Telefonie](https://github.com/Ralle1976/SwyxConnect/wiki/Telefonie) — Anrufe, Leitungen, Weiterleitung
- [Kontakte & Verlauf](https://github.com/Ralle1976/SwyxConnect/wiki/Kontakte-und-Verlauf) — Kontaktbuch, Anrufhistorie
- [Präsenz & Teams](https://github.com/Ralle1976/SwyxConnect/wiki/Praesenz) — Status, Kollegen, Teams-Sync
- [Callcenter](https://github.com/Ralle1976/SwyxConnect/wiki/Callcenter) — Dashboard, Warteschlangen
- [Einstellungen](https://github.com/Ralle1976/SwyxConnect/wiki/Einstellungen) — Theme, Konfiguration
- [Entwicklung](https://github.com/Ralle1976/SwyxConnect/wiki/Entwicklung) — Setup, Build, Architektur

---

## Lizenz

Proprietär — Alle Rechte vorbehalten.

---

## Autor

**Ralle1976** — [GitHub](https://github.com/Ralle1976)
