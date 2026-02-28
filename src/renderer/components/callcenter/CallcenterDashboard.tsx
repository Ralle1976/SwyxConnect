import { useMemo } from 'react'
import {
  Phone,
  PhoneIncoming,
  PhoneOutgoing,
  PhoneMissed,
  Clock,
  Activity,
  User,
  Users,
  Headphones,
  TrendingUp,
  BarChart3,
  CircleDot,
} from 'lucide-react'
import { useLineStore } from '../../stores/useLineStore'
import { useHistoryStore } from '../../stores/useHistoryStore'
import { usePresenceStore } from '../../stores/usePresenceStore'
import { useContactStore } from '../../stores/useContactStore'
import { useBridge } from '../../hooks/useBridge'
import { LineState, PresenceStatus, LineInfo, CallHistoryEntry, ColleaguePresence, Contact } from '../../types/swyx'

// ─── Helper ─────────────────────────────────────────────────────────────────

function formatDuration(seconds: number): string {
  if (seconds <= 0) return '0:00'
  const m = Math.floor(seconds / 60)
  const s = seconds % 60
  return `${m}:${String(s).padStart(2, '0')}`
}

function formatTime(ts: number): string {
  return new Date(ts).toLocaleTimeString('de-DE', { hour: '2-digit', minute: '2-digit' })
}

const PRESENCE_LABEL: Record<string, string> = {
  Available: 'Verfügbar',
  Away: 'Abwesend',
  Busy: 'Beschäftigt',
  DND: 'Nicht stören',
  Offline: 'Offline',
}

const PRESENCE_COLOR: Record<string, string> = {
  Available: 'bg-emerald-500',
  Away: 'bg-amber-500',
  Busy: 'bg-red-500',
  DND: 'bg-red-700',
  Offline: 'bg-zinc-400',
}

const LINE_STATE_LABEL: Record<string, string> = {
  Inactive: 'Frei',
  Ringing: 'Klingelt',
  Dialing: 'Wählt',
  Alerting: 'Rufton',
  Active: 'Aktiv',
  OnHold: 'Gehalten',
  Busy: 'Besetzt',
  Terminated: 'Beendet',
  Disabled: 'Deaktiviert',
  HookOffInternal: 'Intern',
  HookOffExternal: 'Extern',
  Knocking: 'Anklopfen',
  ConferenceActive: 'Konferenz',
  ConferenceOnHold: 'Konferenz (gehalten)',
  Transferring: 'Weiterleitung',
  DirectCall: 'Direktruf',
}

// ─── Stat Card ──────────────────────────────────────────────────────────────

function StatCard({
  label,
  value,
  icon,
  color = 'text-blue-500',
  subtext,
}: {
  label: string
  value: string | number
  icon: React.ReactNode
  color?: string
  subtext?: string
}) {
  return (
    <div className="rounded-xl border border-zinc-200 dark:border-zinc-700 bg-white dark:bg-zinc-900 p-4 flex items-start gap-3">
      <div className={`mt-0.5 ${color}`}>{icon}</div>
      <div className="flex flex-col min-w-0">
        <span className="text-2xl font-bold text-zinc-800 dark:text-zinc-100 tabular-nums leading-tight">
          {value}
        </span>
        <span className="text-xs text-zinc-500 dark:text-zinc-400 font-medium mt-0.5">{label}</span>
        {subtext && (
          <span className="text-[11px] text-zinc-400 dark:text-zinc-500 mt-0.5">{subtext}</span>
        )}
      </div>
    </div>
  )
}

// ─── Section ────────────────────────────────────────────────────────────────

