import { useState, useEffect, useCallback, useRef } from 'react'
import {
  Sun,
  Moon,
  Monitor,
  Volume2,
  Mic,
  Speaker,
  Wifi,
  Info,
  Settings,
  Phone,
  ToggleLeft,
  ToggleRight,
  PanelLeftClose,
  PanelLeftOpen,
  Play,
  Square,
  Check,
  Server,
  RefreshCw,
} from 'lucide-react'
import { useSettingsStore, applyTheme } from '../../stores/useSettingsStore'

type Theme = 'light' | 'dark' | 'system'

const THEME_OPTIONS: { label: string; value: Theme; icon: React.ReactNode }[] = [
  { label: 'Hell', value: 'light', icon: <Sun size={14} /> },
  { label: 'Dunkel', value: 'dark', icon: <Moon size={14} /> },
  { label: 'System', value: 'system', icon: <Monitor size={14} /> },
]

// ─── Reusable UI Components ──────────────────────────────────────────────────

function SectionCard({
  title,
  icon,
  children,
}: {
  title: string
  icon: React.ReactNode
  children: React.ReactNode
}) {
  return (
    <div className="rounded-xl border border-zinc-200 dark:border-zinc-700 bg-white dark:bg-zinc-900 p-5">
      <div className="flex items-center gap-2 mb-4">
        <span className="text-zinc-400 dark:text-zinc-500">{icon}</span>
        <h2 className="text-sm font-semibold text-zinc-700 dark:text-zinc-300">{title}</h2>
      </div>
      {children}
    </div>
  )
}

function ToggleRow({
  label,
  description,
  value,
  onChange,
}: {
  label: string
  description?: string
  value: boolean
  onChange: (v: boolean) => void
}) {
  return (
    <div className="flex items-center justify-between py-1">
      <div className="flex flex-col">
        <span className="text-xs text-zinc-700 dark:text-zinc-300 font-medium">{label}</span>
        {description && (
          <span className="text-[11px] text-zinc-400 dark:text-zinc-500 mt-0.5">{description}</span>
        )}
      </div>
      <button
        onClick={() => onChange(!value)}
        className="text-zinc-400 dark:text-zinc-500 hover:text-blue-500 transition-colors flex-shrink-0"
        aria-label={label}
      >
        {value ? <ToggleRight size={24} className="text-blue-500" /> : <ToggleLeft size={24} />}
      </button>
    </div>
  )
}

function SliderRow({
  label,
  value,
  onChange,
  icon,
}: {
  label: string
  value: number
  onChange: (v: number) => void
  icon: React.ReactNode
}) {
  return (
    <div className="flex flex-col gap-1.5 py-1">
      <div className="flex items-center gap-2">
        <span className="text-zinc-400 dark:text-zinc-500">{icon}</span>
        <span className="text-xs text-zinc-700 dark:text-zinc-300 font-medium flex-1">{label}</span>
        <span className="text-xs text-zinc-400 dark:text-zinc-500 font-mono tabular-nums w-8 text-right">
          {value}%
        </span>
      </div>
      <input
        type="range"
        min={0}
        max={100}
        step={5}
        value={value}
        onChange={(e) => onChange(Number(e.target.value))}
        className="w-full h-1.5 rounded-full appearance-none bg-zinc-200 dark:bg-zinc-700 accent-blue-500 cursor-pointer"
      />
    </div>
  )
}

function SelectRow({
  label,
  value,
  options,
  onChange,
  icon,
}: {
  label: string
  value: string
  options: { value: string; label: string }[]
  onChange: (v: string) => void
  icon: React.ReactNode
}) {
  return (
    <div className="flex flex-col gap-1.5 py-1">
      <div className="flex items-center gap-2">
        <span className="text-zinc-400 dark:text-zinc-500">{icon}</span>
        <span className="text-xs text-zinc-700 dark:text-zinc-300 font-medium">{label}</span>
      </div>
      <select
        value={value}
        onChange={(e) => onChange(e.target.value)}
        className="w-full text-xs px-3 py-2 rounded-lg border border-zinc-200 dark:border-zinc-700 bg-zinc-50 dark:bg-zinc-800 text-zinc-700 dark:text-zinc-300 focus:outline-none focus:ring-2 focus:ring-blue-500/30 transition-colors"
      >
        {options.map((opt) => (
          <option key={opt.value} value={opt.value}>
            {opt.label}
          </option>
        ))}
      </select>
    </div>
  )
}

