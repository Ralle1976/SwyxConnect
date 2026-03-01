import { useLineStore } from '../../stores/useLineStore'
import type { LineState } from '../../types/swyx'

const stateColors: Record<LineState | string, string> = {
  Active: 'bg-emerald-500 shadow-emerald-500/40',
  OnHold: 'bg-amber-400 shadow-amber-400/40',
  Ringing: 'bg-red-500 shadow-red-500/40 animate-pulse',
  Busy: 'bg-red-600 shadow-red-600/40',
  Inactive: 'bg-zinc-300 dark:bg-zinc-600 shadow-none',
  Terminated: 'bg-zinc-300 dark:bg-zinc-600 shadow-none',
}

const stateLabelMap: Record<LineState | string, string> = {
  Active: 'Aktiv',
  OnHold: 'Gehalten',
  Ringing: 'Klingelt',
  Busy: 'Besetzt',
  Inactive: 'Frei',
  Terminated: 'Beendet',
  Disabled: 'Deaktiviert',
  HookOffInternal: 'Intern',
  HookOffExternal: 'Extern',
  Dialing: 'W\u00e4hlt',
  Alerting: 'Rufton',
  Knocking: 'Anklopfen',
  ConferenceActive: 'Konferenz',
  ConferenceOnHold: 'Konferenz (gehalten)',
  Transferring: 'Weiterleitung',
  DirectCall: 'Direktruf',
}

const selectedRing =
  'ring-2 ring-blue-500 dark:ring-blue-400 ring-offset-1 ring-offset-white dark:ring-offset-zinc-900'

export function LineButtons() {
  const { lines, selectedLineId, selectLine } = useLineStore()

  return (
    <div className="grid grid-cols-2 gap-2 w-full">
      {lines.map((line) => {
        const isSelected = line.id === selectedLineId
        const dotClass = stateColors[line.state] ?? stateColors.Inactive
        const stateLabel = stateLabelMap[line.state] ?? line.state
        const isActive = line.state !== 'Inactive' && line.state !== 'Terminated'

        return (
          <button
            key={line.id}
            onClick={() => selectLine(line.id)}
            className={`
              relative flex flex-col items-start px-3 py-2.5 rounded-xl border transition-all duration-150
              ${isSelected
                ? `bg-blue-50 dark:bg-blue-950 border-blue-200 dark:border-blue-800 ${selectedRing}`
                : 'bg-white dark:bg-zinc-900 border-zinc-200 dark:border-zinc-800 hover:border-zinc-300 dark:hover:border-zinc-700'
              }
            `}
          >
            {/* Line number + state dot */}
            <div className="flex items-center gap-2 w-full">
              <span className="text-xs font-semibold text-zinc-500 dark:text-zinc-400">
                Leitung {line.id + 1}
              </span>
              <span
                className={`ml-auto w-2.5 h-2.5 rounded-full shadow-md flex-shrink-0 ${dotClass}`}
              />
            </div>

            {/* State label */}
            <span
              className={`text-[11px] font-medium mt-0.5 ${
                isActive
                  ? 'text-zinc-700 dark:text-zinc-200'
                  : 'text-zinc-400 dark:text-zinc-600'
              }`}
            >
              {stateLabel}
            </span>

            {/* Caller info */}
            {isActive && (line.callerName || line.callerNumber) && (
              <div className="mt-1 w-full">
                {line.callerName && (
                  <p className="text-xs font-semibold text-zinc-800 dark:text-zinc-100 truncate leading-tight">
                    {line.callerName}
                  </p>
                )}
                {line.callerNumber && (
                  <p className="text-[11px] text-zinc-500 dark:text-zinc-400 truncate leading-tight font-mono">
                    {line.callerNumber}
                  </p>
                )}
              </div>
            )}
          </button>
        )
      })}
    </div>
  )
}
