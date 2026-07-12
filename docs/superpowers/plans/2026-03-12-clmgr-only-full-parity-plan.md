# CLMgr-Only Full Parity Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** SwyxConnect ohne `SwyxIt!.exe`-Abhängigkeit produktiv machen und schrittweise auf volle Swyx-Funktionsparität bringen.

**Architecture:** Die Bridge spricht direkt mit `CLMgr.ClientLineMgr` (COM). Session-Aufbau erfolgt explizit per Login (`RegisterUserEx` lokal, optional RC-Flow). Bestehende Handler werden zuerst vollständig in UI integriert, danach werden fehlende COM-Bereiche ergänzt, zuletzt ComSocket optional als Zusatzkanal integriert.

**Tech Stack:** Electron, React, TypeScript, Zustand, C# (.NET 8), COM Interop, JSON-RPC 2.0

---

## File Structure Mapping

- Create: `src/renderer/components/auth/LoginView.tsx` (Login-Maske)
- Create: `src/renderer/stores/useAuthStore.ts` (Session-/Login-State)
- Create: `bridge/SwyxBridge/Handlers/AuthHandler.cs` (login/logout/status JSON-RPC)
- Create: `bridge/SwyxBridge/Com/SessionManager.cs` (Register/Release/Reconnect)
- Modify: `bridge/SwyxBridge/Com/SwyxConnector.cs` (kein `SwyxIt!.exe`-Start)
- Modify: `bridge/SwyxBridge/Program.cs` (AuthHandler verdrahten)
- Modify: `src/shared/constants.ts` (AUTH IPC channels)
- Modify: `src/preload/index.ts` (auth API)
- Modify: `src/main/ipc/handlers.ts` (auth IPC routing)
- Modify: `src/renderer/src/App.tsx` (Login-Gating + Session-Flow)
- Modify: `src/renderer/hooks/useBridge.ts` (bridge/session state sync)
- Modify: `src/renderer/hooks/useCall.ts` (line selection, DTMF, callback actions)
- Modify: `src/renderer/components/phone/*` (Conference, Recording, DTMF UX)
- Modify: `src/renderer/components/settings/SettingsView.tsx` (Audio/RemoteConnector settings)
- Test: `scripts/test-bridge.mjs` (CLMgr-only smoke flow)

---

## Chunk 1: CLMgr-Only Runtime Foundation

### Task 1: Remove SwyxIt Startup Dependency

**Files:**
- Modify: `bridge/SwyxBridge/Com/SwyxConnector.cs`
- Test: `scripts/test-bridge.mjs`

- [ ] **Step 1: Write failing integration check**

Add a bridge smoke assertion in `scripts/test-bridge.mjs` that fails if process list contains `SwyxIt!.exe` after bridge connect.

- [ ] **Step 2: Run smoke check to verify RED**

Run: `node scripts/test-bridge.mjs`
Expected: FAIL because current connector may launch/hide `SwyxIt!.exe`.

- [ ] **Step 3: Implement minimal connector change**

Remove/disable `EnsureSwyxItRunning` + window hide path. Keep COM creation via `Type.GetTypeFromProgID` and robust error handling.

- [ ] **Step 4: Verify GREEN**

Run: `node scripts/test-bridge.mjs`
Expected: PASS with successful COM connect and no `SwyxIt!.exe` launch.

- [ ] **Step 5: Commit**

```bash
git add bridge/SwyxBridge/Com/SwyxConnector.cs scripts/test-bridge.mjs
git commit -m "refactor: run bridge in CLMgr-only mode without SwyxIt startup"
```

### Task 2: Add Explicit Auth Session API

**Files:**
- Create: `bridge/SwyxBridge/Handlers/AuthHandler.cs`
- Create: `bridge/SwyxBridge/Com/SessionManager.cs`
- Modify: `bridge/SwyxBridge/Program.cs`

- [ ] **Step 1: Write failing handler tests**

Create unit tests for `login`, `logout`, `getSessionStatus` mapping (success, COM error, auth error).

- [ ] **Step 2: Run tests to verify RED**

Run: `dotnet test bridge/SwyxBridge`
Expected: FAIL because AuthHandler does not exist yet.

- [ ] **Step 3: Implement minimal AuthHandler + SessionManager**

Expose JSON-RPC methods:
- `login(server, backupServer, username, password, authMode, ctiMaster)`
- `logout()`
- `getSessionStatus()`

- [ ] **Step 4: Verify GREEN**

Run: `dotnet test bridge/SwyxBridge`
Expected: PASS for new auth tests.

- [ ] **Step 5: Commit**

```bash
git add bridge/SwyxBridge/Handlers/AuthHandler.cs bridge/SwyxBridge/Com/SessionManager.cs bridge/SwyxBridge/Program.cs
git commit -m "feat: add explicit CLMgr session login/logout API"
```

---

