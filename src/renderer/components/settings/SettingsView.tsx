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
  Server,
  LogIn,
  CheckCircle2,
  XCircle,
  Loader2,
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

// ─── Audio Device API Type Helpers ──────────────────────────────────────────

interface AudioContextWithSinkId extends AudioContext {
  setSinkId(sinkId: string): Promise<void>
}

interface HTMLAudioElementWithSinkId extends HTMLAudioElement {
  setSinkId(sinkId: string): Promise<void>
}

// ─── Speaker Test Button ────────────────────────────────────────────────────

function SpeakerTestButton({ outputDeviceId }: { outputDeviceId: string }) {
  const [playing, setPlaying] = useState(false)
  const audioCtxRef = useRef<AudioContext | null>(null)

  const stop = useCallback(() => {
    if (audioCtxRef.current) {
      audioCtxRef.current.close().catch(() => {})
      audioCtxRef.current = null
    }
    setPlaying(false)
  }, [])

  const play = useCallback(async () => {
    setPlaying(true)
    try {
      const ctx = new AudioContext()
      audioCtxRef.current = ctx

      // Ausgabegerät setzen, wenn nicht Standard
      if (outputDeviceId !== 'default' && 'setSinkId' in ctx) {
        try {
          await (ctx as unknown as AudioContextWithSinkId).setSinkId(outputDeviceId)
        } catch { /* setSinkId nicht unterstützt */ }
      }

      // 440 Hz Sinuston, 2 Sekunden, sanftes Ausblenden
      const osc = ctx.createOscillator()
      const gain = ctx.createGain()
      osc.type = 'sine'
      osc.frequency.value = 440
      gain.gain.setValueAtTime(0.3, ctx.currentTime)
      gain.gain.exponentialRampToValueAtTime(0.01, ctx.currentTime + 2)
      osc.connect(gain)
      gain.connect(ctx.destination)
      osc.start()
      osc.stop(ctx.currentTime + 2)
      osc.onended = () => stop()
    } catch {
      stop()
    }
  }, [outputDeviceId, stop])

  const handleClick = () => {
    if (playing) stop()
    else play()
  }

  return (
    <button
      onClick={handleClick}
      className={[
        'flex items-center gap-2 px-3 py-2 rounded-lg text-xs font-medium transition-all flex-1',
        playing
          ? 'bg-red-50 dark:bg-red-950/30 text-red-600 dark:text-red-400 border border-red-200 dark:border-red-800'
          : 'bg-zinc-100 dark:bg-zinc-800 text-zinc-600 dark:text-zinc-400 hover:bg-zinc-200 dark:hover:bg-zinc-700 border border-transparent',
      ].join(' ')}
    >
      {playing ? (
        <span className="relative flex h-3 w-3 flex-shrink-0">
          <span className="animate-ping absolute inline-flex h-full w-full rounded-full bg-red-400 opacity-75" />
          <span className="relative inline-flex rounded-full h-3 w-3 bg-red-500" />
        </span>
      ) : (
        <Play size={12} />
      )}
      <span className="flex-1 text-left">
        {playing ? 'Testton wird abgespielt...' : 'Testton abspielen'}
      </span>
    </button>
  )
}

// ─── Mic Record + Playback Test ─────────────────────────────────────────────

type MicTestPhase = 'idle' | 'recording' | 'playing'

