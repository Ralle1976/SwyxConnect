import React, { useEffect, useRef } from 'react'
import { createPortal } from 'react-dom'
import { X, Keyboard } from 'lucide-react'

interface ShortcutEntry {
  keys: string[]
  label: string
}

interface ShortcutGroup {
  title: string
  shortcuts: ShortcutEntry[]
}

const SHORTCUT_GROUPS: ShortcutGroup[] = [
  {
    title: 'Anrufsteuerung',
    shortcuts: [
      { keys: ['F5'], label: 'Anruf annehmen' },
      { keys: ['F6'], label: 'Auflegen' },
      { keys: ['F7'], label: 'Halten / Wiederaufnehmen' },
      { keys: ['F8'], label: 'Stummschalten' },
      { keys: ['Esc'], label: 'Aktiven Anruf beenden' },
    ],
  },
  {
    title: 'Navigation',
    shortcuts: [
      { keys: ['?'], label: 'Diese Hilfe anzeigen' },
      { keys: ['F1'], label: 'Diese Hilfe anzeigen' },
    ],
  },
]

interface KeyCapProps {
  label: string
}

function KeyCap({ label }: KeyCapProps): React.JSX.Element {
  const isWide = label.length > 2
  return (
    <kbd
      className={`
        inline-flex items-center justify-center
        ${isWide ? 'px-2 min-w-[2.5rem]' : 'w-8'}
        h-7 rounded-md
        font-mono text-[11px] font-semibold tracking-wide
        bg-white dark:bg-zinc-700
        text-zinc-700 dark:text-zinc-200
        border border-zinc-300 dark:border-zinc-600
        shadow-[0_2px_0_0_theme(colors.zinc.300)] dark:shadow-[0_2px_0_0_theme(colors.zinc.600)]
        select-none
      `}
    >
      {label}
    </kbd>
  )
}

interface KeyboardShortcutsDialogProps {
  open: boolean
  onClose: () => void
}

export function KeyboardShortcutsDialog({
  open,
  onClose,
}: KeyboardShortcutsDialogProps): React.JSX.Element | null {
  const dialogRef = useRef<HTMLDivElement>(null)

  // Escape to close
  useEffect(() => {
    if (!open) return
    function handleKeyDown(e: KeyboardEvent): void {
      if (e.key === 'Escape') {
        e.stopPropagation()
        onClose()
      }
    }
    document.addEventListener('keydown', handleKeyDown, true)
    return () => document.removeEventListener('keydown', handleKeyDown, true)
  }, [open, onClose])

  // Trap focus inside dialog when open
  useEffect(() => {
    if (open && dialogRef.current) {
      dialogRef.current.focus()
    }
  }, [open])

  if (!open) return null

  function handleBackdropClick(e: React.MouseEvent<HTMLDivElement>): void {
    if (e.target === e.currentTarget) onClose()
  }

  const content = (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 backdrop-blur-sm"
      onClick={handleBackdropClick}
      aria-modal="true"
      role="dialog"
      aria-label="Tastaturkürzel"
    >
      {/* Animate: scale + opacity via Tailwind transition classes */}
      <div
        ref={dialogRef}
        tabIndex={-1}
        className="
          relative w-full max-w-lg mx-4 outline-none
          bg-white dark:bg-zinc-900
          rounded-2xl border border-zinc-200 dark:border-zinc-700
          shadow-2xl
          animate-[dialogIn_150ms_ease-out_both]
        "
        style={{
          animation: 'dialogIn 150ms ease-out both',
        }}
      >
        {/* Keyframe injected inline once */}
        <style>{`
          @keyframes dialogIn {
            from { opacity: 0; transform: scale(0.95) translateY(4px); }
            to   { opacity: 1; transform: scale(1) translateY(0); }
          }
        `}</style>

        {/* Header */}
        <div className="flex items-center gap-3 px-5 pt-5 pb-4 border-b border-zinc-200 dark:border-zinc-800">
          <span className="flex items-center justify-center w-8 h-8 rounded-lg bg-blue-50 dark:bg-blue-950 text-blue-600 dark:text-blue-400">
            <Keyboard size={16} />
          </span>
          <h2 className="flex-1 text-base font-semibold text-zinc-900 dark:text-zinc-100">
            Tastaturkürzel
          </h2>
          <button
            type="button"
            onClick={onClose}
            className="p-1.5 rounded-md text-zinc-400 hover:text-zinc-700 dark:hover:text-zinc-200 hover:bg-zinc-100 dark:hover:bg-zinc-800 transition-colors"
            title="Schließen"
            aria-label="Schließen"
          >
            <X className="w-4 h-4" />
          </button>
        </div>

        {/* Body */}
        <div className="px-5 py-4 space-y-5">
          {SHORTCUT_GROUPS.map((group) => (
            <section key={group.title}>
              <h3 className="text-[11px] font-semibold uppercase tracking-widest text-zinc-400 dark:text-zinc-500 mb-2.5">
                {group.title}
              </h3>
              <ul className="space-y-1">
                {group.shortcuts.map((shortcut) => (
                  <li
                    key={shortcut.label + shortcut.keys.join('')}
                    className="flex items-center justify-between gap-4 px-3 py-2 rounded-lg hover:bg-zinc-50 dark:hover:bg-zinc-800/60 transition-colors"
                  >
                    <span className="text-sm text-zinc-700 dark:text-zinc-300">
                      {shortcut.label}
                    </span>
                    <span className="flex items-center gap-1 flex-shrink-0">
                      {shortcut.keys.map((key, i) => (
                        <React.Fragment key={key}>
                          {i > 0 && (
                            <span className="text-xs text-zinc-400 dark:text-zinc-500 mx-0.5">
                              oder
                            </span>
                          )}
                          <KeyCap label={key} />
                        </React.Fragment>
                      ))}
                    </span>
                  </li>
                ))}
              </ul>
            </section>
          ))}
        </div>

        {/* Footer */}
        <div className="px-5 py-3 border-t border-zinc-200 dark:border-zinc-800 flex items-center justify-end">
          <button
            type="button"
            onClick={onClose}
            className="px-4 py-1.5 text-sm font-medium rounded-lg text-zinc-600 dark:text-zinc-400 hover:bg-zinc-100 dark:hover:bg-zinc-800 transition-colors"
          >
            Schließen
          </button>
        </div>
      </div>
    </div>
  )

  return createPortal(content, document.body)
}
