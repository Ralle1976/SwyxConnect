import { useMemo, useState } from 'react';
import { usePresence } from '../../hooks/usePresence';
import { usePresenceStore } from '../../stores/usePresenceStore';
import { useComSocket } from '../../hooks/useComSocket';
import { useCall } from '../../hooks/useCall';
import { PresenceStatus } from '../../types/swyx';
import type { PhoneBookEntry } from '../../types/swyx';
import ColleagueCard from './ColleagueCard';
import SearchInput from '../common/SearchInput';
import StatusBadge from '../common/StatusBadge';
import Avatar from '../common/Avatar';
import EmptyState from '../common/EmptyState';
import TeamsStatusBanner from './TeamsStatusBanner';
import { Phone, RefreshCw, Users } from 'lucide-react';

const STATUS_OPTIONS: { label: string; value: PresenceStatus }[] = [
  { label: 'Verfügbar', value: PresenceStatus.Available },
  { label: 'Abwesend', value: PresenceStatus.Away },
  { label: 'Beschäftigt', value: PresenceStatus.Busy },
  { label: 'Nicht stören', value: PresenceStatus.DND },
  { label: 'Offline', value: PresenceStatus.Offline },
];

// ComSocket curState codes → PresenceStatus. Spec: 0=Offline, 3=Away, 4=Busy.
// Any other value (e.g. 2) is treated as Available.
function phoneBookStatus(curState: number): PresenceStatus {
  switch (curState) {
    case 0:
      return PresenceStatus.Offline;
    case 3:
      return PresenceStatus.Away;
    case 4:
      return PresenceStatus.Busy;
    default:
      return PresenceStatus.Available;
  }
}

// Sort rank: Available → Busy → Away → Offline (matches task spec).
const STATUS_RANK: Record<PresenceStatus, number> = {
  [PresenceStatus.Available]: 0,
  [PresenceStatus.Busy]: 1,
  [PresenceStatus.Away]: 2,
  [PresenceStatus.Offline]: 3,
  [PresenceStatus.DND]: 1, // treat DND alongside Busy
};

interface PhoneBookRowProps {
  entry: PhoneBookEntry;
  onDial: (number: string) => void;
}

/** Single colleague row sourced from the ComSocket phonebook (live presence). */
function PhoneBookRow({ entry, onDial }: PhoneBookRowProps) {
  const [hovered, setHovered] = useState(false);
  const status = phoneBookStatus(entry.curState);
  // Groups (entityType 4) don't have presence — skip the dot.
  const isGroup = entry.entityType === 4;

  return (
    <div
      className="flex items-center gap-3 px-4 py-3 hover:bg-zinc-50 dark:hover:bg-zinc-800/60 transition-colors cursor-default"
      onMouseEnter={() => setHovered(true)}
      onMouseLeave={() => setHovered(false)}
    >
      <div className="relative shrink-0">
        <Avatar name={entry.name} size="md" presence={isGroup ? undefined : status} />
      </div>

      <div className="flex-1 min-w-0">
        <div className="flex items-center gap-2">
          <span className="text-sm font-semibold text-zinc-800 dark:text-zinc-100 truncate">
            {entry.name}
          </span>
          {!isGroup && <StatusBadge status={status} size="sm" />}
          {isGroup && (
            <span className="text-[10px] px-1.5 py-0.5 rounded bg-zinc-100 dark:bg-zinc-800 text-zinc-500 dark:text-zinc-400 uppercase tracking-wider">
              Gruppe
            </span>
          )}
        </div>
        <div className="flex items-center gap-2 mt-0.5">
          {entry.description && (
            <span className="text-xs text-zinc-400 dark:text-zinc-500 truncate">
              {entry.description}
            </span>
          )}
          {entry.number && (
            <span className="text-xs text-zinc-400 dark:text-zinc-500 font-mono truncate">
              {entry.description ? '· ' : ''}
              {entry.number}
            </span>
          )}
        </div>
      </div>

      {entry.number && (
        <button
          onClick={() => onDial(entry.number)}
          aria-label={`${entry.name} anrufen`}
          className={[
            'shrink-0 p-1.5 rounded-full transition-all',
            hovered
              ? 'opacity-100 bg-emerald-100 dark:bg-emerald-900/40 text-emerald-600 dark:text-emerald-400 hover:bg-emerald-200 dark:hover:bg-emerald-800/60'
              : 'opacity-0 pointer-events-none',
          ].join(' ')}
        >
          <Phone size={15} />
        </button>
      )}
    </div>
  );
}

