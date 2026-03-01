import React, { useEffect } from 'react';
import { Voicemail } from 'lucide-react';
import { useVoicemailStore } from '../../stores/useVoicemailStore';
import { useCall } from '../../hooks/useCall';
import EmptyState from '../common/EmptyState';
import VoicemailEntry from './VoicemailEntry';

export default function VoicemailView(): React.JSX.Element {
  const { messages, loading, fetchVoicemails, markRead, deleteMessage } =
    useVoicemailStore();
  const { dial } = useCall();

  useEffect(() => {
    fetchVoicemails();
  }, [fetchVoicemails]);

  function handlePlay(id: string): void {
    markRead(id);
    // Audio-Wiedergabe erfolgt über den Swyx-Client (SDK-Limitation: kein Audio-Streaming über COM)
  }

  function handleCallback(number: string): void {
    dial(number);
  }

  function handleDelete(id: string): void {
    deleteMessage(id);
  }

  const newCount = messages.filter((m) => m.isNew).length;

  return (
    <div className="flex flex-col h-full w-full bg-white dark:bg-zinc-900 overflow-hidden">
      {/* Header */}
      <div className="flex-none px-6 pt-5 pb-3 border-b border-zinc-200 dark:border-zinc-800">
        <div className="flex items-center gap-2">
          <h1 className="text-lg font-semibold text-zinc-900 dark:text-zinc-100 tracking-tight">
            Voicemail
          </h1>
          {newCount > 0 && (
            <span className="inline-flex items-center justify-center min-w-[1.25rem] h-5 px-1.5 rounded-full bg-blue-500 text-white text-xs font-bold">
              {newCount}
            </span>
          )}
        </div>
        <p className="text-xs text-zinc-500 dark:text-zinc-400 mt-0.5">
          {messages.length} {messages.length === 1 ? 'Nachricht' : 'Nachrichten'}
          {newCount > 0 && ` · ${newCount} neu`}
        </p>
      </div>

      {/* List */}
      <div className="flex-1 overflow-y-auto">
        {loading && (
          <div className="flex items-center justify-center h-24 text-sm text-zinc-400 dark:text-zinc-500">
            Lade Nachrichten…
          </div>
        )}

        {!loading && messages.length === 0 && (
          <EmptyState
            icon={<Voicemail className="w-8 h-8 text-zinc-400 dark:text-zinc-600" />}
            title="Keine Voicemails"
            description="Sie haben keine gespeicherten Sprachnachrichten."
          />
        )}

        {!loading && messages.length > 0 && (
          <ul className="divide-y divide-zinc-100 dark:divide-zinc-800">
            {messages.map((message) => (
              <li key={message.id}>
                <VoicemailEntry
                  message={message}
                  onPlay={handlePlay}
                  onCallback={handleCallback}
                  onDelete={handleDelete}
                />
              </li>
            ))}
          </ul>
        )}
      </div>
    </div>
  );
}