function MicTestButton({
  outputDeviceId,
  inputDeviceId,
}: {
  outputDeviceId: string
  inputDeviceId: string
}) {
  const [phase, setPhase] = useState<MicTestPhase>('idle')
  const [countdown, setCountdown] = useState(3)
  const streamRef = useRef<MediaStream | null>(null)
  const recorderRef = useRef<MediaRecorder | null>(null)
  const timeoutsRef = useRef<ReturnType<typeof setTimeout>[]>([])
  const cancelledRef = useRef(false)
  const audioElRef = useRef<HTMLAudioElement | null>(null)

  const stop = useCallback(() => {
    cancelledRef.current = true
    timeoutsRef.current.forEach(clearTimeout)
    timeoutsRef.current = []
    if (streamRef.current) {
      streamRef.current.getTracks().forEach((t) => t.stop())
      streamRef.current = null
    }
    if (recorderRef.current && recorderRef.current.state !== 'inactive') {
      recorderRef.current.stop()
    }
    recorderRef.current = null
    if (audioElRef.current) {
      audioElRef.current.pause()
      audioElRef.current = null
    }
    setPhase('idle')
    setCountdown(3)
  }, [])

  const startRecording = useCallback(async () => {
    cancelledRef.current = false
    setPhase('recording')
    setCountdown(3)

    try {
      const constraints: MediaStreamConstraints = {
        audio:
          inputDeviceId !== 'default'
            ? { deviceId: { exact: inputDeviceId } }
            : true,
      }
      const stream = await navigator.mediaDevices.getUserMedia(constraints)
      streamRef.current = stream

      const chunks: Blob[] = []
      const recorder = new MediaRecorder(stream)
      recorderRef.current = recorder

      recorder.ondataavailable = (e: BlobEvent) => {
        if (e.data.size > 0) chunks.push(e.data)
      }

      recorder.onstop = async () => {
        if (streamRef.current) {
          streamRef.current.getTracks().forEach((t) => t.stop())
          streamRef.current = null
        }

        if (cancelledRef.current || chunks.length === 0) {
          setPhase('idle')
          setCountdown(3)
          return
        }

        setPhase('playing')

        const blob = new Blob(chunks, { type: recorder.mimeType || 'audio/webm' })
        const url = URL.createObjectURL(blob)
        const audio = new Audio(url)
        audioElRef.current = audio

        // Ausgabegerät für Wiedergabe setzen
        if (outputDeviceId !== 'default' && 'setSinkId' in audio) {
          try {
            await (audio as unknown as HTMLAudioElementWithSinkId).setSinkId(outputDeviceId)
          } catch { /* setSinkId nicht unterstützt */ }
        }

        audio.onended = () => {
          URL.revokeObjectURL(url)
          audioElRef.current = null
          setPhase('idle')
          setCountdown(3)
        }
        audio.onerror = () => {
          URL.revokeObjectURL(url)
          audioElRef.current = null
          setPhase('idle')
          setCountdown(3)
        }

        audio.play().catch(() => {
          URL.revokeObjectURL(url)
          setPhase('idle')
          setCountdown(3)
        })
      }

      recorder.start()

      // Countdown: 3 → 2 → 1 → Aufnahme stoppen
      const t1 = setTimeout(() => { if (!cancelledRef.current) setCountdown(2) }, 1000)
      const t2 = setTimeout(() => { if (!cancelledRef.current) setCountdown(1) }, 2000)
      const t3 = setTimeout(() => {
        if (!cancelledRef.current && recorderRef.current?.state === 'recording') {
          recorderRef.current.stop()
          recorderRef.current = null
        }
      }, 3000)
      timeoutsRef.current = [t1, t2, t3]
    } catch {
      stop()
    }
  }, [inputDeviceId, outputDeviceId, stop])

  const handleClick = () => {
    if (phase !== 'idle') stop()
    else startRecording()
  }

  const label = () => {
    if (phase === 'recording') return `Aufnahme: ${countdown}...`
    if (phase === 'playing') return 'Wiedergabe...'
    return 'Aufnahme starten'
  }

  const isActive = phase !== 'idle'

  return (
    <button
      onClick={handleClick}
      className={[
        'flex items-center gap-2 px-3 py-2 rounded-lg text-xs font-medium transition-all flex-1',
        isActive
          ? 'bg-red-50 dark:bg-red-950/30 text-red-600 dark:text-red-400 border border-red-200 dark:border-red-800'
          : 'bg-zinc-100 dark:bg-zinc-800 text-zinc-600 dark:text-zinc-400 hover:bg-zinc-200 dark:hover:bg-zinc-700 border border-transparent',
      ].join(' ')}
    >
      {isActive ? <Square size={12} /> : <Play size={12} />}
      <span className="flex-1 text-left">{label()}</span>
    </button>
  )
}