function Section({
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

// ─── Main Dashboard ─────────────────────────────────────────────────────────

export default function CallcenterDashboard() {
  const { isConnected } = useBridge()
  const lines = useLineStore((s: { lines: LineInfo[] }) => s.lines)
  const entries = useHistoryStore((s: { entries: CallHistoryEntry[] }) => s.entries)
  const ownStatus = usePresenceStore((s: { ownStatus: PresenceStatus }) => s.ownStatus)
  const colleagues = usePresenceStore((s: { colleagues: ColleaguePresence[] }) => s.colleagues)
  const internalContacts = useContactStore((s: { contacts: Contact[] }) => s.contacts)

  // ─── Derived Stats ──────────────────────────────────────────────────────

  const stats = useMemo(() => {
    const now = new Date()
    const todayStart = new Date(now.getFullYear(), now.getMonth(), now.getDate()).getTime()

    const todayEntries = entries.filter((e) => e.timestamp >= todayStart)
    const totalToday = todayEntries.length
    const inboundToday = todayEntries.filter((e) => e.direction === 'inbound').length
    const outboundToday = todayEntries.filter((e) => e.direction === 'outbound').length
    const missedToday = todayEntries.filter((e) => e.direction === 'missed').length

    const answeredEntries = todayEntries.filter((e) => e.duration > 0)
    const avgDuration =
      answeredEntries.length > 0
        ? Math.round(answeredEntries.reduce((sum, e) => sum + e.duration, 0) / answeredEntries.length)
        : 0

    const totalDuration = todayEntries.reduce((sum, e) => sum + e.duration, 0)

    const activeLines = lines.filter(
      (l: LineInfo) =>
        l.state !== LineState.Inactive &&
        l.state !== LineState.Terminated &&
        l.state !== LineState.Disabled
    )

    const availableColleagues = colleagues.filter((c) => c.status === PresenceStatus.Available).length
    const busyColleagues = colleagues.filter(
      (c) => c.status === PresenceStatus.Busy || c.status === PresenceStatus.DND
    ).length

    return {
      totalToday,
      inboundToday,
      outboundToday,
      missedToday,
      avgDuration,
      totalDuration,
      activeLines,
      availableColleagues,
      busyColleagues,
      todayEntries,
    }
  }, [entries, lines, colleagues])

  return (
    <div className="flex flex-col gap-5 p-5 overflow-y-auto h-full">
      {/* Header */}
      <div className="flex items-center justify-between">
        <h1 className="text-lg font-bold text-zinc-800 dark:text-zinc-100">
          Callcenter Dashboard
        </h1>
        <div className="flex items-center gap-2">
          <CircleDot
            size={12}
            className={isConnected ? 'text-emerald-500' : 'text-red-500'}
          />
          <span className="text-xs text-zinc-500 dark:text-zinc-400">
            {isConnected ? 'Verbunden' : 'Getrennt'}
          </span>
        </div>
      </div>

      {/* ─── KPI Row ─────────────────────────────────────────────────────── */}
      <div className="grid grid-cols-2 lg:grid-cols-4 gap-3">
        <StatCard
          label="Anrufe heute"
          value={stats.totalToday}
          icon={<Phone size={20} />}
          color="text-blue-500"
        />
        <StatCard
          label="Eingehend"
          value={stats.inboundToday}
          icon={<PhoneIncoming size={20} />}
          color="text-emerald-500"
          subtext={stats.totalToday > 0 ? `${Math.round((stats.inboundToday / stats.totalToday) * 100)}%` : undefined}
        />
        <StatCard
          label="Ausgehend"
          value={stats.outboundToday}
          icon={<PhoneOutgoing size={20} />}
          color="text-blue-500"
          subtext={stats.totalToday > 0 ? `${Math.round((stats.outboundToday / stats.totalToday) * 100)}%` : undefined}
        />
        <StatCard
          label="Verpasst"
          value={stats.missedToday}
          icon={<PhoneMissed size={20} />}
          color={stats.missedToday > 0 ? 'text-red-500' : 'text-zinc-400'}
          subtext={stats.missedToday > 0 && stats.totalToday > 0 ? `${Math.round((stats.missedToday / stats.totalToday) * 100)}%` : undefined}
        />
      </div>

      {/* ─── Second Row: Duration + Lines ────────────────────────────────── */}
      <div className="grid grid-cols-2 lg:grid-cols-4 gap-3">
        <StatCard
          label="Ø Gesprächsdauer"
          value={formatDuration(stats.avgDuration)}
          icon={<Clock size={20} />}
          color="text-violet-500"
        />
        <StatCard
          label="Gesamtzeit heute"
          value={formatDuration(stats.totalDuration)}
          icon={<TrendingUp size={20} />}
          color="text-violet-500"
        />
        <StatCard
          label="Aktive Leitungen"
          value={`${stats.activeLines.length} / ${lines.length}`}
          icon={<Activity size={20} />}
          color={stats.activeLines.length > 0 ? 'text-emerald-500' : 'text-zinc-400'}
        />
        <StatCard
          label="Kollegen verfügbar"
          value={stats.availableColleagues}
          icon={<Users size={20} />}
          color="text-blue-500"
          subtext={stats.busyColleagues > 0 ? `${stats.busyColleagues} beschäftigt` : undefined}
        />
      </div>

      {/* ─── Bottom Panels ───────────────────────────────────────────────── */}
      <div className="grid grid-cols-1 lg:grid-cols-3 gap-4">
        {/* Agent-Status */}
        <Section title="Mein Status" icon={<User size={16} />}>
          <div className="flex flex-col gap-3">
            <div className="flex items-center gap-3">
              <div className={`w-3 h-3 rounded-full ${PRESENCE_COLOR[ownStatus] ?? 'bg-zinc-400'}`} />
              <span className="text-sm font-medium text-zinc-800 dark:text-zinc-100">
                {PRESENCE_LABEL[ownStatus] ?? ownStatus}
              </span>
            </div>
            <div className="flex flex-col gap-1.5 mt-1">
              {lines.map((line: LineInfo) => (
                <div
                  key={line.id}
                  className="flex items-center justify-between text-xs px-2 py-1.5 rounded-lg bg-zinc-50 dark:bg-zinc-800/60"
                >
                  <span className="text-zinc-600 dark:text-zinc-400 font-medium">
                    Leitung {line.id + 1}
                  </span>
                  <span
                    className={`font-medium ${
                      line.state === LineState.Active || line.state === LineState.Ringing
                        ? 'text-emerald-600 dark:text-emerald-400'
                        : line.state === LineState.OnHold
                          ? 'text-amber-600 dark:text-amber-400'
                          : 'text-zinc-400 dark:text-zinc-500'
                    }`}
                  >
                    {LINE_STATE_LABEL[line.state] ?? line.state}
                  </span>
                </div>
              ))}
              {lines.length === 0 && (
                <p className="text-xs text-zinc-400 dark:text-zinc-500 italic">
                  Keine Leitungen verfügbar
                </p>
              )}
            </div>
          </div>
        </Section>

        {/* Kollegen */}
        <Section title="Team-Übersicht" icon={<Users size={16} />}>
          <div className="flex flex-col gap-1.5 max-h-48 overflow-y-auto">
            {internalContacts.length > 0 ? (
              internalContacts.map((contact) => {
                // Presence-Status aus presence store über Namensabgleich ermitteln
                const colleague = colleagues.find(
                  (c) => c.name === contact.name || c.userId === contact.id
                )
                const status = colleague?.status
                return (
                  <div
                    key={contact.id}
                    className="flex items-center gap-2 text-xs px-2 py-1.5 rounded-lg bg-zinc-50 dark:bg-zinc-800/60"
                  >
                    <div
                      className={`w-2 h-2 rounded-full flex-shrink-0 ${ status ? (PRESENCE_COLOR[status] ?? 'bg-zinc-400') : 'bg-zinc-300 dark:bg-zinc-600' }`}
                    />
                    <span className="text-zinc-700 dark:text-zinc-300 font-medium truncate flex-1">
                      {contact.name}
                    </span>
                    {contact.department && (
                      <span className="text-zinc-400 dark:text-zinc-500 flex-shrink-0 text-[10px]">
                        {contact.department}
                      </span>
                    )}
                    {status && (
                      <span className="text-zinc-400 dark:text-zinc-500 flex-shrink-0">
                        {PRESENCE_LABEL[status] ?? status}
                      </span>
                    )}
                  </div>
                )
              })
            ) : colleagues.length > 0 ? (
              // Fallback: wenn keine internen Kontakte geladen, Kollegen aus presence store zeigen
              colleagues.map((c) => (
                <div
                  key={c.userId}
                  className="flex items-center gap-2 text-xs px-2 py-1.5 rounded-lg bg-zinc-50 dark:bg-zinc-800/60"
                >
                  <div className={`w-2 h-2 rounded-full flex-shrink-0 ${PRESENCE_COLOR[c.status] ?? 'bg-zinc-400'}`} />
                  <span className="text-zinc-700 dark:text-zinc-300 font-medium truncate flex-1">
                    {c.name}
                  </span>
                  <span className="text-zinc-400 dark:text-zinc-500 flex-shrink-0">
                    {PRESENCE_LABEL[c.status] ?? c.status}
                  </span>
                </div>
              ))
            ) : (
              <p className="text-xs text-zinc-400 dark:text-zinc-500 italic py-4 text-center">
                Keine Kollegendaten verfügbar
              </p>
            )}
          </div>
        </Section>

        {/* Letzte Anrufe */}
        <Section title="Letzte Anrufe" icon={<BarChart3 size={16} />}>
          <div className="flex flex-col gap-1.5 max-h-48 overflow-y-auto">
            {stats.todayEntries.length > 0 ? (
              stats.todayEntries.slice(0, 10).map((entry) => (
                <div
                  key={entry.id}
                  className="flex items-center gap-2 text-xs px-2 py-1.5 rounded-lg bg-zinc-50 dark:bg-zinc-800/60"
                >
                  <span className="flex-shrink-0">
                    {entry.direction === 'inbound' && <PhoneIncoming size={12} className="text-emerald-500" />}
                    {entry.direction === 'outbound' && <PhoneOutgoing size={12} className="text-blue-500" />}
                    {entry.direction === 'missed' && <PhoneMissed size={12} className="text-red-500" />}
                  </span>
                  <span className="text-zinc-700 dark:text-zinc-300 font-medium truncate flex-1">
                    {entry.callerName || entry.callerNumber}
                  </span>
                  <span className="text-zinc-400 dark:text-zinc-500 tabular-nums flex-shrink-0">
                    {formatTime(entry.timestamp)}
                  </span>
                  {entry.duration > 0 && (
                    <span className="text-zinc-400 dark:text-zinc-500 tabular-nums flex-shrink-0">
                      {formatDuration(entry.duration)}
                    </span>
                  )}
                </div>
              ))
            ) : (
              <p className="text-xs text-zinc-400 dark:text-zinc-500 italic py-4 text-center">
                Heute noch keine Anrufe
              </p>
            )}
          </div>
        </Section>
      </div>
    </div>
  )
}
