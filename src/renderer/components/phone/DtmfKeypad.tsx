import { X } from 'lucide-react'
import { useCall } from '../../hooks/useCall'

interface DtmfKeypadProps {
  lineId: number
  onClose: () => void
}

const KEYS = ['1', '2', '3', '4', '5', '6', '7', '8', '9', '*', '0', '#'] as const

const SUB_LABELS: Record<string, string> = {
  '2': 'ABC',
  '3': 'DEF',
  '4': 'GHI',
  '5': 'JKL',
  '6': 'MNO',
  '7': 'PQRS',
  '8': 'TUV',
  '9': 'WXYZ',
  '0': '+',
}

export function DtmfKeypad({ lineId, onClose }: DtmfKeypadProps) {
  const { sendDtmf } = useCall()

  return (
    <div className="absolute -top-4 left-1/2 -translate-x-1/2 -translate-y-full z-10 bg-white dark:bg-zinc-900 rounded-xl shadow-2xl border border-zinc-200 dark:border-zinc-800 p-3 w-52">
      {/* Header */}
      <div className="flex items-center justify-between mb-2">
        <span className="text-xs font-semibold text-zinc-500">DTMF</span>
        <button
          onClick={onClose}
          className="p-0.5 rounded-md hover:bg-zinc-100 dark:hover:bg-zinc-800 transition-colors text-zinc-400 hover:text-zinc-600 dark:hover:text-zinc-200"
        >
          <X size={14} />
        </button>
      </div>

      {/* Grid */}
      <div className="grid grid-cols-3 gap-1.5">
        {KEYS.map((key) => {
          const sub = SUB_LABELS[key]
          return (
            <button
              key={key}
              onClick={() => sendDtmf(lineId, key)}
              className="flex flex-col items-center justify-center w-14 h-12 rounded-lg bg-zinc-100 dark:bg-zinc-800 hover:bg-zinc-200 dark:hover:bg-zinc-700 active:bg-blue-100 dark:active:bg-blue-950 transition-colors"
            >
              <span className="text-base font-semibold text-zinc-800 dark:text-zinc-200 leading-none">
                {key}
              </span>
              {sub !== undefined ? (
                <span className="text-[8px] leading-none text-zinc-400 dark:text-zinc-500 tracking-widest mt-0.5">
                  {sub}
                </span>
              ) : (
                <span className="text-[8px] leading-none mt-0.5 invisible">_</span>
              )}
            </button>
          )
        })}
      </div>
    </div>
  )
}