// ─── Audio Test Component ────────────────────────────────────────────────────────────────

function AudioTestButton({ type }: { type: 'speaker' | 'mic' }) {
  const [testing, setTesting] = useState(false)
  const [micLevel, setMicLevel] = useState(0)
  const audioCtxRef = useRef<AudioContext | null>(null)
  const streamRef = useRef<MediaStream | null>(null)
  const animRef = useRef<number>(0)

  const stopTest = useCallback(() => {
    if (audioCtxRef.current) {
      audioCtxRef.current.close().catch(() => {})
      audioCtxRef.current = null
    }
    if (streamRef.current) {
      streamRef.current.getTracks().forEach((t) => t.stop())
      streamRef.current = null
    }
    cancelAnimationFrame(animRef.current)
    setMicLevel(0)
    setTesting(false)
  }, [])

  const testSpeaker = useCallback(async () => {
    setTesting(true)
    const ctx = new AudioContext()
    audioCtxRef.current = ctx
    // Electron: AudioContext startet oft im 'suspended' Status
    if (ctx.state === 'suspended') await ctx.resume()
    // Testton: 440Hz Sinus, 3 Sekunden, gut hörbar
    const osc = ctx.createOscillator()
    const gain = ctx.createGain()
    osc.type = 'sine'
    osc.frequency.value = 440
    gain.gain.setValueAtTime(0.5, ctx.currentTime)
    gain.gain.setValueAtTime(0.5, ctx.currentTime + 2.5)
    gain.gain.exponentialRampToValueAtTime(0.01, ctx.currentTime + 3)
    osc.connect(gain)
    gain.connect(ctx.destination)
    osc.start()
    osc.stop(ctx.currentTime + 3)
    osc.onended = () => stopTest()
  }, [stopTest])

  const testMic = useCallback(async () => {
    setTesting(true)
    try {
      const stream = await navigator.mediaDevices.getUserMedia({ audio: true })
      streamRef.current = stream
      const ctx = new AudioContext()
      audioCtxRef.current = ctx
      // Electron: AudioContext startet oft im 'suspended' Status
      if (ctx.state === 'suspended') await ctx.resume()
      const source = ctx.createMediaStreamSource(stream)
      const analyser = ctx.createAnalyser()
      analyser.fftSize = 256
      source.connect(analyser)
      const data = new Uint8Array(analyser.frequencyBinCount)
      const tick = () => {
        analyser.getByteFrequencyData(data)
        const avg = data.reduce((a, b) => a + b, 0) / data.length
        setMicLevel(Math.min(100, Math.round(avg * 1.5)))
        animRef.current = requestAnimationFrame(tick)
      }
      tick()
      // 5 Sekunden aufnehmen, dann stoppen
      setTimeout(() => stopTest(), 5000)
    } catch {
      stopTest()
    }
  }, [stopTest])

  const handleClick = () => {
    if (testing) {
      stopTest()
    } else {
      type === 'speaker' ? testSpeaker() : testMic()
    }
  }

  return (
    <button
      onClick={handleClick}
      className={[
        'flex items-center gap-2 px-3 py-2 rounded-lg text-xs font-medium transition-all flex-1',
        testing
          ? 'bg-red-50 dark:bg-red-950/30 text-red-600 dark:text-red-400 border border-red-200 dark:border-red-800'
          : 'bg-zinc-100 dark:bg-zinc-800 text-zinc-600 dark:text-zinc-400 hover:bg-zinc-200 dark:hover:bg-zinc-700 border border-transparent',
      ].join(' ')}
    >
      {testing ? <Square size={12} /> : <Play size={12} />}
      <span className="flex-1 text-left">
        {type === 'speaker'
          ? (testing ? 'Stopp' : 'Lautsprecher testen')
          : (testing ? 'Stopp' : 'Mikrofon testen')}
      </span>
      {type === 'mic' && testing && (
        <div className="w-16 h-1.5 bg-zinc-200 dark:bg-zinc-700 rounded-full overflow-hidden">
          <div
            className="h-full bg-emerald-500 rounded-full transition-all duration-75"
            style={{ width: `${micLevel}%` }}
          />
        </div>
      )}
    </button>
  )
}

