import { useState, useEffect, useCallback } from 'react'
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
  MinusCircle,
  XCircle,
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
  } = useSettingsStore()

  // Audio-Geräte enumerieren
  const [audioInputs, setAudioInputs] = useState<MediaDeviceInfo[]>([])
  const [audioOutputs, setAudioOutputs] = useState<MediaDeviceInfo[]>([])

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

  function handleThemeChange(t: Theme) {
    setTheme(t)
    applyTheme(t)
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
            <div className="rounded-lg bg-amber-50 dark:bg-amber-950/30 border border-amber-200 dark:border-amber-800 px-3 py-2">
              <p className="text-xs text-amber-700 dark:text-amber-400">
                Teams-Integration wird in einem zukünftigen Update verfügbar. Die Einstellung wird gespeichert.
              </p>
            </div>
          )}
        </div>
      </SectionCard>

      {/* ─── Telefonie ─────────────────────────────────────────────────── */}
      <SectionCard title="Telefonie" icon={<Phone size={16} />}>
        <div className="flex flex-col gap-2">
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
