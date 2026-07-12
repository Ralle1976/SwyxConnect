import { useState, useEffect, useCallback } from 'react'
import {
  PhoneOff,
  Pause,
  Play,
  Mic,
  MicOff,
  ArrowRightLeft,
  Hash,
  Circle,
  Users,
  PhoneForwarded,
} from 'lucide-react'
import { useCall } from '../../hooks/useCall'
import { useLineStore } from '../../stores/useLineStore'
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
  const { selectedLineId, lines } = useLineStore()
  const { hangup, hold, unhold, mute, unmute, transfer } = useCall()
  const [elapsed, setElapsed] = useState(0)
  const [isMuted, setIsMuted] = useState(false)
  const [isOnHold, setIsOnHold] = useState(false)
  const [showTransfer, setShowTransfer] = useState(false)
  const [showDtmf, setShowDtmf] = useState(false)
  const [isRecording, setIsRecording] = useState(false)
  const [showForward, setShowForward] = useState(false)
  const [forwardTarget, setForwardTarget] = useState('')
  const activeLine = lines.find((l) => l.id === selectedLineId)
  const isRinging = activeLine?.state === 'Ringing'
  const isDialing = activeLine?.state === 'Dialing' || activeLine?.state === 'Alerting'
  const isVisible =
    activeLine &&
    activeLine.state !== 'Inactive' &&
    activeLine.state !== 'Terminated'

  // Reset timer when active line changes
  useEffect(() => {
    setElapsed(0)
    setIsMuted(false)
    setIsOnHold(false)
    setIsRecording(false)
    setShowForward(false)
    setForwardTarget('')
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

  const handleRecordToggle = useCallback(() => {
    if (!activeLine) return
    const lineNumber = activeLine.id
    if (isRecording) {
      window.swyxApi.stopRecording(lineNumber).catch(() => {})
    } else {
      window.swyxApi.startRecording(lineNumber).catch(() => {})
    }
    setIsRecording((v) => !v)
  }, [activeLine, isRecording])

  const handleConference = useCallback(() => {
    if (!activeLine) return
    window.swyxApi.createConference(activeLine.id).catch(() => {})
  }, [activeLine])

  const handleForwardCall = useCallback(() => {
    if (!activeLine || !forwardTarget.trim()) return
    window.swyxApi.forwardCall(activeLine.id, forwardTarget.trim()).catch(() => {})
    setShowForward(false)
    setForwardTarget('')
  }, [activeLine, forwardTarget])

  // Anzahl aktiver Leitungen (für Konferenz-Button)
  const activeLineCount = lines.filter(
    (l) => l.state !== 'Inactive' && l.state !== 'Terminated' && l.state !== 'Disabled'
  ).length

  if (!isVisible || !activeLine) return null

  return (
    <div className="relative flex flex-col items-center gap-5 w-full max-w-xs mx-auto p-5 rounded-2xl bg-white dark:bg-zinc-900 border border-zinc-200 dark:border-zinc-800 shadow-xl shadow-black/5">
      {/* Pulse ring for ringing state */}
      {isRinging && (
        <span className="absolute inset-0 rounded-2xl animate-ping bg-emerald-400/20 pointer-events-none" />
      )}

      {/* Caller avatar */}
      <div className="relative">
        <Avatar name={activeLine.callerName ?? activeLine.callerNumber ?? '?'} size="lg" />
        {isRinging && (
          <span className="absolute -inset-2 rounded-full border-2 border-emerald-400 animate-ping opacity-75" />
        )}
      </div>

      {/* Caller info */}
      <div className="text-center">
        <p className="text-base font-semibold text-zinc-900 dark:text-zinc-100 leading-tight">
          {activeLine.callerName ?? activeLine.callerNumber ?? 'Unbekannt'}
        </p>
        {activeLine.callerName && activeLine.callerNumber && (
          <p className="text-sm font-mono text-zinc-500 dark:text-zinc-400 mt-0.5">
            {activeLine.callerNumber}
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

      {/* Forward call inline input */}
      {showForward && (
        <div className="flex items-center gap-2 w-full px-1">
          <input
            type="text"
            value={forwardTarget}
            onChange={(e) => setForwardTarget(e.target.value)}
            onKeyDown={(e) => e.key === 'Enter' && handleForwardCall()}
            placeholder="Zielnummer…"
            className="flex-1 px-3 py-1.5 rounded-lg bg-zinc-100 dark:bg-zinc-800 border border-zinc-200 dark:border-zinc-700 text-sm text-zinc-800 dark:text-zinc-200 placeholder:text-zinc-400 dark:placeholder:text-zinc-500 focus:outline-none focus:ring-2 focus:ring-blue-500/40"
            autoFocus
          />
          <button
            onClick={handleForwardCall}
            disabled={!forwardTarget.trim()}
            className="px-3 py-1.5 rounded-lg bg-blue-500 hover:bg-blue-400 disabled:opacity-40 text-white text-xs font-medium transition-colors"
          >
            Umleiten
          </button>
          <button
            onClick={() => { setShowForward(false); setForwardTarget('') }}
            className="px-2 py-1.5 rounded-lg text-zinc-400 hover:text-zinc-600 dark:hover:text-zinc-200 text-xs transition-colors"
          >
            ×
          </button>
        </div>
      )}

      {/* Action buttons - Row 1: Primary controls */}
      <div className="flex items-center gap-2 flex-wrap justify-center">
        <ActionButton onClick={handleHoldToggle} active={isOnHold} title={isOnHold ? 'Fortsetzen' : 'Halten'}>
          {isOnHold ? <Play size={18} /> : <Pause size={18} />}
          <span className="text-[10px] font-medium">{isOnHold ? 'Fortsetzen' : 'Halten'}</span>
        </ActionButton>

        <ActionButton onClick={handleMuteToggle} active={isMuted} title={isMuted ? 'Stummsch. aus' : 'Stummschalten'}>
          {isMuted ? <MicOff size={18} /> : <Mic size={18} />}
          <span className="text-[10px] font-medium">{isMuted ? 'Ton ein' : 'Stumm'}</span>
        </ActionButton>

        <ActionButton onClick={handleRecordToggle} active={isRecording} title={isRecording ? 'Aufnahme stoppen' : 'Aufnahme starten'}>
          <Circle size={18} className={isRecording ? 'fill-red-500 text-red-500 animate-pulse' : ''} />
          <span className="text-[10px] font-medium">{isRecording ? 'Stopp' : 'Aufnahme'}</span>
        </ActionButton>

        <ActionButton onClick={() => setShowTransfer(true)} title="Verbinden">
          <ArrowRightLeft size={18} />
          <span className="text-[10px] font-medium">Verbinden</span>
        </ActionButton>

        <ActionButton onClick={() => setShowForward(!showForward)} active={showForward} title="Umleiten">
          <PhoneForwarded size={18} />
          <span className="text-[10px] font-medium">Umleiten</span>
        </ActionButton>

        {activeLineCount >= 2 && (
          <ActionButton onClick={handleConference} title="Konferenz starten">
            <Users size={18} />
            <span className="text-[10px] font-medium">Konferenz</span>
          </ActionButton>
        )}

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
