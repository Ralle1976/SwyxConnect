import React, { useEffect, useMemo, useState } from 'react';
import { Clock } from 'lucide-react';
import { useHistoryStore } from '../../stores/useHistoryStore';
import { useComSocket } from '../../hooks/useComSocket';
import { useCall } from '../../hooks/useCall';
import type { CallHistoryEntry, JournalEntry, JournalPart } from '../../types/swyx';
import EmptyState from '../common/EmptyState';
import HistoryEntry from './HistoryEntry';

type FilterTab = 'alle' | 'inbound' | 'outbound' | 'missed';

const FILTER_TABS: { key: FilterTab; label: string }[] = [
  { key: 'alle', label: 'Alle' },
  { key: 'inbound', label: 'Eingehend' },
  { key: 'outbound', label: 'Ausgehend' },
  { key: 'missed', label: 'Verpasst' },
];

// ComSocket journal kind codes → display direction.
// Spec: 0=incoming, 2=missed; outgoing is inferred as kind 1 and from callState.
function journalDirection(entry: JournalEntry): CallHistoryEntry['direction'] {
  switch (entry.kind) {
    case 0:
      return 'inbound';
    case 2:
      return 'missed';
    case 1:
      return 'outbound';
    default:
      // Fallback heuristic: if we dialled someone, it's outbound.
      return entry.dialedNumber ? 'outbound' : 'inbound';
  }
}

function parseTimestamp(callStart: string): number {
  const ms = Date.parse(callStart);
  return Number.isNaN(ms) ? 0 : ms;
}

// Normalise a ComSocket JournalEntry into the CallHistoryEntry shape that the
// existing HistoryEntry component renders.
function toHistoryEntry(entry: JournalEntry): CallHistoryEntry {
  const direction = journalDirection(entry);
  const isOutbound = direction === 'outbound';
  return {
    id: entry.id,
    callerName: isOutbound ? entry.dialedName ?? entry.name ?? '' : entry.name ?? '',
    callerNumber: isOutbound ? entry.dialedNumber ?? entry.number ?? '' : entry.number ?? '',
    direction,
    timestamp: parseTimestamp(entry.callStart),
    duration: entry.callDuration ?? 0,
  };
}

const PART_FOR_TAB: Record<FilterTab, JournalPart> = {
  alle: 'all',
  inbound: 'incoming',
  outbound: 'outgoing',
  missed: 'missed',
};

export default function HistoryView(): React.JSX.Element {
  const { entries, loading: comLoading, fetchHistory } = useHistoryStore();
  const { dial } = useCall();
  const { journal, available, loading: csLoading, refreshJournal } = useComSocket();
  const [activeFilter, setActiveFilter] = useState<FilterTab>('alle');

  // Initial legacy-COM fetch (kept for fallback and the persistent store).
  useEffect(() => {
    fetchHistory();
  }, [fetchHistory]);

  // Re-query the bridge whenever the active filter changes so the backend can
  // return a pre-filtered journal partition.
  useEffect(() => {
    if (available) {
      void refreshJournal(PART_FOR_TAB[activeFilter]);
    }
  }, [available, activeFilter, refreshJournal]);

  function handleDial(number: string): void {
    dial(number);
  }

  // ComSocket journal is the source of truth when available; otherwise degrade
  // to the COM-backed store entries already rendered by this view.
  const useJournal = available && journal.length > 0;

  const journalEntries = useMemo<CallHistoryEntry[]>(
    () => journal.map(toHistoryEntry).sort((a, b) => b.timestamp - a.timestamp),
    [journal]
  );

  const sourceEntries = useJournal ? journalEntries : entries;

  const filtered: CallHistoryEntry[] = sourceEntries.filter((e) => {
    if (activeFilter === 'alle') return true;
    if (activeFilter === 'inbound') return e.direction === 'inbound';
    if (activeFilter === 'outbound') return e.direction === 'outbound';
    if (activeFilter === 'missed') return e.direction === 'missed';
    return true;
  });

  const sourceLabel = useJournal
    ? `ComSocket · ${journalEntries.length}`
    : `COM · ${entries.length}`;

  return (
    <div className="flex flex-col h-full w-full bg-white dark:bg-zinc-900 overflow-hidden">
      {/* Header */}
      <div className="flex-none px-6 pt-5 pb-3 border-b border-zinc-200 dark:border-zinc-800">
        <div className="flex items-center justify-between">
          <h1 className="text-lg font-semibold text-zinc-900 dark:text-zinc-100 tracking-tight">
            Anrufliste
          </h1>
          <span className="text-[10px] uppercase tracking-wider text-zinc-400 dark:text-zinc-500 font-semibold">
            {sourceLabel}
          </span>
        </div>
        <p className="text-xs text-zinc-500 dark:text-zinc-400 mt-0.5">
          {sourceEntries.length} Einträge
        </p>
      </div>

      {/* Filter tabs */}
      <div className="flex-none flex gap-1 px-4 py-2 border-b border-zinc-200 dark:border-zinc-800 bg-zinc-50 dark:bg-zinc-900/50">
        {FILTER_TABS.map((tab) => {
          const count =
            tab.key === 'alle'
              ? sourceEntries.length
              : sourceEntries.filter((e) =>
                  tab.key === 'missed'
                    ? e.direction === 'missed'
                    : e.direction === tab.key
                ).length;

          return (
            <button
              key={tab.key}
              type="button"
              onClick={() => setActiveFilter(tab.key)}
              className={`flex items-center gap-1.5 px-3 py-1.5 rounded-md text-xs font-medium transition-colors
                ${
                  activeFilter === tab.key
                    ? 'bg-white dark:bg-zinc-700 text-zinc-900 dark:text-zinc-100 shadow-sm'
                    : 'text-zinc-500 dark:text-zinc-400 hover:text-zinc-700 dark:hover:text-zinc-200 hover:bg-zinc-100 dark:hover:bg-zinc-800'
                }`}
            >
              {tab.label}
              {count > 0 && (
                <span
                  className={`text-xs px-1.5 py-0.5 rounded-full font-mono ${
                    activeFilter === tab.key
                      ? 'bg-blue-100 dark:bg-blue-900/40 text-blue-700 dark:text-blue-300'
                      : 'bg-zinc-200 dark:bg-zinc-700 text-zinc-500 dark:text-zinc-400'
                  }`}
                >
                  {count}
                </span>
              )}
            </button>
          );
        })}
      </div>

      {/* List */}
      <div className="flex-1 overflow-y-auto">
        {(comLoading || csLoading) && useJournal === false && (
          <div className="flex items-center justify-center h-24 text-sm text-zinc-400 dark:text-zinc-500">
            Lade Anrufliste…
          </div>
        )}

        {filtered.length === 0 && (
          <EmptyState
            icon={<Clock className="w-8 h-8 text-zinc-400 dark:text-zinc-600" />}
            title="Keine Einträge"
            description={
              activeFilter === 'alle'
                ? 'Die Anrufliste ist leer.'
                : `Keine ${FILTER_TABS.find((t) => t.key === activeFilter)?.label ?? ''} Anrufe vorhanden.`
            }
          />
        )}

        {filtered.length > 0 && (
          <ul className="divide-y divide-zinc-100 dark:divide-zinc-800">
            {filtered.map((entry) => (
              <li key={entry.id}>
                <HistoryEntry entry={entry} onDial={handleDial} />
              </li>
            ))}
          </ul>
        )}
      </div>
    </div>
  );
}
