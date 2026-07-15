import React from 'react';
import { useLineStore } from '../../stores/useLineStore';
import { LineState } from '../../types/swyx';
import { Dialpad } from './Dialpad'
import { LineButtons } from './LineButtons'
import { ActiveCallPanel } from './ActiveCallPanel'

export default function PhoneView(): React.JSX.Element {
  const { lines, selectedLineId } = useLineStore();

  const selectedLine = lines.find((l) => l.id === selectedLineId) ?? null;
  const isCallActive =
    selectedLine !== null &&
    selectedLine.state !== LineState.Inactive &&
    selectedLine.state !== LineState.Disabled &&
    selectedLine.state !== LineState.Terminated;

  return (
    <div className="flex flex-col h-full w-full bg-white dark:bg-zinc-900 overflow-hidden">
      {/* Header */}
      <div className="flex-none px-6 pt-5 pb-3 border-b border-zinc-200 dark:border-zinc-800">
        <h1 className="text-lg font-semibold text-zinc-900 dark:text-zinc-100 tracking-tight">
          Telefon
        </h1>
        <p className="text-xs text-zinc-500 dark:text-zinc-400 mt-0.5">
          Wählen Sie eine Leitung und geben Sie eine Nummer ein
        </p>
      </div>

      {/* Body */}
      <div className="flex flex-1 min-h-0 overflow-hidden flex-col sm:flex-row">
        {/* Left: Dialpad */}
        <div className="flex-none flex flex-col items-center justify-start pt-4 px-6 pb-4 border-b sm:border-b-0 sm:border-r border-zinc-200 dark:border-zinc-800 bg-zinc-50 dark:bg-zinc-900/60">
          <Dialpad />
        </div>

        {/* Right: LineButtons + ActiveCallPanel */}
        <div className="flex flex-1 flex-col min-h-0 overflow-y-auto px-4 py-4 gap-4">
          <LineButtons />
          {isCallActive && selectedLine && (
            <div className="mt-2">
              <ActiveCallPanel />
            </div>
          )}
          {!isCallActive && (
            <div className="flex flex-1 items-center justify-center text-zinc-400 dark:text-zinc-600 text-sm select-none mt-8">
              Kein aktiver Anruf
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