## Chunk 2: Electron Auth + Session UX

### Task 3: Wire Auth IPC/Preload Contract

**Files:**
- Modify: `src/shared/constants.ts`
- Modify: `src/preload/index.ts`
- Modify: `src/main/ipc/handlers.ts`

- [ ] **Step 1: Write failing renderer contract tests**

Add tests for `window.swyxApi.login/logout/getSessionStatus` existence.

- [ ] **Step 2: Run tests to verify RED**

Run: `npm test`
Expected: FAIL because auth methods are missing.

- [ ] **Step 3: Implement minimal IPC mapping**

Add channels and preload methods mapped to bridge `sendRequest`.

- [ ] **Step 4: Verify GREEN**

Run: `npm test`
Expected: PASS for auth contract tests.

- [ ] **Step 5: Commit**

```bash
git add src/shared/constants.ts src/preload/index.ts src/main/ipc/handlers.ts
git commit -m "feat: expose auth session IPC methods to renderer"
```

### Task 4: Add Login Flow and Session Store

**Files:**
- Create: `src/renderer/stores/useAuthStore.ts`
- Create: `src/renderer/components/auth/LoginView.tsx`
- Modify: `src/renderer/src/App.tsx`

- [ ] **Step 1: Write failing UI tests**

Tests for:
- login form rendered when disconnected
- app routes unlocked after login success
- error banner on login failure

- [ ] **Step 2: Run tests to verify RED**

Run: `npm test`
Expected: FAIL because LoginView/AuthStore missing.

- [ ] **Step 3: Implement minimal auth UI/state**

Add auth store with states: `idle`, `logging_in`, `authenticated`, `failed`.
Gate main routes behind `authenticated`.

- [ ] **Step 4: Verify GREEN**

Run: `npm test`
Expected: PASS for login flow tests.

- [ ] **Step 5: Commit**

```bash
git add src/renderer/stores/useAuthStore.ts src/renderer/components/auth/LoginView.tsx src/renderer/src/App.tsx
git commit -m "feat: add explicit login/session flow for CLMgr-only mode"
```

---

## Chunk 3: Close Existing Feature Gaps (Bridge Already Has Methods)

### Task 5: Conference + Recording + DTMF UX Completion

**Files:**
- Modify: `src/renderer/components/phone/ActiveCallPanel.tsx`
- Modify: `src/renderer/components/phone/DtmfKeypad.tsx`
- Modify: `src/renderer/hooks/useCall.ts`

- [ ] **Step 1: Write failing interaction tests**

Test user actions for create/join conference, start/stop recording, send DTMF.

- [ ] **Step 2: Run tests to verify RED**

Run: `npm test`
Expected: FAIL on missing handlers/UI bindings.

- [ ] **Step 3: Implement minimal UI bindings**

Wire existing preload methods to visible controls and state feedback.

- [ ] **Step 4: Verify GREEN**

Run: `npm test`
Expected: PASS for conference/recording/DTMF interactions.

- [ ] **Step 5: Commit**

```bash
git add src/renderer/components/phone/ActiveCallPanel.tsx src/renderer/components/phone/DtmfKeypad.tsx src/renderer/hooks/useCall.ts
git commit -m "feat: complete conference recording and DTMF call controls"
```

### Task 6: Audio Device and Volume Control UX

**Files:**
- Modify: `src/renderer/components/settings/SettingsView.tsx`
- Modify: `src/renderer/stores/useSettingsStore.ts`

- [ ] **Step 1: Write failing settings tests**

Tests for loading audio device lists, changing active mode, mic/speaker toggle.

- [ ] **Step 2: Run tests to verify RED**

Run: `npm test`
Expected: FAIL because controls are incomplete.

- [ ] **Step 3: Implement minimal settings UI/API binding**

Use `getAudioDevices`, `setAudioMode`, `setMicro`, `setSpeaker` with robust error toast.

- [ ] **Step 4: Verify GREEN**

Run: `npm test`
Expected: PASS for audio settings behaviors.

- [ ] **Step 5: Commit**

```bash
git add src/renderer/components/settings/SettingsView.tsx src/renderer/stores/useSettingsStore.ts
git commit -m "feat: add full audio device and mode controls"
```

---

## Chunk 4: Missing COM Parity Features

### Task 7: Chat/IM + Presence Subscriptions

**Files:**
- Create: `bridge/SwyxBridge/Handlers/ChatHandler.cs`
- Modify: `bridge/SwyxBridge/Com/EventSink.cs`
- Modify: `src/shared/constants.ts`
- Modify: `src/main/ipc/handlers.ts`
- Create: `src/renderer/components/chat/ChatView.tsx`

- [ ] **Step 1: Write failing bridge tests for chat methods/events**
- [ ] **Step 2: Run bridge tests to verify RED**
Run: `dotnet test bridge/SwyxBridge`

