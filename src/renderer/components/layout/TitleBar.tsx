import { Minus, Square, X } from 'lucide-react'
import { useBridge } from '../../hooks/useBridge'

const connectionDot: Record<string, string> = {
  Connected: 'bg-emerald-500',
  Restarting: 'bg-amber-400 animate-pulse',
  Failed: 'bg-red-500',
  Disconnected: 'bg-zinc-400',
  Starting: 'bg-blue-400 animate-pulse',
}

const connectionLabel: Record<string, string> = {
  Connected: 'Verbunden',
  Restarting: 'Verbindet…',
  Failed: 'Fehler',
  Disconnected: 'Getrennt',
  Starting: 'Verbinde…',
}

export function TitleBar() {
  const { bridgeState } = useBridge()
  const state = bridgeState
  const dotClass = connectionDot[state] ?? 'bg-zinc-400'
  const label = connectionLabel[state] ?? 'Unbekannt'

  const handleMinimize = () => {
    window.windowControls?.minimize()
  }

  const handleMaximize = () => {
    window.windowControls?.maximize()
  }

  const handleClose = () => {
    window.windowControls?.close()
  }

  return (
    <div
      className="h-8 flex items-center justify-between px-2 bg-zinc-100 dark:bg-zinc-900 border-b border-zinc-200 dark:border-zinc-800 flex-shrink-0 z-50"
      style={{ WebkitAppRegion: 'drag' } as React.CSSProperties}
    >
      {/* Left: App name + status */}
      <div className="flex items-center gap-2">
        <span className="text-xs font-semibold tracking-wider text-zinc-700 dark:text-zinc-300 uppercase">
          SwyxConnect
        </span>
        <span className="text-[9px] font-normal tracking-wide text-zinc-400 dark:text-zinc-500 lowercase">
          by Ralle1976
        </span>
        <div className="flex items-center gap-1">
          <span className={`w-2 h-2 rounded-full flex-shrink-0 ${dotClass}`} />
          <span className="text-[10px] text-zinc-500 dark:text-zinc-400 hidden sm:inline">
            {label}
          </span>
        </div>
      </div>

      {/* Right: Window controls */}
      <div
        className="flex items-center gap-0.5"
        style={{ WebkitAppRegion: 'no-drag' } as React.CSSProperties}
      >
        <button
          onClick={handleMinimize}
          title="Minimieren"
          className="w-7 h-7 flex items-center justify-center rounded hover:bg-zinc-200 dark:hover:bg-zinc-700 text-zinc-500 dark:text-zinc-400 transition-colors"
        >
          <Minus size={12} />
        </button>
        <button
          onClick={handleMaximize}
          title="Maximieren"
          className="w-7 h-7 flex items-center justify-center rounded hover:bg-zinc-200 dark:hover:bg-zinc-700 text-zinc-500 dark:text-zinc-400 transition-colors"
        >
          <Square size={11} />
        </button>
        <button
          onClick={handleClose}
          title="Schließen"
          className="w-7 h-7 flex items-center justify-center rounded hover:bg-red-500 hover:text-white text-zinc-500 dark:text-zinc-400 transition-colors"
        >
          <X size={13} />
        </button>
      </div>
    </div>
  )
}
