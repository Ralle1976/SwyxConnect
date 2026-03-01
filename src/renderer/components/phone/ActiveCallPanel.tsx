import { useState, useEffect, useCallback } from 'react'
import {
  PhoneOff,
  Pause,
  Play,
  Mic,
  MicOff,
  ArrowRightLeft,
  Hash,
} from 'lucide-react'
import { useCall } from '../../hooks/useCall'
import { useLineStore } from '../../stores/useLineStore'
import { useLocalContactStore } from '../../stores/useLocalContactStore'
import { LineInfo } from '../../types/swyx'
import { Avatar } from '../common/Avatar'
import { TransferDialog } from './TransferDialog'
import { DtmfKeypad } from './DtmfKeypad'

function formatDuration(seconds: number): string {
  const h = Math.floor(seconds / 3600)
  const m = Math.floor((seconds % 3600) / 60)
  const s = seconds % 60
  if (h > 0) {
    return `${h}:${String(m).padStart(2, '0')}:${String(s).padStart(2, '0')}`
  }
  return `${String(m).padStart(2, '0')}:${String(s).padStart(2, '0')}`
}

interface ActionButtonProps {
  onClick: () => void
  active?: boolean
  danger?: boolean
  title: string
  children: React.ReactNode
}

function ActionButton({ onClick, active, danger, title, children }: ActionButtonProps) {
  return (
    <button
      onClick={onClick}
      title={title}
      className={`
        flex flex-col items-center justify-center gap-1 w-16 h-16 rounded-xl transition-all duration-150 active:scale-95
        ${danger
          ? 'bg-red-500 hover:bg-red-400 text-white shadow-lg shadow-red-500/30'
          : active
            ? 'bg-blue-100 dark:bg-blue-950 text-blue-600 dark:text-blue-400 ring-1 ring-blue-300 dark:ring-blue-700'
            : 'bg-zinc-100 dark:bg-zinc-800 text-zinc-600 dark:text-zinc-400 hover:bg-zinc-200 dark:hover:bg-zinc-700'
        }
      `}
    >
      {children}
    </button>
  )
}

