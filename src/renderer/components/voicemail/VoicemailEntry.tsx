import React from 'react';
import { Play, Phone, Trash2 } from 'lucide-react';
import { VoicemailEntry as VoicemailEntryType } from '../../types/swyx';

interface VoicemailEntryProps {
  message: VoicemailEntryType;
  onPlay: (id: string) => void;
  onCallback: (number: string) => void;
  onDelete: (id: string) => void;
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

export default function VoicemailEntry({
  message,
  onPlay,
  onCallback,
  onDelete,
}: VoicemailEntryProps): React.JSX.Element {
  return (
    <div
      className={`flex items-start gap-3 px-4 py-3 transition-colors hover:bg-zinc-50 dark:hover:bg-zinc-800/60 ${
        message.isNew ? 'bg-blue-50/40 dark:bg-blue-950/20' : ''
      }`}
    >
      {/* New indicator dot */}
      <div className="flex-none flex items-center justify-center w-5 pt-1">
        {message.isNew && (
          <span
            className="w-2 h-2 rounded-full bg-blue-500 shadow-sm"
            title="Neue Nachricht"
          />
        )}
      </div>

      {/* Content */}
      <div className="flex-1 min-w-0">
        <div className="flex items-center gap-2 flex-wrap">
          <span
            className={`text-sm font-medium truncate ${
              message.isNew
                ? 'text-zinc-900 dark:text-zinc-50'
                : 'text-zinc-700 dark:text-zinc-300'
            }`}
          >
            {message.callerName || message.callerNumber}
          </span>
          {message.isNew && (
            <span className="text-xs px-1.5 py-0.5 rounded-full bg-blue-500 text-white font-semibold leading-none">
              Neu
            </span>
          )}
        </div>

        {message.callerName && (
          <div className="text-xs text-zinc-500 dark:text-zinc-400 font-mono mt-0.5 truncate">
            {message.callerNumber}
          </div>
        )}

        <div className="flex items-center gap-2 mt-1 text-xs text-zinc-400 dark:text-zinc-500">
          <span>{formatRelativeTime(message.timestamp)}</span>
          <span className="text-zinc-300 dark:text-zinc-600">·</span>
          <span className="font-mono">{formatDuration(message.duration)}</span>
        </div>
      </div>

      {/* Action buttons */}
      <div className="flex-none flex items-center gap-1">
        {/* Play */}
        <button
          type="button"
          onClick={() => onPlay(message.id)}
          title="Abspielen"
          className="flex items-center justify-center w-8 h-8 rounded-full bg-zinc-100 dark:bg-zinc-700 hover:bg-blue-100 dark:hover:bg-blue-900/40 text-zinc-600 dark:text-zinc-300 hover:text-blue-600 dark:hover:text-blue-400 transition-colors"
        >
          <Play className="w-3.5 h-3.5" />
        </button>

        {/* Callback */}
        <button
          type="button"
          onClick={() => onCallback(message.callerNumber)}
          title="Rückruf"
          className="flex items-center justify-center w-8 h-8 rounded-full bg-zinc-100 dark:bg-zinc-700 hover:bg-green-100 dark:hover:bg-green-900/40 text-zinc-600 dark:text-zinc-300 hover:text-green-600 dark:hover:text-green-400 transition-colors"
        >
          <Phone className="w-3.5 h-3.5" />
        </button>

        {/* Delete */}
        <button
          type="button"
          onClick={() => onDelete(message.id)}
          title="Löschen"
          className="flex items-center justify-center w-8 h-8 rounded-full bg-zinc-100 dark:bg-zinc-700 hover:bg-red-100 dark:hover:bg-red-900/40 text-zinc-600 dark:text-zinc-300 hover:text-red-600 dark:hover:text-red-400 transition-colors"
        >
          <Trash2 className="w-3.5 h-3.5" />
        </button>
      </div>
    </div>
  );
}