- [ ] **Step 3: Implement minimal COM wrappers**

Add wrappers for:
- `DispRegisterChatMessageReader`
- `DispSendChatMessage`
- `DispReadChatMessage`
- optional `OpenChatTo` / `ShowChatApplication`

- [ ] **Step 4: Wire renderer event path and minimal ChatView**
- [ ] **Step 5: Verify GREEN**

Run:
- `dotnet test bridge/SwyxBridge`
- `npm test`

- [ ] **Step 6: Commit**

```bash
git add bridge/SwyxBridge/Handlers/ChatHandler.cs bridge/SwyxBridge/Com/EventSink.cs src/shared/constants.ts src/main/ipc/handlers.ts src/renderer/components/chat/ChatView.tsx
git commit -m "feat: add chat messaging and presence subscription support"
```

### Task 8: CTI + Callback-on-Busy + Contact Plugin Search

**Files:**
- Create: `bridge/SwyxBridge/Handlers/CtiHandler.cs`
- Modify: `bridge/SwyxBridge/Handlers/ContactHandler.cs`
- Modify: `bridge/SwyxBridge/Handlers/ForwardingHandler.cs`
- Modify: `src/renderer/components/contacts/ContactsView.tsx`

- [ ] **Step 1: Write failing bridge tests for CTI/callback/contact methods**
- [ ] **Step 2: Run tests to verify RED**
Run: `dotnet test bridge/SwyxBridge`

- [ ] **Step 3: Implement minimal wrappers**

Add wrappers for:
- `StartCstaSession`, `StartCstaMonitor`, `SaveCtiPairing`
- callback pickup/reject flows
- `SearchContacts`, `FulltextSearchInContactsEx`

- [ ] **Step 4: Add renderer controls + status indicators**
- [ ] **Step 5: Verify GREEN**

Run:
- `dotnet test bridge/SwyxBridge`
- `npm test`

- [ ] **Step 6: Commit**

```bash
git add bridge/SwyxBridge/Handlers/CtiHandler.cs bridge/SwyxBridge/Handlers/ContactHandler.cs bridge/SwyxBridge/Handlers/ForwardingHandler.cs src/renderer/components/contacts/ContactsView.tsx
git commit -m "feat: add CTI controls callback-on-busy and advanced contact search"
```

---

## Chunk 5: RemoteConnector + Optional ComSocket Path

### Task 9: RemoteConnector Mode

**Files:**
- Modify: `bridge/SwyxBridge/Com/SessionManager.cs`
- Modify: `src/renderer/components/settings/SettingsView.tsx`
- Modify: `src/renderer/stores/useSettingsStore.ts`

- [ ] **Step 1: Write failing tests for RC config/session mode switch**
- [ ] **Step 2: Run tests to verify RED**

Run:
- `dotnet test bridge/SwyxBridge`
- `npm test`

- [ ] **Step 3: Implement minimal RC login path**

Add config model and branch from local login to RC registration (`RegisterUserConnector4UC`) only when RC mode enabled.

- [ ] **Step 4: Verify GREEN**

Run:
- `dotnet test bridge/SwyxBridge`
- `npm test`

- [ ] **Step 5: Commit**

```bash
git add bridge/SwyxBridge/Com/SessionManager.cs src/renderer/components/settings/SettingsView.tsx src/renderer/stores/useSettingsStore.ts
git commit -m "feat: add optional remoteconnector authentication mode"
```

### Task 10: ComSocket Optional Integration (Non-Blocking)

**Files:**
- Create: `src/main/services/ComSocketService.ts`
- Modify: `src/main/ipc/handlers.ts`
- Modify: `src/renderer/stores/useLineStore.ts`

- [ ] **Step 1: Write failing integration tests for alternative event source**
- [ ] **Step 2: Run tests to verify RED**
Run: `npm test`

- [ ] **Step 3: Implement minimal feature-flagged ComSocket listener**

Use a settings feature flag and keep COM JSON-RPC as primary fallback.

- [ ] **Step 4: Verify GREEN**
Run: `npm test`

- [ ] **Step 5: Commit**

```bash
git add src/main/services/ComSocketService.ts src/main/ipc/handlers.ts src/renderer/stores/useLineStore.ts
git commit -m "feat: add optional ComSocket event pipeline behind feature flag"
```

---

## Final Verification Gate

- [ ] Run: `dotnet build bridge/SwyxBridge`
- [ ] Run: `npx electron-vite build`
- [ ] Run: `npm test`
- [ ] Run: `node scripts/test-bridge.mjs`
- [ ] Manual E2E: Login without `SwyxIt!.exe`, call flow, conference, callback, audio switch, presence, voicemail

Expected final result:
- CLMgr-only Betrieb stabil
- Kein `SwyxIt!.exe`-Start mehr notwendig
- Feature-Parität deutlich erweitert und schrittweise vollständig erreichbar
