import { useState, useRef, useCallback, KeyboardEvent } from 'react'
import { Phone } from 'lucide-react'
import { useCall } from '../../hooks/useCall'

const DIALPAD_KEYS = [
  { digit: '1', sub: '' },
  { digit: '2', sub: 'ABC' },
  { digit: '3', sub: 'DEF' },
  { digit: '4', sub: 'GHI' },
  { digit: '5', sub: 'JKL' },
  { digit: '6', sub: 'MNO' },
  { digit: '7', sub: 'PQRS' },
  { digit: '8', sub: 'TUV' },
  { digit: '9', sub: 'WXYZ' },
  { digit: '*', sub: '' },
  { digit: '0', sub: '+' },
  { digit: '#', sub: '' },
] as const

export function Dialpad() {
  const [number, setNumber] = useState('')
  const { dial } = useCall()
  const longPressTimer = useRef<ReturnType<typeof setTimeout> | null>(null)

  const appendDigit = useCallback((digit: string) => {
    setNumber((prev) => prev + digit)
  }, [])

  const handleDial = useCallback(() => {
    const target = number.trim()
    if (!target) return
    dial(target)
    setNumber('')
  }, [number, dial])

  const handleKeyDown = useCallback(
    (e: KeyboardEvent<HTMLInputElement>) => {
      if (e.key === 'Enter') {
        handleDial()
      }
    },
    [handleDial]
  )

  const handlePointerDown = useCallback((digit: string) => {
    if (digit !== '0') return
    longPressTimer.current = setTimeout(() => {
      setNumber((prev) => prev.slice(0, -1) + '+')
    }, 600)
  }, [])

  const handlePointerUp = useCallback(() => {
    if (longPressTimer.current !== null) {
      clearTimeout(longPressTimer.current)
      longPressTimer.current = null
    }
  }, [])

  return (
    <div className="flex flex-col items-center gap-4 w-full max-w-xs mx-auto">
      {/* Number input */}
      <div className="relative w-full">
        <input
          type="tel"
          value={number}
          onChange={(e) => setNumber(e.target.value)}
          onKeyDown={handleKeyDown}
          placeholder="Nummer eingeben…"
          className="w-full px-4 py-3 text-center text-xl font-mono tracking-widest rounded-xl border border-zinc-200 dark:border-zinc-700 bg-white dark:bg-zinc-900 text-zinc-900 dark:text-zinc-100 focus:outline-none focus:ring-2 focus:ring-blue-500 placeholder:text-zinc-400 dark:placeholder:text-zinc-600"
          autoFocus
        />
      </div>

      {/* Dialpad grid */}
      <div className="grid grid-cols-3 gap-2 w-full">
        {DIALPAD_KEYS.map(({ digit, sub }) => (
          <button
            key={digit}
            onPointerDown={() => handlePointerDown(digit)}
            onPointerUp={handlePointerUp}
            onPointerLeave={handlePointerUp}
            onClick={() => appendDigit(digit)}
            className="flex flex-col items-center justify-center h-14 rounded-xl bg-zinc-100 dark:bg-zinc-800 hover:bg-zinc-200 dark:hover:bg-zinc-700 active:scale-95 transition-all duration-75 text-zinc-900 dark:text-zinc-100 select-none cursor-pointer"
          >
            <span className="text-xl font-semibold leading-none">{digit}</span>
            {sub && (
              <span className="text-[9px] tracking-widest text-zinc-500 dark:text-zinc-400 mt-0.5">
                {sub}
              </span>
            )}
          </button>
        ))}
      </div>

      {/* Dial button */}
      <button
        onClick={handleDial}
        disabled={!number.trim()}
        className="flex items-center justify-center gap-2 w-full h-14 rounded-xl bg-emerald-500 hover:bg-emerald-400 active:bg-emerald-600 disabled:opacity-40 disabled:cursor-not-allowed text-white font-semibold text-base transition-colors duration-150 shadow-md shadow-emerald-500/30"
      >
        <Phone size={20} />
        Anrufen
      </button>
    </div>
  )
}
