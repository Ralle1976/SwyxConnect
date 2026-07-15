import { useState } from 'react'
import { X, ArrowRightLeft, Phone } from 'lucide-react'
import { useCall } from '../../hooks/useCall'
import { useContactStore } from '../../stores/useContactStore'

interface Contact {
  id: string
  name: string
  number: string
  email?: string
  department?: string
  presence?: string
}

interface TransferDialogProps {
  lineId: number
  onClose: () => void
}

export function TransferDialog({ lineId, onClose }: TransferDialogProps) {
  const [query, setQuery] = useState('')
  const { transfer } = useCall()
  const contacts = useContactStore((s) => s.contacts)
  const searchContacts = useContactStore((s) => s.searchContacts)

  function handleTransfer(target: string) {
    transfer(lineId, target)
    onClose()
  }

  function handleQueryChange(value: string) {
    setQuery(value)
    searchContacts(value)
  }

  const hasDigits = /\d/.test(query) && query.length > 0

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/50"
      onClick={onClose}
    >
      <div
        className="relative bg-white dark:bg-zinc-900 rounded-2xl shadow-2xl w-80 max-h-[70vh] flex flex-col overflow-hidden border border-zinc-200 dark:border-zinc-800"
        onClick={(e) => e.stopPropagation()}
      >
        {/* Header */}
        <div className="flex items-center justify-between px-4 py-3 border-b border-zinc-200 dark:border-zinc-800">
          <div className="flex items-center gap-2">
            <ArrowRightLeft size={16} className="text-zinc-500 dark:text-zinc-400" />
            <span className="text-sm font-semibold text-zinc-800 dark:text-zinc-200">
              Weiterleiten
            </span>
          </div>
          <button
            onClick={onClose}
            className="p-1 rounded-lg hover:bg-zinc-100 dark:hover:bg-zinc-800 transition-colors text-zinc-400 hover:text-zinc-600 dark:hover:text-zinc-200"
          >
            <X size={16} />
          </button>
        </div>

        {/* Search */}
        <div className="px-4 py-2">
          <input
            type="text"
            value={query}
            onChange={(e) => handleQueryChange(e.target.value)}
            placeholder="Name oder Nummer…"
            className="w-full px-3 py-2 rounded-lg bg-zinc-100 dark:bg-zinc-800 border border-zinc-200 dark:border-zinc-700 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500/40 text-zinc-800 dark:text-zinc-200 placeholder:text-zinc-400 dark:placeholder:text-zinc-500"
            autoFocus
          />
        </div>

        {/* Contact list */}
        <div className="flex-1 overflow-y-auto">
          {hasDigits && (
            <button
              className="flex items-center gap-3 w-full px-4 py-2.5 hover:bg-zinc-100 dark:hover:bg-zinc-800 transition-colors text-left"
              onClick={() => handleTransfer(query)}
            >
              <div className="w-8 h-8 rounded-full bg-green-500 text-white flex items-center justify-center text-xs font-semibold flex-shrink-0">
                <Phone size={14} />
              </div>
              <div className="flex-1 min-w-0">
                <div className="text-sm font-medium text-zinc-800 dark:text-zinc-200">
                  Direkt wählen: {query}
                </div>
              </div>
            </button>
          )}

          {(contacts as Contact[]).slice(0, 10).map((contact) => {
            const initials = contact.name
              .split(' ')
              .map((p) => p[0])
              .join('')
              .toUpperCase()
              .slice(0, 2)

            return (
              <button
                key={contact.id}
                className="flex items-center gap-3 w-full px-4 py-2.5 hover:bg-zinc-100 dark:hover:bg-zinc-800 transition-colors text-left"
                onClick={() => handleTransfer(contact.number)}
              >
                <div className="w-8 h-8 rounded-full bg-blue-500 text-white flex items-center justify-center text-xs font-semibold flex-shrink-0">
                  {initials}
                </div>
                <div className="flex-1 min-w-0">
                  <div className="text-sm font-medium text-zinc-800 dark:text-zinc-200 truncate">
                    {contact.name}
                  </div>
                  <div className="text-xs text-zinc-500 truncate">{contact.number}</div>
                </div>
                <Phone size={14} className="ml-auto text-zinc-400 flex-shrink-0" />
              </button>
            )
          })}
        </div>

        {/* Footer */}
        <div className="px-4 py-3 border-t border-zinc-200 dark:border-zinc-800 flex justify-end">
          <button
            onClick={onClose}
            className="text-sm text-zinc-500 hover:text-zinc-700 dark:hover:text-zinc-300 transition-colors"
          >
            Abbrechen
          </button>
        </div>
      </div>
    </div>
  )
}
