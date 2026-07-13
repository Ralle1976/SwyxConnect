import { useMemo, useState } from 'react'
import {
  Phone, PhoneIncoming, PhoneOutgoing, PhoneMissed,
  Clock, TrendingUp, Activity, User, Users, BarChart3, CircleDot,
} from 'lucide-react'
import { useLineStore } from '../../stores/useLineStore'
import { usePhoneBookStore } from '../../stores/usePhoneBookStore'
import { useBridge } from '../../hooks/useBridge'
import { useCall } from '../../hooks/useCall'
import { LineState, PresenceStatus, type PhoneBookEntry, type JournalEntry } from '../../types/swyx'

// ─── Helpers ─────────────────────────────────────────────────────────────────

function formatDuration(seconds: number): string {
  if (seconds <= 0) return '0:00'
  const m = Math.floor(seconds / 60)
  const s = seconds % 60
  return `${m}:${String(s).padStart(2, '0')}`
}

function formatTime(iso: string): string {
  try {
    return new Date(iso).toLocaleTimeString('de-DE', { hour: '2-digit', minute: '2-digit' })
  } catch {
    return '??:??'
  }
}

// ComSocket curState codes → PresenceStatus
// 0=Offline, 1=Available, 2=Away, 3=Away, 4=Busy, 5=Away
function phoneBookStatus(curState: number): PresenceStatus {
  switch (curState) {
    case 0: return PresenceStatus.Offline
    case 4: return PresenceStatus.Busy
    case 1: return PresenceStatus.Available
    default: return PresenceStatus.Away
  }
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

const PRESENCE_RANK: Record<string, number> = {
  Available: 0, Busy: 1, DND: 1, Away: 2, Offline: 3,
}

// Filter to real users only (entityType 3), exclude groups (4)
function isUser(entry: PhoneBookEntry): boolean {
  return entry.entityType === 3 || entry.entityType === undefined
}

// ─── Stat Card ───────────────────────────────────────────────────────────────

function StatCard({ label, value, icon, color, subtext }: {
  label: string; value: string | number; icon: React.ReactNode
  color?: string; subtext?: string
}) {
  return (
    <div className="rounded-xl border border-zinc-200 dark:border-zinc-700 bg-white dark:bg-zinc-900 p-4 flex items-start gap-3">
      <div className={`mt-0.5 ${color ?? 'text-blue-500'}`}>{icon}</div>
      <div className="flex flex-col min-w-0">
        <span className="text-2xl font-bold text-zinc-800 dark:text-zinc-100 tabular-nums leading-tight">{value}</span>
        <span className="text-xs text-zinc-500 dark:text-zinc-400 font-medium mt-0.5">{label}</span>
        {subtext && <span className="text-[11px] text-zinc-400 dark:text-zinc-500 mt-0.5">{subtext}</span>}
      </div>
    </div>
  )
}

// ─── Team Row ────────────────────────────────────────────────────────────────

function TeamRow({ entry, onDial }: { entry: PhoneBookEntry; onDial: (n: string) => void }) {
  const [hovered, setHovered] = useState(false)
  const status = phoneBookStatus(entry.curState)
  const initials = entry.name.split(' ').map(w => w[0]).join('').substring(0, 2).toUpperCase()

  return (
    <button
      onClick={() => onDial(entry.number)}
      onMouseEnter={() => setHovered(true)}
      onMouseLeave={() => setHovered(false)}
      className="w-full flex items-center gap-2 text-xs px-2 py-1.5 rounded-lg hover:bg-zinc-100 dark:hover:bg-zinc-800 transition-colors text-left"
    >
      <div className="relative flex-shrink-0">
        <div className="w-6 h-6 rounded-full bg-zinc-300 dark:bg-zinc-700 flex items-center justify-center text-[10px] font-bold text-zinc-600 dark:text-zinc-300">
          {initials}
        </div>
        <div className={`absolute -bottom-0.5 -right-0.5 w-2.5 h-2.5 rounded-full ring-2 ring-white dark:ring-zinc-900 ${PRESENCE_COLOR[status] ?? 'bg-zinc-400'}`} />
      </div>
      <div className="flex-1 min-w-0">
        <div className="font-medium text-zinc-700 dark:text-zinc-300 truncate">{entry.name}</div>
        {entry.description && (
          <div className="text-[10px] text-zinc-400 dark:text-zinc-500 truncate">{entry.description}</div>
        )}
      </div>
      <span className="text-zinc-400 dark:text-zinc-500 flex-shrink-0">{PRESENCE_LABEL[status] ?? status}</span>
      <span className={`text-[10px] font-mono flex-shrink-0 ${hovered ? 'text-blue-500' : 'text-zinc-400 dark:text-zinc-500'}`}>
        {entry.number}
      </span>
    </button>
  )
}

// ─── Main Dashboard ──────────────────────────────────────────────────────────

export default function CallcenterDashboard() {
  const { isConnected } = useBridge()
  const lines = useLineStore((s) => s.lines)
  const phoneBook = usePhoneBookStore((s) => s.phoneBook)
  const journal = usePhoneBookStore((s) => s.journal)
  const comSocketAvailable = usePhoneBookStore((s) => s.available)
  const { dial } = useCall()

  const handleDial = (number: string) => { void dial(number) }

  // ─── Derived: Team Stats ──────────────────────────────────────────────
  const teamStats = useMemo(() => {
    const users = phoneBook.filter(isUser)
    const available = users.filter(u => phoneBookStatus(u.curState) === PresenceStatus.Available).length
    const busy = users.filter(u => {
      const s = phoneBookStatus(u.curState)
      return s === PresenceStatus.Busy || s === PresenceStatus.DND
    }).length
    const away = users.filter(u => phoneBookStatus(u.curState) === PresenceStatus.Away).length
    const offline = users.filter(u => phoneBookStatus(u.curState) === PresenceStatus.Offline).length
    return { total: users.length, available, busy, away, offline, users }
  }, [phoneBook])

  // ─── Derived: Today's Call Stats ──────────────────────────────────────
  const callStats = useMemo(() => {
    const now = new Date()
    const todayStart = new Date(now.getFullYear(), now.getMonth(), now.getDate())

    const todayEntries = journal.filter((e: JournalEntry) => {
      try {
        return new Date(e.callStart) >= todayStart
      } catch {
        return false
      }
    })

    // kind: 0=outgoing, 1=missed, 2=incoming (from RE + verification)
    const inbound = todayEntries.filter(e => e.kind === 2).length
    const outbound = todayEntries.filter(e => e.kind === 0).length
    const missed = todayEntries.filter(e => e.kind === 1).length

    const answered = todayEntries.filter(e => e.callDuration > 0)
    const avgDuration = answered.length > 0
      ? Math.round(answered.reduce((s, e) => s + e.callDuration, 0) / answered.length)
      : 0
    const totalDuration = todayEntries.reduce((s, e) => s + e.callDuration, 0)

    return { total: todayEntries.length, inbound, outbound, missed, avgDuration, totalDuration, todayEntries }
  }, [journal])

  // ─── Derived: Active Lines ────────────────────────────────────────────
  const activeLines = lines.filter(l =>
    l.state !== LineState.Inactive &&
    l.state !== LineState.Terminated &&
    l.state !== LineState.Disabled
  )

  // Sorted team list: Available → Busy → Away → Offline
  const sortedTeam = useMemo(() => {
    return [...teamStats.users].sort((a, b) =>
      (PRESENCE_RANK[phoneBookStatus(a.curState)] ?? 3) - (PRESENCE_RANK[phoneBookStatus(b.curState)] ?? 3)
    )
  }, [teamStats.users])

  if (!comSocketAvailable) {
    return (
      <div className="flex flex-col items-center justify-center h-full p-8">
        <BarChart3 size={48} className="text-zinc-300 dark:text-zinc-600 mb-4" />
        <h2 className="text-lg font-semibold text-zinc-600 dark:text-zinc-400 mb-2">ComSocket nicht verfügbar</h2>
        <p className="text-sm text-zinc-400 dark:text-zinc-500 text-center max-w-md">
          Das Callcenter-Dashboard benötigt die ComSocket-Verbindung für Team-Daten.
          Bitte stellen Sie sicher, dass SwyxIt! läuft und die Auto-Attach-Verbindung aktiv ist.
        </p>
      </div>
    )
  }

  return (
    <div className="flex flex-col gap-5 p-5 overflow-y-auto h-full">
      {/* Header */}
      <div className="flex items-center justify-between">
        <h1 className="text-lg font-bold text-zinc-800 dark:text-zinc-100">Callcenter Dashboard</h1>
        <div className="flex items-center gap-2">
          <CircleDot size={12} className={isConnected ? 'text-emerald-500' : 'text-red-500'} />
          <span className="text-xs text-zinc-500 dark:text-zinc-400">{isConnected ? 'Verbunden' : 'Getrennt'}</span>
        </div>
      </div>

      {/* ─── KPI Row: Today's Calls ─────────────────────────────────────── */}
      <div className="grid grid-cols-2 lg:grid-cols-4 gap-3">
        <StatCard label="Anrufe heute" value={callStats.total} icon={<Phone size={20} />} color="text-blue-500" />
        <StatCard label="Eingehend" value={callStats.inbound} icon={<PhoneIncoming size={20} />} color="text-emerald-500"
          subtext={callStats.total > 0 ? `${Math.round((callStats.inbound / callStats.total) * 100)}%` : undefined} />
        <StatCard label="Ausgehend" value={callStats.outbound} icon={<PhoneOutgoing size={20} />} color="text-blue-500"
          subtext={callStats.total > 0 ? `${Math.round((callStats.outbound / callStats.total) * 100)}%` : undefined} />
        <StatCard label="Verpasst" value={callStats.missed} icon={<PhoneMissed size={20} />}
          color={callStats.missed > 0 ? 'text-red-500' : 'text-zinc-400'}
          subtext={callStats.missed > 0 && callStats.total > 0 ? `${Math.round((callStats.missed / callStats.total) * 100)}%` : undefined} />
      </div>

      {/* ─── KPI Row: Duration + Team ───────────────────────────────────── */}
      <div className="grid grid-cols-2 lg:grid-cols-4 gap-3">
        <StatCard label="Ø Gesprächsdauer" value={formatDuration(callStats.avgDuration)} icon={<Clock size={20} />} color="text-violet-500" />
        <StatCard label="Gesamt heute" value={formatDuration(callStats.totalDuration)} icon={<TrendingUp size={20} />} color="text-violet-500" />
        <StatCard label="Aktive Leitungen" value={`${activeLines.length} / ${lines.length}`} icon={<Activity size={20} />}
          color={activeLines.length > 0 ? 'text-emerald-500' : 'text-zinc-400'} />
        <StatCard label="Team verfügbar" value={`${teamStats.available} / ${teamStats.total}`} icon={<Users size={20} />} color="text-blue-500"
          subtext={teamStats.busy > 0 ? `${teamStats.busy} beschäftigt` : undefined} />
      </div>

      {/* ─── Bottom Panels ──────────────────────────────────────────────── */}
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-4">
        {/* Team-Übersicht */}
        <div className="rounded-xl border border-zinc-200 dark:border-zinc-700 bg-white dark:bg-zinc-900 p-5">
          <div className="flex items-center gap-2 mb-4">
            <Users size={16} className="text-zinc-400 dark:text-zinc-500" />
            <h2 className="text-sm font-semibold text-zinc-700 dark:text-zinc-300">Team-Übersicht</h2>
            <span className="ml-auto text-xs text-zinc-400 dark:text-zinc-500">{teamStats.total} Kollegen</span>
          </div>

          {/* Mini Status Bar */}
          <div className="flex gap-2 mb-3 text-[11px]">
            <span className="flex items-center gap-1"><span className="w-2 h-2 rounded-full bg-emerald-500" /> {teamStats.available} Verfügbar</span>
            <span className="flex items-center gap-1"><span className="w-2 h-2 rounded-full bg-red-500" /> {teamStats.busy} Beschäftigt</span>
            <span className="flex items-center gap-1"><span className="w-2 h-2 rounded-full bg-amber-500" /> {teamStats.away} Abwesend</span>
            <span className="flex items-center gap-1"><span className="w-2 h-2 rounded-full bg-zinc-400" /> {teamStats.offline} Offline</span>
          </div>

          <div className="flex flex-col gap-0.5 max-h-72 overflow-y-auto">
            {sortedTeam.length > 0 ? (
              sortedTeam.map((entry) => (
                <TeamRow key={`${entry.entityId}-${entry.number}`} entry={entry} onDial={handleDial} />
              ))
            ) : (
              <p className="text-xs text-zinc-400 dark:text-zinc-500 italic py-4 text-center">Keine Kollegendaten</p>
            )}
          </div>
        </div>

        {/* Letzte Anrufe */}
        <div className="rounded-xl border border-zinc-200 dark:border-zinc-700 bg-white dark:bg-zinc-900 p-5">
          <div className="flex items-center gap-2 mb-4">
            <BarChart3 size={16} className="text-zinc-400 dark:text-zinc-500" />
            <h2 className="text-sm font-semibold text-zinc-700 dark:text-zinc-300">Letzte Anrufe (heute)</h2>
          </div>
          <div className="flex flex-col gap-1.5 max-h-72 overflow-y-auto">
            {callStats.todayEntries.length > 0 ? (
              callStats.todayEntries.slice(0, 15).map((entry: JournalEntry) => (
                <div key={entry.id} className="flex items-center gap-2 text-xs px-2 py-1.5 rounded-lg bg-zinc-50 dark:bg-zinc-800/60">
                  <span className="flex-shrink-0">
                    {entry.kind === 2 && <PhoneIncoming size={12} className="text-emerald-500" />}
                    {entry.kind === 0 && <PhoneOutgoing size={12} className="text-blue-500" />}
                    {entry.kind === 1 && <PhoneMissed size={12} className="text-red-500" />}
                  </span>
                  <span className="text-zinc-700 dark:text-zinc-300 font-medium truncate flex-1">
                    {entry.connectedName || entry.dialedName || entry.number}
                  </span>
                  <span className="text-zinc-400 dark:text-zinc-500 tabular-nums flex-shrink-0">{formatTime(entry.callStart)}</span>
                  {entry.callDuration > 0 && (
                    <span className="text-zinc-400 dark:text-zinc-500 tabular-nums flex-shrink-0">{formatDuration(entry.callDuration)}</span>
                  )}
                </div>
              ))
            ) : (
              <p className="text-xs text-zinc-400 dark:text-zinc-500 italic py-4 text-center">Heute noch keine Anrufe</p>
            )}
          </div>
        </div>
      </div>
    </div>
  )
}