// ─── Main Component ──────────────────────────────────────────────────────────

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
    teamsEnabled,
    externalLinePrefix,
    externalLinePrefixEnabled,
    pbxServer,
    cdsHost,
    cdsPort,
    cdsUsername,
    setTheme,
    toggleSidebar,
    setStartMinimized,
    setCloseToTray,
    setAudioInputDevice,
    setAudioOutputDevice,
    setAudioInputVolume,
    setAudioOutputVolume,
    setNumberOfLines,
    setTeamsEnabled,
    setExternalLinePrefix,
    setExternalLinePrefixEnabled,
    setPbxServer,
    setCdsHost,
    setCdsPort,
    setCdsUsername,
  } = useSettingsStore()

  // CDS Login local state
  const [cdsPassword, setCdsPassword] = useState('')
  const [cdsLoginStatus, setCdsLoginStatus] = useState<'idle' | 'loading' | 'success' | 'error'>('idle')
  const [cdsLoginMessage, setCdsLoginMessage] = useState('')
  const [cdsContactCount, setCdsContactCount] = useState(0)

  // Local input state for CDS fields (commit on blur / Enter)
  const [cdsHostInput, setCdsHostInput] = useState(cdsHost || '127.0.0.1')
  const [cdsPortInput, setCdsPortInput] = useState(String(cdsPort || 9094))
  const [cdsUsernameInput, setCdsUsernameInput] = useState(cdsUsername || '')

  useEffect(() => { setCdsHostInput(cdsHost || '127.0.0.1') }, [cdsHost])
  useEffect(() => { setCdsPortInput(String(cdsPort || 9094)) }, [cdsPort])
  useEffect(() => { setCdsUsernameInput(cdsUsername || '') }, [cdsUsername])

  async function handleCdsLogin() {
    const host = cdsHostInput.trim() || '127.0.0.1'
    const port = parseInt(cdsPortInput, 10) || 9094
    const username = cdsUsernameInput.trim()
    const password = cdsPassword

    if (!username || !password) {
      setCdsLoginStatus('error')
      setCdsLoginMessage('Benutzername und Passwort erforderlich.')
      return
    }

    setCdsLoginStatus('loading')
    setCdsLoginMessage('Verbinde...')

    try {
      const result = await window.swyxApi.cdsConnect({ host, port, username, password }) as {
        success: boolean;
        contactCount?: number;
        userName?: string;
        error?: string;
      }
      if (result?.success) {
        setCdsLoginStatus('success')
        setCdsContactCount(result.contactCount ?? 0)
        setCdsLoginMessage(`Verbunden als ${result.userName ?? username}. ${result.contactCount ?? 0} Kontakte geladen.`)
        // Persist host/port/username (NOT password)
        setCdsHost(host)
        setCdsPort(port)
        setCdsUsername(username)
      } else {
        setCdsLoginStatus('error')
        setCdsLoginMessage(result?.error ?? 'Verbindung fehlgeschlagen.')
      }
    } catch (err) {
      setCdsLoginStatus('error')
      setCdsLoginMessage(err instanceof Error ? err.message : 'Unbekannter Fehler')
    }
  }

  // Lokaler Input-State für PBX-Server (Commit on blur / Enter)
  const [pbxServerInput, setPbxServerInput] = useState(pbxServer)

  useEffect(() => {
    setPbxServerInput(pbxServer)
  }, [pbxServer])

  // Audio-Geräte enumerieren
  const [audioInputs, setAudioInputs] = useState<MediaDeviceInfo[]>([])
  const [audioOutputs, setAudioOutputs] = useState<MediaDeviceInfo[]>([])

  const enumerateDevices = useCallback(async () => {
    try {
      // Erst Mikrofon-Berechtigung anfordern, damit Labels sichtbar werden
      try {
        const stream = await navigator.mediaDevices.getUserMedia({ audio: true })
        stream.getTracks().forEach((t) => t.stop())
      } catch {
        // Berechtigung verweigert oder kein Mikrofon — trotzdem Geräte auflisten
      }
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

  function handleThemeChange(t: Theme) {
    setTheme(t)
    applyTheme(t)
  }

  function commitPbxServer() {
    const trimmed = pbxServerInput.trim()
    if (trimmed !== pbxServer) {
      setPbxServer(trimmed)
    }
  }

  // Suppress unused-import warning for icons only used conditionally via sidebar
  void PanelLeftClose
  void PanelLeftOpen

  return (
    <div className="flex flex-col gap-5 p-5 max-w-2xl mx-auto overflow-y-auto h-full">
      <h1 className="text-lg font-bold text-zinc-800 dark:text-zinc-100">Einstellungen</h1>

      {/* ─── Verbindung ────────────────────────────────────────────────── */}
      <SectionCard title="Verbindung" icon={<Server size={16} />}>
        <div className="flex flex-col gap-3">
          <div className="flex flex-col gap-1.5">
            <label className="text-xs text-zinc-700 dark:text-zinc-300 font-medium">
              SwyxWare Server
            </label>
            <input
              type="text"
              value={pbxServerInput}
              placeholder="z. B. swyx-server.firma.local"
              onChange={(e) => setPbxServerInput(e.target.value)}
              onBlur={commitPbxServer}
              onKeyDown={(e) => {
                if (e.key === 'Enter') {
                  e.currentTarget.blur()
                }
              }}
              className="w-full text-xs px-3 py-2 rounded-lg border border-zinc-200 dark:border-zinc-700 bg-zinc-50 dark:bg-zinc-800 text-zinc-700 dark:text-zinc-300 focus:outline-none focus:ring-2 focus:ring-blue-500/30 font-mono transition-colors"
            />
            <p className="text-[11px] text-zinc-400 dark:text-zinc-500 leading-relaxed">
              IP-Adresse oder Hostname des SwyxWare-Servers. Leer = automatische Erkennung.
            </p>
          </div>

          {pbxServer !== '' && (
            <div className="rounded-lg bg-amber-50 dark:bg-amber-950/30 border border-amber-200 dark:border-amber-800 px-3 py-2">
              <p className="text-xs text-amber-700 dark:text-amber-400">
                Neustart der App erforderlich für Verbindungsänderungen.
              </p>
            </div>
          )}
        </div>
      </SectionCard>

      {/* ─── CDS Standalone-Anmeldung ──────────────────────────────── */}
      <SectionCard title="CDS Standalone-Anmeldung" icon={<LogIn size={16} />}>
        <div className="flex flex-col gap-3">
          <p className="text-[11px] text-zinc-400 dark:text-zinc-500 -mt-2 mb-1 leading-relaxed">
            Direkte Verbindung zum SwyxWare CDS-Server per WCF. Ermöglicht Kontaktabruf ohne laufende SwyxIt!-Anwendung.
          </p>

          <div className="grid grid-cols-2 gap-3">
            <div className="flex flex-col gap-1.5">
              <label className="text-xs text-zinc-700 dark:text-zinc-300 font-medium">CDS-Host</label>
              <input
                type="text"
                value={cdsHostInput}
                placeholder="127.0.0.1"
                onChange={(e) => setCdsHostInput(e.target.value)}
                className="w-full text-xs px-3 py-2 rounded-lg border border-zinc-200 dark:border-zinc-700 bg-zinc-50 dark:bg-zinc-800 text-zinc-700 dark:text-zinc-300 focus:outline-none focus:ring-2 focus:ring-blue-500/30 font-mono transition-colors"
              />
            </div>
            <div className="flex flex-col gap-1.5">
              <label className="text-xs text-zinc-700 dark:text-zinc-300 font-medium">Port</label>
              <input
                type="text"
                value={cdsPortInput}
                placeholder="9094"
                onChange={(e) => setCdsPortInput(e.target.value)}
                className="w-full text-xs px-3 py-2 rounded-lg border border-zinc-200 dark:border-zinc-700 bg-zinc-50 dark:bg-zinc-800 text-zinc-700 dark:text-zinc-300 focus:outline-none focus:ring-2 focus:ring-blue-500/30 font-mono transition-colors"
              />
            </div>
          </div>

          <div className="flex flex-col gap-1.5">
            <label className="text-xs text-zinc-700 dark:text-zinc-300 font-medium">Benutzername</label>
            <input
              type="text"
              value={cdsUsernameInput}
              placeholder="z. B. Vorname Nachname"
              onChange={(e) => setCdsUsernameInput(e.target.value)}
              className="w-full text-xs px-3 py-2 rounded-lg border border-zinc-200 dark:border-zinc-700 bg-zinc-50 dark:bg-zinc-800 text-zinc-700 dark:text-zinc-300 focus:outline-none focus:ring-2 focus:ring-blue-500/30 transition-colors"
            />
          </div>

          <div className="flex flex-col gap-1.5">
            <label className="text-xs text-zinc-700 dark:text-zinc-300 font-medium">Passwort</label>
            <input
              type="password"
              value={cdsPassword}
              placeholder="CDS-Passwort"
              onChange={(e) => setCdsPassword(e.target.value)}
              onKeyDown={(e) => { if (e.key === 'Enter') handleCdsLogin() }}
              className="w-full text-xs px-3 py-2 rounded-lg border border-zinc-200 dark:border-zinc-700 bg-zinc-50 dark:bg-zinc-800 text-zinc-700 dark:text-zinc-300 focus:outline-none focus:ring-2 focus:ring-blue-500/30 transition-colors"
            />
            <p className="text-[10px] text-zinc-400 dark:text-zinc-600">
              Passwort wird nicht gespeichert.
            </p>
          </div>

          <button
            onClick={handleCdsLogin}
            disabled={cdsLoginStatus === 'loading'}
            className={[
              'flex items-center justify-center gap-2 px-4 py-2.5 rounded-lg text-xs font-semibold transition-all',
              cdsLoginStatus === 'loading'
                ? 'bg-blue-400 dark:bg-blue-600 text-white cursor-wait'
                : 'bg-blue-500 hover:bg-blue-600 dark:bg-blue-600 dark:hover:bg-blue-500 text-white cursor-pointer',
            ].join(' ')}
          >
            {cdsLoginStatus === 'loading' ? (
              <Loader2 size={14} className="animate-spin" />
            ) : (
              <LogIn size={14} />
            )}
            {cdsLoginStatus === 'loading' ? 'Verbinde...' : 'Anmelden'}
          </button>

          {cdsLoginStatus !== 'idle' && cdsLoginStatus !== 'loading' && (
            <div className={[
              'rounded-lg px-3 py-2 flex items-start gap-2',
              cdsLoginStatus === 'success'
                ? 'bg-emerald-50 dark:bg-emerald-950/30 border border-emerald-200 dark:border-emerald-800'
                : 'bg-red-50 dark:bg-red-950/30 border border-red-200 dark:border-red-800',
            ].join(' ')}>
              {cdsLoginStatus === 'success' ? (
                <CheckCircle2 size={14} className="text-emerald-600 dark:text-emerald-400 mt-0.5 flex-shrink-0" />
              ) : (
                <XCircle size={14} className="text-red-600 dark:text-red-400 mt-0.5 flex-shrink-0" />
              )}
              <p className={[
                'text-xs leading-relaxed',
                cdsLoginStatus === 'success'
                  ? 'text-emerald-700 dark:text-emerald-400'
                  : 'text-red-700 dark:text-red-400',
              ].join(' ')}>
                {cdsLoginMessage}
              </p>
            </div>
          )}
        </div>
      </SectionCard>

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
                Gleichzeitige Telefonleitungen
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
            <SpeakerTestButton outputDeviceId={audioOutputDevice} />
            <MicTestButton
              outputDeviceId={audioOutputDevice}
              inputDeviceId={audioInputDevice}
            />
          </div>
        </div>
      </SectionCard>

      {/* ─── Microsoft Teams ───────────────────────────────────────────── */}
      <SectionCard title="Microsoft Teams" icon={<Wifi size={16} />}>
        <div className="flex flex-col gap-3">
          <ToggleRow
            label="Teams-Status Synchronisation"
            description="Swyx-Status automatisch mit Microsoft Teams abgleichen"
            value={teamsEnabled}
            onChange={setTeamsEnabled}
          />

          {teamsEnabled && (
            <div className="flex flex-col gap-3">
              <div className="rounded-lg bg-blue-50 dark:bg-blue-950/30 border border-blue-200 dark:border-blue-800 px-3 py-2">
                <p className="text-xs text-blue-700 dark:text-blue-400 leading-relaxed">
                  Verbindet Ihren Swyx-Status bidirektional mit Microsoft Teams.
                  Wenn Sie in SwyxConnect telefonieren, wird Ihr Teams-Status automatisch auf
                  {'\u201e'}Beschäftigt{'\u201c'} gesetzt {'\u2014'} und umgekehrt.
                </p>
                <p className="text-xs text-blue-600 dark:text-blue-500 mt-1.5">
                  Mehrere Teams-Konten (privat/geschäftlich) werden unterstützt.
                </p>
              </div>

              <div className="rounded-lg bg-amber-50 dark:bg-amber-950/30 border border-amber-200 dark:border-amber-800 px-3 py-2">
                <p className="text-xs text-amber-700 dark:text-amber-400">
                  Voraussetzung: Azure AD App-Registrierung mit Presence.ReadWrite-Berechtigung.
                  Konto-Verwaltung folgt im nächsten Update.
                </p>
              </div>
            </div>
          )}
        </div>
      </SectionCard>

      {/* ─── Telefonie ─────────────────────────────────────────────────── */}
      <SectionCard title="Telefonie" icon={<Phone size={16} />}>
        <div className="flex flex-col gap-3">
          {/* Amtsholung */}
          <ToggleRow
            label="Amtsholung (externe Vorwahl)"
            description={`Automatisch eine ${externalLinePrefix || '0'} vor externe Nummern setzen`}
            value={externalLinePrefixEnabled}
            onChange={setExternalLinePrefixEnabled}
          />

          {externalLinePrefixEnabled && (
            <div className="flex items-center gap-2 pl-4">
              <span className="text-xs text-zinc-500 dark:text-zinc-400">Präfix:</span>
              <input
                type="text"
                value={externalLinePrefix}
                onChange={(e) => setExternalLinePrefix(e.target.value)}
                maxLength={3}
                className="w-16 text-center text-xs px-2 py-1.5 rounded-lg border border-zinc-200 dark:border-zinc-700 bg-zinc-50 dark:bg-zinc-800 text-zinc-700 dark:text-zinc-300 focus:outline-none focus:ring-2 focus:ring-blue-500/30 font-mono"
              />
            </div>
          )}

          <div className="pt-2 border-t border-zinc-100 dark:border-zinc-800 flex flex-col gap-2">
            <div className="flex items-center justify-between py-1">
              <span className="text-xs text-zinc-700 dark:text-zinc-300 font-medium">SwyxIt!-Client</span>
              <span className="text-xs text-emerald-600 dark:text-emerald-400 font-medium">Wird automatisch gestartet</span>
            </div>
            <div className="flex items-center justify-between py-1">
              <span className="text-xs text-zinc-700 dark:text-zinc-300 font-medium">Swyx-Verbindung</span>
              <span className="text-xs text-zinc-500 dark:text-zinc-400">Über lokalen Client Line Manager (COM)</span>
            </div>
            <div className="flex items-center justify-between py-1">
              <span className="text-xs text-zinc-700 dark:text-zinc-300 font-medium">Fensterunterdrückung</span>
              <span className="text-xs text-zinc-500 dark:text-zinc-400">SwyxIt!-Fenster wird automatisch minimiert</span>
            </div>
          </div>
        </div>
      </SectionCard>

      {/* ─── Über ──────────────────────────────────────────────────────── */}
      <SectionCard title="Über SwyxConnect" icon={<Info size={16} />}>
        <div className="flex flex-col gap-1.5">
          <p className="text-sm font-semibold text-zinc-800 dark:text-zinc-100">SwyxConnect <span className="text-xs font-normal text-zinc-400 dark:text-zinc-500">by Ralle1976</span></p>
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
