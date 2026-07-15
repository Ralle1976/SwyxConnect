import { useEffect, useCallback, useRef, useMemo } from 'react'
import { HashRouter, Routes, Route } from 'react-router-dom'
import { TitleBar } from '../components/layout/TitleBar'
import { Sidebar } from '../components/layout/Sidebar'
import { MainContent } from '../components/layout/MainContent'
import { IncomingCallBanner } from '../components/phone/IncomingCallBanner'
import { useBridge } from '../hooks/useBridge'
import { useLineStore, getRingingLines, hasActiveCall } from '../stores/useLineStore'
import { useSettingsStore, applyTheme } from '../stores/useSettingsStore'
import { usePresenceStore } from '../stores/usePresenceStore'
import { useAuthStore } from '../stores/useAuthStore'
import { useCallHistoryTracker } from '../stores/useCallHistoryTracker'
import { useHistoryStore } from '../stores/useHistoryStore'
import { usePhoneBookStore } from '../stores/usePhoneBookStore'
import { LineState, PresenceStatus } from '../types/swyx'
import PhoneView from '../components/phone/PhoneView'
import ContactsView from '../components/contacts/ContactsView'
import HistoryView from '../components/history/HistoryView'
import VoicemailView from '../components/voicemail/VoicemailView'
import PresenceView from '../components/presence/PresenceView'
import SettingsView from '../components/settings/SettingsView'
import CallcenterDashboard from '../components/callcenter/CallcenterDashboard'
import { LoginView } from '../components/auth/LoginView'
export function App() {
  const { isConnected, bridgeState } = useBridge()
  const { status: authStatus, session, refreshSessionStatus, setAttachedSession } = useAuthStore()
  const lines = useLineStore((s) => s.lines)
  const setLines = useLineStore((s) => s.setLines)
  const setActiveCall = useLineStore((s) => s.setActiveCall)
  const updateLine = useLineStore((s) => s.updateLine)
  const setColleagues = usePresenceStore((s) => s.setColleagues)
  const theme = useSettingsStore((s) => s.theme)
  const loadFromMain = useSettingsStore((s) => s.loadFromMain)
  // ─── Auto-Presence: Busy bei Anruf, Restore bei Idle ───────────────────
  const ownStatus = usePresenceStore((s) => s.ownStatus)
  const setOwnStatus = usePresenceStore((s) => s.setOwnStatus)
  // Status vor dem automatischen Wechsel auf Busy speichern
  const preCallStatusRef = useRef<PresenceStatus | null>(null)
  // Flag ob gerade ein Anruf aktiv ist (um Transitionen zu erkennen)
  const wasInCallRef = useRef(false)
  // Initialize call history tracking
  useCallHistoryTracker()
  // Refresh session status on mount
  useEffect(() => {
    refreshSessionStatus()
  }, [refreshSessionStatus])

  // Load settings from Main Process on connect
  useEffect(() => {
    if (isConnected) {
      loadFromMain()
    }
  }, [isConnected, loadFromMain])

  useEffect(() => { applyTheme(theme) }, [theme])

  useEffect(() => {
    if (!window.swyxApi) return
    const unsub1 = window.swyxApi.onLineStateChanged((updatedLines) => { setLines(updatedLines) })
    const unsub2 = window.swyxApi.onIncomingCall((call) => { setActiveCall(call) })
    const unsub3 = window.swyxApi.onPresenceChanged((colleagues) => { setColleagues(colleagues) })
    const unsub4 = window.swyxApi.onCallEnded((data) => { updateLine(data.lineId, { state: LineState.Inactive }) })
    const unsub5 = window.swyxApi.onSessionAttached((attachedSession) => { setAttachedSession(attachedSession) })
    return () => { unsub1(); unsub2(); unsub3(); unsub4(); unsub5() }
  }, [setLines, setActiveCall, setColleagues, updateLine, setAttachedSession])

  // Initiale Leitungsabfrage wenn Bridge verbunden
  const fetchInitialLines = useCallback(async () => {
    if (!window.swyxApi) return
    try {
      const result = await window.swyxApi.getLines()
      const fetchedLines = Array.isArray(result)
        ? result
        : (result as { lines: unknown[] } | null)?.lines ?? []
      if (fetchedLines.length > 0) setLines(fetchedLines as import('../types/swyx').LineInfo[])
    } catch { /* Bridge noch nicht bereit */ }
  }, [setLines])

  // Initiale Kollegenabfrage + Polling wenn Bridge verbunden
  const fetchPresence = useCallback(async () => {
    if (!window.swyxApi) return
    try {
      const result = await window.swyxApi.getPresence()
      const colleagues = Array.isArray(result)
        ? result
        : (result as { colleagues: unknown[] } | null)?.colleagues ?? []
      if (colleagues.length > 0) setColleagues(colleagues as import('../types/swyx').ColleaguePresence[])
    } catch { /* Bridge noch nicht bereit */ }
  }, [setColleagues])

  const presenceTimerRef = useRef<ReturnType<typeof setInterval> | null>(null)

  useEffect(() => {
    if (isConnected) {
      // Eigenen Status beim Verbinden auf 'Available' setzen
      window.swyxApi.setPresence(PresenceStatus.Available).catch(() => {})
      fetchPresence()
      // Polling alle 10 Sekunden für Presence-Updates (Status-Änderungen)
      presenceTimerRef.current = setInterval(fetchPresence, 10_000)

      // Amtsvorwahl automatisch vom Server erkennen
      window.swyxApi.getSystemInfo().then((info) => {
        const sysInfo = info as { publicAccessPrefix?: string } | null
        if (sysInfo?.publicAccessPrefix) {
          const store = useSettingsStore.getState()
          // Nur setzen wenn noch der Default ('0') oder leer
          if (!store.trunkPrefix || store.trunkPrefix === '0') {
            store.setTrunkPrefix(sysInfo.publicAccessPrefix)
            store.setTrunkPrefixEnabled(true)
          }
        }
      }).catch(() => {})

      // Anrufhistorie vom COM-Server laden und mit lokalen Einträgen zusammenführen
      useHistoryStore.getState().fetchHistory()
    } else {
      if (presenceTimerRef.current) {
        clearInterval(presenceTimerRef.current)
        presenceTimerRef.current = null
      }
    }
    return () => {
      if (presenceTimerRef.current) {
        clearInterval(presenceTimerRef.current)
        presenceTimerRef.current = null
      }
    }
  }, [isConnected, fetchPresence])

  useEffect(() => {
    if (isConnected) fetchInitialLines()
  }, [isConnected, fetchInitialLines])

  // ─── Line-State Polling ────────────────────────────────────────────────
  // COM push events (PubOnLineMgrNotification) are unreliable in Auto-Attach mode.
  // Poll getLines() every 2s while connected to catch incoming calls and state changes.
  const linePollRef = useRef<ReturnType<typeof setInterval> | null>(null)

  useEffect(() => {
    if (isConnected && window.swyxApi) {
      linePollRef.current = setInterval(async () => {
        try {
          const result = await window.swyxApi.getLines()
          const fetched = Array.isArray(result)
            ? result
            : (result as { lines: unknown[] } | null)?.lines ?? []
          if (fetched.length > 0) setLines(fetched as import('../types/swyx').LineInfo[])
        } catch { /* bridge busy — skip */ }
      }, 2000)
    } else {
      if (linePollRef.current) {
        clearInterval(linePollRef.current)
        linePollRef.current = null
      }
    }
    return () => {
      if (linePollRef.current) {
        clearInterval(linePollRef.current)
        linePollRef.current = null
      }
    }
  }, [isConnected, setLines])

  // ─── ComSocket PhoneBook + Journal: Initial Load + Update Mode ────────
  // Push events (cs.userDataChanged) are unreliable in Auto-Attach mode.
  // User can choose: "push" (best-effort, may miss updates) or "polling" (reliable).
  const presenceUpdateMode = useSettingsStore((s) => s.presenceUpdateMode)
  const presencePollInterval = useSettingsStore((s) => s.presencePollInterval)
  const csPollRef = useRef<ReturnType<typeof setInterval> | null>(null)

  useEffect(() => {
    if (!isConnected || !window.swyxApi) return

    const { refreshPhoneBook, refreshJournal } = usePhoneBookStore.getState()

    // Initial load: phonebook + journal
    void refreshPhoneBook()
    void refreshJournal('all')

    if (presenceUpdateMode === 'polling') {
      // Polling mode: refresh phonebook every N seconds
      csPollRef.current = setInterval(() => {
        void usePhoneBookStore.getState().refreshPhoneBook()
      }, Math.max(3, presencePollInterval) * 1000)
    } else {
      // Push mode: subscribe to cs.userDataChanged events
      const unsub = window.swyxApi.onCsUserDataChanged(() => {
        void usePhoneBookStore.getState().refreshPhoneBook()
      })
      // Store cleanup via a ref-like pattern
      return () => {
        unsub()
        if (csPollRef.current) {
          clearInterval(csPollRef.current)
          csPollRef.current = null
        }
      }
    }

    return () => {
      if (csPollRef.current) {
        clearInterval(csPollRef.current)
        csPollRef.current = null
      }
    }
  }, [isConnected, presenceUpdateMode, presencePollInterval])
  const isInCall = useMemo(() => hasActiveCall(lines), [lines])

  useEffect(() => {
    if (!isConnected || !window.swyxApi) return

    const wasInCall = wasInCallRef.current

    if (isInCall && !wasInCall) {
      // Transition: kein Anruf → Anruf aktiv → auf Busy wechseln
      // Nur speichern wenn noch kein preCallStatus gespeichert ist
      if (preCallStatusRef.current === null) {
        preCallStatusRef.current = ownStatus
      }
      setOwnStatus(PresenceStatus.Busy)
      window.swyxApi.setPresence(PresenceStatus.Busy).catch(() => {})
    } else if (!isInCall && wasInCall) {
      // Transition: Anruf aktiv → kein Anruf → Status wiederherstellen
      const restoreTo = preCallStatusRef.current ?? PresenceStatus.Available
      preCallStatusRef.current = null
      // DND/Away nicht überschreiben wenn manuell gesetzt
      // Nur wiederherstellen wenn aktueller Status noch Busy ist (auto-gesetzt)
      if (ownStatus === PresenceStatus.Busy) {
        setOwnStatus(restoreTo)
        window.swyxApi.setPresence(restoreTo).catch(() => {})
      }
    }

    wasInCallRef.current = isInCall
  }, [isInCall, isConnected, ownStatus, setOwnStatus])

  const ringing = getRingingLines(lines)
  // Show login view if not authenticated
  const isAuthenticated = authStatus === 'authenticated' && session?.isAuthenticated

  if (!isAuthenticated) {
    return <LoginView />
  }

  return (
    <HashRouter>
      <div className="flex flex-col h-screen w-screen overflow-hidden bg-zinc-50 dark:bg-zinc-950 text-zinc-900 dark:text-zinc-100 select-none">
        <TitleBar />

        {!isConnected && (
          <div className="w-full bg-amber-500 dark:bg-amber-600 text-white text-xs font-medium px-4 py-1 flex items-center gap-2 z-40">
            <span className="inline-block w-2 h-2 rounded-full bg-white animate-pulse" />
            {bridgeState === 'Restarting'
              ? 'Verbindung wird wiederhergestellt…'
              : 'Bridge nicht verbunden — SwyxIT nicht erreichbar'}
          </div>
        )}

        {ringing.map((line) => (
          <IncomingCallBanner
            key={line.id}
            lineId={line.id}
            callerName={line.callerName ?? ''}
            callerNumber={line.callerNumber ?? ''}
          />
        ))}

        <div className="flex flex-1 overflow-hidden">
          <Sidebar />
          <MainContent>
            <Routes>
              <Route path="/" element={<PhoneView />} />
              <Route path="/contacts" element={<ContactsView />} />
              <Route path="/history" element={<HistoryView />} />
              <Route path="/voicemail" element={<VoicemailView />} />
              <Route path="/presence" element={<PresenceView />} />
              <Route path="/settings" element={<SettingsView />} />
              <Route path="/callcenter" element={<CallcenterDashboard />} />
            </Routes>
          </MainContent>
        </div>
      </div>
    </HashRouter>
  )
}