export function ActiveCallPanel() {
  const { selectedLineId, lines, getDisplayNumber } = useLineStore()
  const findByNumber = useLocalContactStore((s) => s.findByNumber)
  const { hangup, hold, unhold, mute, unmute } = useCall()
  const [elapsed, setElapsed] = useState(0)
  const [isMuted, setIsMuted] = useState(false)
  const [isOnHold, setIsOnHold] = useState(false)
  const [showTransfer, setShowTransfer] = useState(false)
  const [showDtmf, setShowDtmf] = useState(false)

  const activeLine = lines.find((l: LineInfo) => l.id === selectedLineId)
  const isRinging = activeLine?.state === 'Ringing'
  const isDialing = activeLine?.state === 'Dialing' || activeLine?.state === 'Alerting'
  const isVisible =
    activeLine &&
    activeLine.state !== 'Inactive' &&
    activeLine.state !== 'Terminated'

  // Lokalen Kontakt nach Rufnummer suchen (COM-Name hat Vorrang wenn vorhanden)
  const displayNumber = activeLine ? getDisplayNumber(activeLine) : ''
  const comName = activeLine ? (activeLine.callerName ?? '') : ''
  const localContact = displayNumber ? findByNumber(displayNumber) : undefined
  // COM-Name hat Vorrang; lokaler Kontaktname als Fallback wenn COM leer
  const resolvedDisplayName = comName || localContact?.name || displayNumber || 'Unbekannt'
  // Reset timer when active line changes
  useEffect(() => {
    setElapsed(0)
    setIsMuted(false)
    setIsOnHold(false)
  }, [selectedLineId])

  // Count up timer when call is active (not ringing or dialing)
  useEffect(() => {
    if (!isVisible || isRinging || isDialing) return
    const timer = setInterval(() => setElapsed((s) => s + 1), 1000)
    return () => clearInterval(timer)
  }, [isVisible, isRinging, isDialing])

  const handleHoldToggle = useCallback(() => {
    if (!activeLine) return
    if (isOnHold) {
      unhold(activeLine.id)
    } else {
      hold(activeLine.id)
    }
    setIsOnHold((v) => !v)
  }, [activeLine, isOnHold, hold, unhold])

  const handleMuteToggle = useCallback(() => {
    if (!activeLine) return
    if (isMuted) {
      unmute(activeLine.id)
    } else {
      mute(activeLine.id)
    }
    setIsMuted((v) => !v)
  }, [activeLine, isMuted, mute, unmute])

  const handleHangup = useCallback(() => {
    if (!activeLine) return
    hangup(activeLine.id)
  }, [activeLine, hangup])

  if (!isVisible || !activeLine) return null

  return (
    <div className="relative flex flex-col items-center gap-5 w-full max-w-xs mx-auto p-5 rounded-2xl bg-white dark:bg-zinc-900 border border-zinc-200 dark:border-zinc-800 shadow-xl shadow-black/5">
      {/* Pulse ring for ringing state */}
      {isRinging && (
        <span className="absolute inset-0 rounded-2xl animate-ping bg-emerald-400/20 pointer-events-none" />
      )}

      {/* Caller avatar */}
      <div className="relative">
        <Avatar name={resolvedDisplayName} size="lg" />
        {isRinging && (
          <span className="absolute -inset-2 rounded-full border-2 border-emerald-400 animate-ping opacity-75" />
        )}
      </div>

      {/* Caller info */}
      <div className="text-center">
        <p className="text-base font-semibold text-zinc-900 dark:text-zinc-100 leading-tight">
          {resolvedDisplayName}
        </p>
        {displayNumber && resolvedDisplayName !== displayNumber && (
          <p className="text-sm font-mono text-zinc-500 dark:text-zinc-400 mt-0.5">
            {displayNumber}
          </p>
        )}
        <p className="text-xs text-zinc-400 dark:text-zinc-500 mt-1 font-mono tabular-nums">
          {isRinging ? 'Eingehend…' : isDialing ? 'Wird angerufen…' : formatDuration(elapsed)}
        </p>
      </div>

      {/* DTMF keypad (floating above) */}
      {showDtmf && (
        <DtmfKeypad lineId={activeLine.id} onClose={() => setShowDtmf(false)} />
      )}

      {/* Transfer dialog */}
      {showTransfer && (
        <TransferDialog lineId={activeLine.id} onClose={() => setShowTransfer(false)} />
      )}

      {/* Action buttons */}
      <div className="flex items-center gap-3">
        <ActionButton onClick={handleHoldToggle} active={isOnHold} title={isOnHold ? 'Fortsetzen' : 'Halten'}>
          {isOnHold ? <Play size={18} /> : <Pause size={18} />}
          <span className="text-[10px] font-medium">{isOnHold ? 'Fortsetzen' : 'Halten'}</span>
        </ActionButton>

        <ActionButton onClick={handleMuteToggle} active={isMuted} title={isMuted ? 'Stummsch. aus' : 'Stummschalten'}>
          {isMuted ? <MicOff size={18} /> : <Mic size={18} />}
          <span className="text-[10px] font-medium">{isMuted ? 'Ton ein' : 'Stumm'}</span>
        </ActionButton>

        <ActionButton onClick={() => setShowTransfer(true)} title="Weiterleiten">
          <ArrowRightLeft size={18} />
          <span className="text-[10px] font-medium">Weiterl.</span>
        </ActionButton>

        <ActionButton onClick={() => setShowDtmf(true)} title="DTMF">
          <Hash size={18} />
          <span className="text-[10px] font-medium">DTMF</span>
        </ActionButton>

        <ActionButton onClick={handleHangup} danger title="Auflegen">
          <PhoneOff size={18} />
          <span className="text-[10px] font-medium">Auflegen</span>
        </ActionButton>
      </div>
    </div>
  )
}
