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
import { useCallHistoryTracker } from '../stores/useCallHistoryTracker'
import { LineState, PresenceStatus } from '../types/swyx'
import PhoneView from '../components/phone/PhoneView'
import ContactsView from '../components/contacts/ContactsView'
import HistoryView from '../components/history/HistoryView'
import VoicemailView from '../components/voicemail/VoicemailView'
import PresenceView from '../components/presence/PresenceView'
import SettingsView from '../components/settings/SettingsView'
import CallcenterDashboard from '../components/callcenter/CallcenterDashboard'

export function App() {
  const { isConnected, bridgeState } = useBridge()
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
    return () => { unsub1(); unsub2(); unsub3(); unsub4() }
  }, [setLines, setActiveCall, setColleagues, updateLine])

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

  // ─── Auto-Presence basierend auf Leitungsstatus ─────────────────────────
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
