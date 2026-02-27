import { useEffect } from 'react'
import { HashRouter, Routes, Route } from 'react-router-dom'
import { TitleBar } from '../components/layout/TitleBar'
import { Sidebar } from '../components/layout/Sidebar'
import { MainContent } from '../components/layout/MainContent'
import { IncomingCallBanner } from '../components/phone/IncomingCallBanner'
import { useBridge } from '../hooks/useBridge'
import { useLineStore, getRingingLines } from '../stores/useLineStore'
import { useSettingsStore, applyTheme } from '../stores/useSettingsStore'
import { usePresenceStore } from '../stores/usePresenceStore'
import { useCallHistoryTracker } from '../stores/useCallHistoryTracker'
import { LineState } from '../types/swyx'
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

  // Initialize call history tracking
  useCallHistoryTracker()

  useEffect(() => { applyTheme(theme) }, [theme])

  useEffect(() => {
    if (!window.swyxApi) return
    const unsub1 = window.swyxApi.onLineStateChanged((updatedLines) => { setLines(updatedLines) })
    const unsub2 = window.swyxApi.onIncomingCall((call) => { setActiveCall(call) })
    const unsub3 = window.swyxApi.onPresenceChanged((colleagues) => { setColleagues(colleagues) })
    const unsub4 = window.swyxApi.onCallEnded((data) => { updateLine(data.lineId, { state: LineState.Inactive }) })
    return () => { unsub1(); unsub2(); unsub3(); unsub4() }
  }, [setLines, setActiveCall, setColleagues, updateLine])

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