function getAvailabilityLabel(availability: string): { label: string; colorClass: string } {
  switch (availability) {
    case 'Available':
    case 'AvailableIdle':
      return { label: 'Verfügbar', colorClass: 'text-emerald-500 dark:text-emerald-400' }
    case 'Busy':
    case 'BusyIdle':
      return { label: 'Beschäftigt', colorClass: 'text-red-500 dark:text-red-400' }
    case 'DoNotDisturb':
      return { label: 'Nicht stören', colorClass: 'text-red-500 dark:text-red-400' }
    case 'OnThePhone':
      return { label: 'Am Telefon', colorClass: 'text-red-500 dark:text-red-400' }
    case 'Presenting':
      return { label: 'Präsentiert', colorClass: 'text-red-500 dark:text-red-400' }
    case 'InAMeeting':
      return { label: 'In Besprechung', colorClass: 'text-red-500 dark:text-red-400' }
    case 'Focusing':
      return { label: 'Konzentriert', colorClass: 'text-violet-500 dark:text-violet-400' }
    case 'BeRightBack':
      return { label: 'Bin gleich zurück', colorClass: 'text-amber-500 dark:text-amber-400' }
    case 'Away':
      return { label: 'Abwesend', colorClass: 'text-amber-500 dark:text-amber-400' }
    case 'Offline':
      return { label: 'Offline', colorClass: 'text-zinc-400 dark:text-zinc-500' }
    default:
      return { label: 'Unbekannt', colorClass: 'text-zinc-400 dark:text-zinc-500' }
  }
}

// ─── Main Component ──────────────────────────────────────────────────────────────────────────────

