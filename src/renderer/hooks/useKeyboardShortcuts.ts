import { useEffect } from 'react'
import { useLineStore, getRingingLines, getActiveLines, getHeldLines } from '../stores/useLineStore'
import { useCall } from './useCall'

/**
 * Globale Tastaturkürzel für Anrufsteuerung.
 *
 *   F5  → Anruf annehmen (klingelnde Leitung)
 *   F6  → Auflegen (aktive Leitung)
 *   F7  → Halten / Wiederaufnehmen (aktive oder gehaltene Leitung)
 *   F8  → Stummschalten (aktive Leitung)
 *   Esc → Aktiven Anruf beenden
 *   F1  → Tastaturkürzel-Hilfe anzeigen
 *   ?   → Tastaturkürzel-Hilfe anzeigen
 */
export function useKeyboardShortcuts(onShowHelp?: () => void): void {
  const lines = useLineStore((s) => s.lines)
  const { answer, hangup, hold, unhold, mute, unmute } = useCall()

  useEffect(() => {
    const handler = (e: KeyboardEvent): void => {
      // Nicht reagieren wenn Eingabefeld fokussiert ist
      const tag = (e.target as HTMLElement)?.tagName?.toLowerCase()
      if (tag === 'input' || tag === 'textarea' || tag === 'select') return

      switch (e.key) {
        case 'F5': {
          e.preventDefault()
          const ringing = getRingingLines(lines)
          if (ringing.length > 0) {
            answer(ringing[0].id)
          }
          break
        }

        case 'F6': {
          e.preventDefault()
          const active = getActiveLines(lines)
          if (active.length > 0) {
            hangup(active[0].id)
          } else {
            // Fallback: klingelnde Leitung auflegen
            const ringing = getRingingLines(lines)
            if (ringing.length > 0) hangup(ringing[0].id)
          }
          break
        }

        case 'F7': {
          e.preventDefault()
          const active = getActiveLines(lines)
          const held = getHeldLines(lines)
          if (active.length > 0) {
            hold(active[0].id)
          } else if (held.length > 0) {
            unhold(held[0].id)
          }
          break
        }

        case 'F8': {
          e.preventDefault()
          const active = getActiveLines(lines)
          if (active.length > 0) {
            // Toggle mute — kein State-Tracking nötig, COM handled es
            mute(active[0].id)
          }
          break
        }

        case 'Escape': {
          const active = getActiveLines(lines)
          if (active.length > 0) {
            hangup(active[0].id)
          }
          break
        }

        case 'F1': {
          e.preventDefault()
          onShowHelp?.()
          break
        }

        case '?': {
          onShowHelp?.()
          break
        }
      }
    }

    window.addEventListener('keydown', handler)
    return () => window.removeEventListener('keydown', handler)
  }, [lines, answer, hangup, hold, unhold, mute, unmute, onShowHelp])
}
