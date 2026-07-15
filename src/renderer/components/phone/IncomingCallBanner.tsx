import { Phone, PhoneOff } from 'lucide-react'
import { useCall } from '../../hooks/useCall'
import { Avatar } from '../common/Avatar'

interface IncomingCallBannerProps {
  lineId: number
  callerName: string
  callerNumber: string
}

export function IncomingCallBanner({ lineId, callerName, callerNumber }: IncomingCallBannerProps) {
  const { answer, hangup } = useCall()

  const displayName = callerName || callerNumber || 'Unbekannt'

  return (
    <div
      className="
        relative w-full z-50 overflow-hidden
        bg-gradient-to-r from-emerald-600 via-emerald-500 to-teal-500
        dark:from-emerald-800 dark:via-emerald-700 dark:to-teal-700
        text-white
        animate-[slideDown_0.3s_ease-out]
        shadow-lg shadow-emerald-500/30
      "
      style={{
        animation: 'slideDown 0.3s ease-out',
      }}
    >
      {/* Animated pulse background overlay */}
      <div className="absolute inset-0 bg-white/10 animate-pulse pointer-events-none" />

      <div className="relative flex items-center gap-3 px-4 py-3">
        {/* Avatar */}
        <div className="relative flex-shrink-0">
          <Avatar name={displayName} size="md" />
          <span className="absolute -inset-1 rounded-full border-2 border-white/60 animate-ping" />
        </div>

        {/* Caller info */}
        <div className="flex-1 min-w-0">
          <p className="text-xs font-medium text-white/70">Eingehender Anruf · Leitung {lineId}</p>
          <p className="text-sm font-bold leading-tight truncate">{displayName}</p>
          {callerName && callerNumber && (
            <p className="text-xs font-mono text-white/80 truncate">{callerNumber}</p>
          )}
        </div>

        {/* Action buttons */}
        <div className="flex items-center gap-2 flex-shrink-0">
          <button
            onClick={() => hangup(lineId)}
            title="Ablehnen"
            className="flex items-center gap-1.5 px-3 py-2 rounded-lg bg-red-500 hover:bg-red-400 active:bg-red-600 text-white text-xs font-semibold transition-colors shadow-md"
          >
            <PhoneOff size={14} />
            Ablehnen
          </button>
          <button
            onClick={() => answer(lineId)}
            title="Annehmen"
            className="flex items-center gap-1.5 px-3 py-2 rounded-lg bg-white text-emerald-700 hover:bg-emerald-50 active:bg-emerald-100 text-xs font-semibold transition-colors shadow-md"
          >
            <Phone size={14} />
            Annehmen
          </button>
        </div>
      </div>

      <style>{`
        @keyframes slideDown {
          from { transform: translateY(-100%); opacity: 0; }
          to { transform: translateY(0); opacity: 1; }
        }
      `}</style>
    </div>
  )
}