export default function SettingsView() {
  const {
    theme,
    sidebarCollapsed,
    startMinimized,
    closeToTray,
    audioInputDevice,
    audioOutputDevice,
    audioInputVolume,
    audioOutputVolume,
    numberOfLines,
    trunkPrefix,
    trunkPrefixEnabled,
    setTheme,
    toggleSidebar,
    setStartMinimized,
    setCloseToTray,
    setAudioInputDevice,
    setAudioOutputDevice,
    setAudioInputVolume,
    setAudioOutputVolume,
    setNumberOfLines,
    setTrunkPrefix,
    setTrunkPrefixEnabled,
  } = useSettingsStore()

  // Audio-Geräte enumerieren
  const [audioInputs, setAudioInputs] = useState<MediaDeviceInfo[]>([])
  const [audioOutputs, setAudioOutputs] = useState<MediaDeviceInfo[]>([])
  const [teamsGraphStatus, setTeamsGraphStatus] = useState<{
    loggedIn: boolean
    userName: string | null
    presence: { availability: string; activity: string } | null
  } | null>(null)
  const [teamsLoggingIn, setTeamsLoggingIn] = useState(false)
  const [connectionInfo, setConnectionInfo] = useState<{
    connected: boolean
    serverName: string | null
    userName: string | null
    ownNumber: string | null
    version: string | null
    isRegistered: boolean
  } | null>(null)
  const [connectionLoading, setConnectionLoading] = useState(false)
  const [teamsLocalPresence, setTeamsLocalPresence] = useState<{
    availability: string; activity: string; source: string; isRunning: boolean
  } | null>(null)

  const enumerateDevices = useCallback(async () => {
    try {
      const devices = await navigator.mediaDevices.enumerateDevices()
      setAudioInputs(devices.filter((d) => d.kind === 'audioinput'))
      setAudioOutputs(devices.filter((d) => d.kind === 'audiooutput'))
    } catch {
      // Keine Berechtigung oder nicht verfügbar
    }
  }, [])

  useEffect(() => {
    enumerateDevices()
    navigator.mediaDevices?.addEventListener('devicechange', enumerateDevices)
    return () => navigator.mediaDevices?.removeEventListener('devicechange', enumerateDevices)
  }, [enumerateDevices])

  // Swyx-Verbindungsinformationen laden
  const fetchConnectionInfo = useCallback(async () => {
    setConnectionLoading(true)
    try {
      const api = window.swyxApi
      if (api && 'getConnectionInfo' in api) {
        const info = await (api as Record<string, (...args: unknown[]) => unknown>).getConnectionInfo()
        if (info) setConnectionInfo(info as typeof connectionInfo)
      }
    } catch {
      // Bridge nicht verbunden
    }
    setConnectionLoading(false)
  }, [])

  useEffect(() => {
    fetchConnectionInfo()
  }, [fetchConnectionInfo])

  function handleThemeChange(t: Theme) {
    setTheme(t)
    applyTheme(t)
  }
  // Teams Graph — Status beim Start laden
  useEffect(() => {
    window.swyxApi.teamsGraphGetStatus().then(setTeamsGraphStatus).catch(() => {})
  }, [])

  // Teams Graph — Echtzeit-Events abonnieren
  useEffect(() => {
    const unsub1 = window.swyxApi.onTeamsGraphStateChanged((state) => {
      setTeamsGraphStatus(prev =>
        prev
          ? { ...prev, ...state }
          : { loggedIn: state.loggedIn, userName: state.userName, presence: null }
      )
    })
    const unsub2 = window.swyxApi.onTeamsGraphPresenceChanged((presence) => {
      setTeamsGraphStatus(prev => (prev ? { ...prev, presence } : null))
    })
    return () => {
      unsub1()
      unsub2()
    }
  }, [])

  // Teams Local — Status laden + Events abonnieren
  useEffect(() => {
    window.swyxApi.teamsLocalGetTeamsPresence().then(setTeamsLocalPresence).catch(() => {})
  }, [])

  useEffect(() => {
    const unsub = window.swyxApi.onTeamsLocalPresenceChanged((data: unknown) => {
      const p = data as { availability?: string; activity?: string; source?: string }
      if (p && p.availability) {
        setTeamsLocalPresence(prev => ({
          availability: p.availability ?? 'Unknown',
          activity: p.activity ?? 'Unknown',
          source: p.source ?? 'unknown',
          isRunning: prev?.isRunning ?? true
        }))
      }
    })
    return () => unsub()
  }, [])

  const handleTeamsLogin = async () => {
    setTeamsLoggingIn(true)
    try {
      const result = await window.swyxApi.teamsGraphLogin()
      if (result.ok) {
        const status = await window.swyxApi.teamsGraphGetStatus()
        setTeamsGraphStatus(status)
        await window.swyxApi.teamsGraphStartPolling()
      }
    } catch {
      // ignore
    }
    setTeamsLoggingIn(false)
  }

  const handleTeamsLogout = async () => {
    await window.swyxApi.teamsGraphLogout()
    setTeamsGraphStatus({ loggedIn: false, userName: null, presence: null })
  }

  return (
    <div className="flex flex-col gap-5 p-5 max-w-2xl mx-auto overflow-y-auto h-full">
      <h1 className="text-lg font-bold text-zinc-800 dark:text-zinc-100">Einstellungen</h1>

      {/* ─── Allgemein ─────────────────────────────────────────────────── */}
      <SectionCard title="Allgemein" icon={<Settings size={16} />}>
        <div className="flex flex-col gap-3">
          <ToggleRow
            label="Minimiert starten"
            description="App startet im System-Tray statt im Vordergrund"
            value={startMinimized}
            onChange={setStartMinimized}
          />
          <ToggleRow
            label="In Tray schließen"
            description="Schließen-Button minimiert in den System-Tray statt die App zu beenden"
            value={closeToTray}
            onChange={setCloseToTray}
          />

          {/* Anzahl Leitungen */}
          <div className="flex items-center justify-between py-1">
            <div className="flex flex-col">
              <span className="text-xs text-zinc-700 dark:text-zinc-300 font-medium">
                Anzahl Leitungen
              </span>
              <span className="text-[11px] text-zinc-400 dark:text-zinc-500 mt-0.5">
                Gleichzeitige Telefonleitungen (Neustart erforderlich)
              </span>
            </div>
            <div className="flex items-center gap-1">
              {[1, 2, 4, 8].map((n) => (
                <button
                  key={n}
                  onClick={() => setNumberOfLines(n)}
                  className={[
                    'px-2.5 py-1 rounded-md text-xs font-medium transition-all',
                    numberOfLines === n
                      ? 'ring-2 ring-blue-500 ring-offset-1 bg-blue-50 dark:bg-blue-950/40 text-blue-700 dark:text-blue-300'
                      : 'bg-zinc-100 dark:bg-zinc-800 text-zinc-600 dark:text-zinc-400 hover:bg-zinc-200 dark:hover:bg-zinc-700',
                  ].join(' ')}
                >
                  {n}
                </button>
              ))}
            </div>
          </div>
        </div>
      </SectionCard>

      {/* ─── Darstellung ───────────────────────────────────────────────── */}
      <SectionCard title="Darstellung" icon={<Monitor size={16} />}>
        <div className="flex flex-col gap-4">
          <div>
            <p className="text-xs text-zinc-500 dark:text-zinc-400 mb-2">Farbschema</p>
            <div className="flex gap-2">
              {THEME_OPTIONS.map(({ label, value, icon }) => (
                <button
                  key={value}
                  onClick={() => handleThemeChange(value)}
                  className={[
                    'flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-xs font-medium transition-all',
                    theme === value
                      ? 'ring-2 ring-blue-500 ring-offset-1 bg-blue-50 dark:bg-blue-950/40 text-blue-700 dark:text-blue-300'
                      : 'bg-zinc-100 dark:bg-zinc-800 text-zinc-600 dark:text-zinc-400 hover:bg-zinc-200 dark:hover:bg-zinc-700',
                  ].join(' ')}
                >
                  {icon}
                  {label}
                </button>
              ))}
            </div>
          </div>

          <ToggleRow
            label="Seitenleiste einklappen"
            description="Sidebar zeigt nur Icons — mehr Platz für den Hauptbereich"
            value={sidebarCollapsed}
            onChange={toggleSidebar}
          />
        </div>
      </SectionCard>

      {/* ─── Audio ─────────────────────────────────────────────────────── */}
      <SectionCard title="Audio" icon={<Volume2 size={16} />}>
        <div className="flex flex-col gap-3">
          <p className="text-[11px] text-zinc-400 dark:text-zinc-500 -mt-2 mb-1">
            Die Audio-Wiedergabe erfolgt über den Swyx-Client. Hier können Sie das bevorzugte Gerät festlegen.
          </p>

          <SelectRow
            label="Eingabegerät (Mikrofon)"
            icon={<Mic size={14} />}
            value={audioInputDevice}
            onChange={setAudioInputDevice}
            options={[
              { value: 'default', label: 'Standard-Systemgerät' },
              ...audioInputs.map((d) => ({
                value: d.deviceId,
                label: d.label || `Mikrofon (${d.deviceId.slice(0, 8)})`,
              })),
            ]}
          />

          <SelectRow
            label="Ausgabegerät (Lautsprecher)"
            icon={<Speaker size={14} />}
            value={audioOutputDevice}
            onChange={setAudioOutputDevice}
            options={[
              { value: 'default', label: 'Standard-Systemgerät' },
              ...audioOutputs.map((d) => ({
                value: d.deviceId,
                label: d.label || `Lautsprecher (${d.deviceId.slice(0, 8)})`,
              })),
            ]}
          />

          <SliderRow
            label="Eingabelautstärke"
            icon={<Mic size={14} />}
            value={audioInputVolume}
            onChange={setAudioInputVolume}
          />

          <SliderRow
            label="Ausgabelautstärke"
            icon={<Speaker size={14} />}
            value={audioOutputVolume}
            onChange={setAudioOutputVolume}
          />

          {/* Audio-Test Buttons */}
          <div className="flex gap-2 pt-2 border-t border-zinc-100 dark:border-zinc-800">
            <AudioTestButton type="speaker" />
            <AudioTestButton type="mic" />
          </div>
        </div>
      </SectionCard>

      {/* ─── Microsoft Teams ───────────────────────────────────────────── */}
      <SectionCard title="Microsoft Teams Präsenz" icon={<Wifi size={16} />}>
        <div className="flex flex-col gap-4">

          {/* Lokale Erkennung Status */}
          <div className="flex items-center justify-between rounded-lg bg-zinc-50 dark:bg-zinc-800 border border-zinc-200 dark:border-zinc-700 px-3 py-2.5">
            <div className="flex items-center gap-2.5">
              <span
                className={`h-2 w-2 rounded-full flex-shrink-0 ${
                  !teamsLocalPresence?.isRunning
                    ? 'bg-zinc-300 dark:bg-zinc-600'
                    : teamsLocalPresence?.availability === 'Unknown' || teamsLocalPresence?.availability === 'Offline'
                      ? 'bg-zinc-400 dark:bg-zinc-500'
                      : ['Busy', 'DoNotDisturb', 'OnThePhone'].includes(teamsLocalPresence?.availability ?? '')
                        ? 'bg-red-500 shadow-[0_0_6px_rgba(239,68,68,0.4)]'
                        : ['BeRightBack', 'Away'].includes(teamsLocalPresence?.availability ?? '')
                          ? 'bg-amber-500 shadow-[0_0_6px_rgba(245,158,11,0.4)]'
                          : 'bg-emerald-500 shadow-[0_0_6px_rgba(16,185,129,0.5)]'
                }`}
              />
              <div className="flex flex-col">
                <span className="text-xs font-medium text-zinc-700 dark:text-zinc-300">
                  {teamsLocalPresence?.isRunning
                    ? getAvailabilityLabel(teamsLocalPresence.availability).label
                    : 'Erkennung nicht aktiv'}
                </span>
                {teamsLocalPresence?.isRunning && teamsLocalPresence.source !== 'none' && (
                  <span className="text-[10px] text-zinc-400 dark:text-zinc-500">
                    {teamsLocalPresence.source === 'logfile' && 'via Teams Log-Datei'}
                    {teamsLocalPresence.source === 'logfile-new' && 'via New Teams Log-Datei'}
                    {teamsLocalPresence.source === 'process-check' && 'via Process-Erkennung'}
                  </span>
                )}
              </div>
            </div>
            {teamsLocalPresence?.isRunning && teamsLocalPresence.activity !== 'Unknown' && (
              <span className={`text-[10px] font-medium px-2 py-0.5 rounded-full ${
                teamsLocalPresence.activity === 'InACall'
                  ? 'bg-red-100 dark:bg-red-950/30 text-red-600 dark:text-red-400'
                  : 'bg-zinc-100 dark:bg-zinc-700 text-zinc-500 dark:text-zinc-400'
              }`}>
                {teamsLocalPresence.activity === 'InACall' ? 'Im Gespräch' : 'Kein Gespräch'}
              </span>
            )}
          </div>

          {/* Funktions-Info */}
          <div className="rounded-lg border border-zinc-100 dark:border-zinc-800 bg-zinc-50 dark:bg-zinc-800/50 px-3 py-2.5">
            <p className="text-[10px] font-semibold text-zinc-400 dark:text-zinc-500 uppercase tracking-wider mb-2">
              Erkennung
            </p>
            <div className="flex flex-col gap-1.5">
              {[
                'Automatische Teams-Version-Erkennung (Legacy + New)',
                'Log-Datei Überwachung (Classic Teams)',
                'Process-Erkennung als Fallback',
                'Echtzeit Status-Updates',
              ].map((feature) => (
                <div key={feature} className="flex items-center gap-2">
                  <Check size={11} className="text-emerald-500 flex-shrink-0" />
                  <span className="text-xs text-zinc-600 dark:text-zinc-400">{feature}</span>
                </div>
              ))}
            </div>
          </div>

          {/* Graph API Status (falls aktiv) */}
          {teamsGraphStatus?.loggedIn && (
            <div className="rounded-lg border border-blue-200 dark:border-blue-800 bg-blue-50 dark:bg-blue-950/20 px-3 py-2.5">
              <p className="text-[10px] font-semibold text-blue-400 dark:text-blue-500 uppercase tracking-wider mb-1">
                Microsoft Graph API
              </p>
              <div className="flex items-center gap-2">
                <span className="text-xs text-blue-700 dark:text-blue-300">{teamsGraphStatus.userName ?? 'Angemeldet'}</span>
                <button
                  onClick={handleTeamsLogout}
                  className="ml-auto text-[10px] text-red-500 hover:text-red-400 underline"
                >
                  Abmelden
                </button>
              </div>
            </div>
          )}

        </div>
      </SectionCard>
      {/* ─── Telefonie ─────────────────────────────────────────────────────────── */}
      <SectionCard title="Telefonie" icon={<Phone size={16} />}>
        <div className="flex flex-col gap-3">

          {/* Verbindungsstatus-Karte */}
          <div className="flex items-center justify-between rounded-lg bg-zinc-50 dark:bg-zinc-800 border border-zinc-200 dark:border-zinc-700 px-3 py-2.5">
            <div className="flex items-center gap-2.5">
              <span
                className={`h-2.5 w-2.5 rounded-full flex-shrink-0 ${
                  connectionInfo?.connected && connectionInfo.isRegistered
                    ? 'bg-emerald-500 shadow-[0_0_6px_rgba(16,185,129,0.5)]'
                    : connectionInfo?.connected
                      ? 'bg-amber-500 shadow-[0_0_6px_rgba(245,158,11,0.4)]'
                      : 'bg-zinc-300 dark:bg-zinc-600'
                }`}
              />
              <div className="flex flex-col">
                <span className="text-xs font-medium text-zinc-700 dark:text-zinc-300">
                  {connectionInfo?.connected
                    ? connectionInfo.isRegistered
                      ? 'Registriert'
                      : 'Verbunden (nicht registriert)'
                    : 'Nicht verbunden'}
                </span>
                {connectionInfo?.serverName && (
                  <span className="text-[10px] text-zinc-400 dark:text-zinc-500">
                    {connectionInfo.serverName}
                  </span>
                )}
              </div>
            </div>
            <button
              onClick={fetchConnectionInfo}
              disabled={connectionLoading}
              className="text-zinc-400 dark:text-zinc-500 hover:text-blue-500 transition-colors p-1"
              title="Aktualisieren"
            >
              <RefreshCw size={13} className={connectionLoading ? 'animate-spin' : ''} />
            </button>
          </div>

          {/* Verbindungsdetails */}
          {connectionInfo?.connected && (
            <div className="grid grid-cols-2 gap-x-4 gap-y-2 rounded-lg border border-zinc-100 dark:border-zinc-800 bg-zinc-50 dark:bg-zinc-800/50 px-3 py-2.5">
              <div className="flex flex-col">
                <span className="text-[10px] text-zinc-400 dark:text-zinc-500 uppercase tracking-wider">Server</span>
                <span className="text-xs text-zinc-700 dark:text-zinc-300 font-medium">{connectionInfo.serverName ?? '—'}</span>
              </div>
              <div className="flex flex-col">
                <span className="text-[10px] text-zinc-400 dark:text-zinc-500 uppercase tracking-wider">Benutzer</span>
                <span className="text-xs text-zinc-700 dark:text-zinc-300 font-medium">{connectionInfo.userName ?? '—'}</span>
              </div>
              <div className="flex flex-col">
                <span className="text-[10px] text-zinc-400 dark:text-zinc-500 uppercase tracking-wider">Nebenstelle</span>
                <span className="text-xs text-zinc-700 dark:text-zinc-300 font-medium font-mono">{connectionInfo.ownNumber ?? '—'}</span>
              </div>
              <div className="flex flex-col">
                <span className="text-[10px] text-zinc-400 dark:text-zinc-500 uppercase tracking-wider">SwyxIt! Version</span>
                <span className="text-xs text-zinc-700 dark:text-zinc-300 font-medium">{connectionInfo.version ?? '—'}</span>
              </div>
            </div>
          )}

          {/* Statische Info-Zeilen */}
          <div className="flex flex-col gap-1 pt-1 border-t border-zinc-100 dark:border-zinc-800">
            <div className="flex items-center justify-between py-0.5">
              <span className="text-xs text-zinc-700 dark:text-zinc-300 font-medium">SwyxIt!-Client</span>
              <span className="text-xs text-emerald-600 dark:text-emerald-400 font-medium">Wird automatisch gestartet</span>
            </div>
            <div className="flex items-center justify-between py-0.5">
              <span className="text-xs text-zinc-700 dark:text-zinc-300 font-medium">Protokoll</span>
              <span className="text-xs text-zinc-500 dark:text-zinc-400">Client Line Manager (COM)</span>
            </div>
            <div className="flex items-center justify-between py-0.5">
              <span className="text-xs text-zinc-700 dark:text-zinc-300 font-medium">Fensterunterdrückung</span>
              <span className="text-xs text-zinc-500 dark:text-zinc-400">SwyxIt!-Fenster wird automatisch minimiert</span>
            </div>
          </div>

          {/* Amtsvorwahl */}
          <div className="flex flex-col gap-2 pt-2 border-t border-zinc-100 dark:border-zinc-800">
            <ToggleRow
              label="Amtsvorwahl verwenden"
              description="Automatisch Vorwahl (z. B. 0) bei externen Nummern voranstellen"
              value={trunkPrefixEnabled}
              onChange={setTrunkPrefixEnabled}
            />
            {trunkPrefixEnabled && (
              <div className="flex items-center gap-3 pl-1">
                <span className="text-xs text-zinc-700 dark:text-zinc-300 font-medium">Vorwahl</span>
                <input
                  type="text"
                  value={trunkPrefix}
                  onChange={(e) => setTrunkPrefix(e.target.value.replace(/[^0-9*#]/g, ''))}
                  maxLength={4}
                  className="w-20 text-center text-sm font-mono px-2 py-1.5 rounded-lg border border-zinc-200 dark:border-zinc-700 bg-zinc-50 dark:bg-zinc-800 text-zinc-700 dark:text-zinc-300 focus:outline-none focus:ring-2 focus:ring-blue-500/30"
                  placeholder="0"
                />
                <span className="text-[11px] text-zinc-400 dark:text-zinc-500">
                  Wird bei Nummern {'>'} 4 Stellen automatisch vorangestellt
                </span>
              </div>
            )}
          </div>

        </div>
      </SectionCard>

      {/* ─── Über ──────────────────────────────────────────────────────── */}
      <SectionCard title="Über SwyxConnect" icon={<Info size={16} />}>
        <div className="flex flex-col gap-1.5">
          <p className="text-sm font-semibold text-zinc-800 dark:text-zinc-100">SwyxConnect</p>
          <p className="text-xs text-zinc-500 dark:text-zinc-400">Version 1.0.0</p>
          <p className="text-xs text-zinc-400 dark:text-zinc-500 mt-1 leading-relaxed">
            Moderner Desktop-Softphone-Client für Swyx/Enreach. Ersetzt die klassische SwyxIt!-Oberfläche
            durch eine intuitive, reaktive Anwendung für Telefonie, Präsenzverwaltung und Callcenter-Funktionen.
          </p>
          <div className="flex gap-4 mt-2">
            <a
              href="https://github.com/Ralle1976/SwyxConnect"
              target="_blank"
              rel="noopener noreferrer"
              className="text-xs text-blue-500 hover:text-blue-400 underline underline-offset-2"
            >
              GitHub
            </a>
            <a
              href="https://github.com/Ralle1976/SwyxConnect/wiki"
              target="_blank"
              rel="noopener noreferrer"
              className="text-xs text-blue-500 hover:text-blue-400 underline underline-offset-2"
            >
              Dokumentation
            </a>
          </div>
        </div>
      </SectionCard>
    </div>
  )
}
