import React, { useState } from 'react';
import {
  ArrowDownLeft,
  ArrowUpRight,
  PhoneMissed,
  Phone,
  UserPlus,
} from 'lucide-react';
import { CallHistoryEntry } from '../../types/swyx';

interface HistoryEntryProps {
  entry: CallHistoryEntry;
  onDial: (number: string) => void;
  /** Wird aufgerufen wenn "Kontakt erstellen" geklickt wird */
  onAddContact?: (number: string) => void;
  /** Aufgelöster Name aus lokalem Telefonbuch (überschreibt Anzeige) */
  resolvedName?: string;
}

/** Format seconds → mm:ss */
function formatDuration(seconds: number): string {
  if (seconds <= 0) return '0:00';
  const m = Math.floor(seconds / 60);
  const s = seconds % 60;
  return `${m}:${s.toString().padStart(2, '0')}`;
}

/** Relative German timestamp */
function formatRelativeTime(timestamp: number): string {
  const diffMs = Date.now() - timestamp;
  const diffMin = Math.floor(diffMs / 60_000);
  const diffHours = Math.floor(diffMs / 3_600_000);
  const diffDays = Math.floor(diffMs / 86_400_000);

  if (diffMin < 1) return 'Gerade eben';
  if (diffMin < 60) return `vor ${diffMin} Min.`;
  if (diffHours < 24) return `vor ${diffHours} Std.`;
  if (diffDays === 1) return 'Gestern';
  if (diffDays < 7) return `vor ${diffDays} Tagen`;

  return new Date(timestamp).toLocaleDateString('de-DE', {
    day: '2-digit',
    month: '2-digit',
    year: '2-digit',
  });
}

type Direction = 'inbound' | 'outbound' | 'missed';

const DIRECTION_META: Record<
  Direction,
  { icon: React.ElementType; colorClass: string; label: string }
> = {
  inbound: {
    icon: ArrowDownLeft,
    colorClass: 'text-green-500 dark:text-green-400',
    label: 'Eingehend',
  },
  outbound: {
    icon: ArrowUpRight,
    colorClass: 'text-blue-500 dark:text-blue-400',
    label: 'Ausgehend',
  },
  missed: {
    icon: PhoneMissed,
    colorClass: 'text-red-500 dark:text-red-400',
    label: 'Verpasst',
  },
};

export default function HistoryEntry({
  entry,
  onDial,
  onAddContact,
  resolvedName,
}: HistoryEntryProps): React.JSX.Element {
  const [hovered, setHovered] = useState(false);
  const meta = DIRECTION_META[entry.direction];
  const Icon = meta.icon;

  function handleRedial(e: React.MouseEvent<HTMLButtonElement>): void {
    e.stopPropagation();
    onDial(entry.callerNumber);
  }

  function handleAddContact(e: React.MouseEvent<HTMLButtonElement>): void {
    e.stopPropagation();
    onAddContact?.(entry.callerNumber);
  }

  return (
    <div
      role="button"
      tabIndex={0}
      onClick={() => onDial(entry.callerNumber)}
      onKeyDown={(e) => e.key === 'Enter' && onDial(entry.callerNumber)}
      onMouseEnter={() => setHovered(true)}
      onMouseLeave={() => setHovered(false)}
      className="flex items-center gap-3 px-4 py-3 cursor-pointer hover:bg-zinc-50 dark:hover:bg-zinc-800/60 transition-colors outline-none focus-visible:ring-2 focus-visible:ring-blue-500"
      title={`${meta.label}: ${entry.callerNumber}`}
    >
      {/* Direction icon */}
      <div className={`flex-none ${meta.colorClass}`}>
        <Icon className="w-5 h-5" />
      </div>

      {/* Caller info */}
      <div className="flex-1 min-w-0">
        <div className="flex items-center gap-2">
          <span
            className={`text-sm font-medium truncate ${
              entry.direction === 'missed'
                ? 'text-red-600 dark:text-red-400'
                : 'text-zinc-900 dark:text-zinc-100'
            }`}
          >
            {resolvedName ?? entry.callerName ?? entry.callerNumber}
          </span>
        </div>
        {(resolvedName ?? entry.callerName) && (
          <div className="text-xs text-zinc-500 dark:text-zinc-400 mt-0.5 font-mono truncate">
            {entry.callerNumber}
          </div>
        )}
      </div>

      {/* Right: time + duration */}
      <div className="flex-none flex flex-col items-end gap-0.5 text-xs text-zinc-400 dark:text-zinc-500">
        <span>{formatRelativeTime(entry.timestamp)}</span>
        {entry.direction !== 'missed' && entry.duration > 0 && (
          <span className="font-mono">{formatDuration(entry.duration)}</span>
        )}
      </div>

      {/* Aktionen: Kontakt erstellen + Rückruf */}
      <div
        className={`flex items-center gap-1 flex-none ml-1 transition-opacity duration-100 ${
          hovered ? 'opacity-100' : 'opacity-0 pointer-events-none'
        }`}
      >
        {onAddContact && !resolvedName && !entry.callerName && (
          <button
            type="button"
            onClick={handleAddContact}
            title="Kontakt erstellen"
            className="flex items-center justify-center w-8 h-8 rounded-full bg-blue-500 hover:bg-blue-600 text-white shadow-sm transition-colors"
          >
            <UserPlus className="w-4 h-4" />
          </button>
        )}
        <button
          type="button"
          onClick={handleRedial}
          title="Rückruf"
          className="flex items-center justify-center w-8 h-8 rounded-full bg-green-500 hover:bg-green-600 text-white shadow-sm transition-colors"
        >
          <Phone className="w-4 h-4" />
        </button>
      </div>
    </div>
  );
}
