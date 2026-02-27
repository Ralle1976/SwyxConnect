import { useState } from 'react';
import { usePresence } from '../../hooks/usePresence';
import { usePresenceStore } from '../../stores/usePresenceStore';
import { useCall } from '../../hooks/useCall';
import { PresenceStatus } from '../../types/swyx';
import ColleagueCard from './ColleagueCard';
import SearchInput from '../common/SearchInput';
import StatusBadge from '../common/StatusBadge';
import EmptyState from '../common/EmptyState';
import TeamsStatusBanner from './TeamsStatusBanner';
import { Users } from 'lucide-react';

const STATUS_OPTIONS: { label: string; value: PresenceStatus }[] = [
  { label: 'Verfügbar', value: PresenceStatus.Available },
  { label: 'Abwesend', value: PresenceStatus.Away },
  { label: 'Beschäftigt', value: PresenceStatus.Busy },
  { label: 'Nicht stören', value: PresenceStatus.DND },
  { label: 'Offline', value: PresenceStatus.Offline },
];

export default function PresenceView() {
  const { ownStatus, setPresence } = usePresence();
  const { colleagues } = usePresenceStore();
  const { dial } = useCall();
  const [search, setSearch] = useState('');

  const filtered = colleagues.filter((c) =>
    c.name.toLowerCase().includes(search.toLowerCase())
  );

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
        <div className="p-3 border-b border-zinc-100 dark:border-zinc-800">
          <SearchInput
            value={search}
            onChange={setSearch}
            placeholder="Kollegen suchen…"
          />
        </div>

        <div className="flex-1 overflow-y-auto">
          {filtered.length === 0 ? (
            <EmptyState
              icon={Users}
              title="Keine Kollegen gefunden"
              description={
                search
                  ? `Keine Ergebnisse für „${search}"`
                  : 'Noch keine Kollegen verfügbar.'
              }
            />
          ) : (
            filtered.map((colleague) => (
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