export default function PresenceView() {
  const { ownStatus, setPresence } = usePresence();
  const { colleagues } = usePresenceStore();
  const { dial } = useCall();
  const { phoneBook, available, loading, refreshPhoneBook } = useComSocket();
  const [search, setSearch] = useState('');

  // Prefer the ComSocket phonebook; fall back to legacy COM speed dials when
  // the bridge is unavailable or has not yet returned entries.
  const usePhoneBook = available && phoneBook.length > 0;

  const filteredPhoneBook = useMemo(() => {
    const q = search.trim().toLowerCase();
    return phoneBook
      .filter(
        (e) =>
          !q ||
          e.name.toLowerCase().includes(q) ||
          (e.description ?? '').toLowerCase().includes(q) ||
          (e.number ?? '').toLowerCase().includes(q)
      )
      .sort((a, b) => {
        const rank = STATUS_RANK[phoneBookStatus(a.curState)] - STATUS_RANK[phoneBookStatus(b.curState)];
        if (rank !== 0) return rank;
        return a.name.localeCompare(b.name, 'de');
      });
  }, [phoneBook, search]);

  const filteredColleagues = useMemo(() => {
    const q = search.trim().toLowerCase();
    return colleagues.filter((c) => c.name.toLowerCase().includes(q));
  }, [colleagues, search]);

  const sourceLabel = usePhoneBook
    ? `Telefonbuch · ${filteredPhoneBook.length}`
    : `Kurzwahl (COM) · ${filteredColleagues.length}`;

  return (
    <div className="flex flex-col h-full gap-4 p-4">
      {/* Own status section */}
      <div className="rounded-xl border border-zinc-200 dark:border-zinc-700 bg-white dark:bg-zinc-900 p-4 flex flex-col gap-3">
        <div className="flex items-center gap-2">
          <StatusBadge status={ownStatus} />
          <span className="text-sm font-medium text-zinc-700 dark:text-zinc-300">
            Eigener Status
          </span>
        </div>

        <div className="flex flex-wrap gap-2">
          {STATUS_OPTIONS.map(({ label, value }) => (
            <button
              key={value}
              onClick={() => setPresence(value)}
              className={[
                'px-3 py-1.5 rounded-lg text-xs font-medium transition-all',
                ownStatus === value
                  ? 'ring-2 ring-offset-1 ring-blue-500 bg-blue-50 dark:bg-blue-950/40 text-blue-700 dark:text-blue-300'
                  : 'bg-zinc-100 dark:bg-zinc-800 text-zinc-600 dark:text-zinc-400 hover:bg-zinc-200 dark:hover:bg-zinc-700',
              ].join(' ')}
            >
              {label}
            </button>
          ))}
        </div>

        <TeamsStatusBanner />
      </div>

      {/* Colleagues section */}
      <div className="flex-1 flex flex-col rounded-xl border border-zinc-200 dark:border-zinc-700 bg-white dark:bg-zinc-900 overflow-hidden">
        <div className="p-3 border-b border-zinc-100 dark:border-zinc-800 flex flex-col gap-2">
          <div className="flex items-center justify-between gap-2">
            <span className="text-[11px] uppercase tracking-wider text-zinc-400 dark:text-zinc-500 font-semibold">
              {sourceLabel}
            </span>
            <button
              onClick={() => void refreshPhoneBook()}
              disabled={loading}
              title="Aktualisieren"
              className="text-zinc-400 dark:text-zinc-500 hover:text-blue-500 transition-colors p-1 disabled:opacity-50"
            >
              <RefreshCw size={13} className={loading ? 'animate-spin' : ''} />
            </button>
          </div>
          <SearchInput
            value={search}
            onChange={setSearch}
            placeholder="Kollegen suchen…"
          />
        </div>

        <div className="flex-1 overflow-y-auto">
          {usePhoneBook ? (
            filteredPhoneBook.length === 0 ? (
              <EmptyState
                icon={<Users size={32} />}
                title="Keine Kollegen gefunden"
                description={
                  search ? `Keine Ergebnisse für „${search}"` : 'Noch keine Kollegen verfügbar.'
                }
              />
            ) : (
              filteredPhoneBook.map((entry) => (
                <PhoneBookRow
                  key={`${entry.entityType}-${entry.entityId ?? entry.id}`}
                  entry={entry}
                  onDial={(number) => dial(number)}
                />
              ))
            )
          ) : filteredColleagues.length === 0 ? (
            <EmptyState
              icon={<Users size={32} />}
              title="Keine Kollegen gefunden"
              description={
                search
                  ? `Keine Ergebnisse für „${search}"`
                  : 'Noch keine Kollegen verfügbar.'
              }
            />
          ) : (
            filteredColleagues.map((colleague) => (
              <ColleagueCard
                key={colleague.userId}
                colleague={colleague}
                onDial={(userId) => dial(userId)}
              />
            ))
          )}
        </div>
      </div>
    </div>
  );
}
