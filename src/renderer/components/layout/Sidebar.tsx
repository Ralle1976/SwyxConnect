import { NavLink } from 'react-router-dom'
import {
  Phone,
  BookUser,
  Clock,
  MessageSquare,
  Users,
  Headphones,
  Settings,
  ChevronLeft,
  ChevronRight,
} from 'lucide-react'
import { useSettingsStore } from '../../stores/useSettingsStore'
import { useHistoryStore, getMissedCount } from '../../stores/useHistoryStore'
import { useVoicemailStore, getNewVoicemailCount } from '../../stores/useVoicemailStore'
import { usePresenceStore } from '../../stores/usePresenceStore'
import { StatusBadge } from '../common/StatusBadge'

interface NavItem {
  to: string
  label: string
  Icon: React.ComponentType<{ size?: number; className?: string }>
  badge?: number
}

export function Sidebar() {
  const sidebarCollapsed = useSettingsStore((s) => s.sidebarCollapsed)
  const setSidebarCollapsed = useSettingsStore((s) => s.setSidebarCollapsed)
  const entries = useHistoryStore((s) => s.entries)
  const messages = useVoicemailStore((s) => s.messages)
  const ownStatus = usePresenceStore((s) => s.ownStatus)

  const missedCount = getMissedCount(entries)
  const voicemailCount = getNewVoicemailCount(messages)

  const navItems: NavItem[] = [
    { to: '/', label: 'Telefon', Icon: Phone },
    { to: '/contacts', label: 'Kontakte', Icon: BookUser },
    { to: '/history', label: 'Verlauf', Icon: Clock, badge: missedCount },
    { to: '/voicemail', label: 'Voicemail', Icon: MessageSquare, badge: voicemailCount },
    { to: '/presence', label: 'Präsenz', Icon: Users },
    { to: '/callcenter', label: 'Callcenter', Icon: Headphones },
    { to: '/settings', label: 'Einstellungen', Icon: Settings },
  ]

  const collapsed = sidebarCollapsed

  return (
    <aside
      className={`
        flex flex-col flex-shrink-0 h-full
        bg-white dark:bg-zinc-900
        border-r border-zinc-200 dark:border-zinc-800
        transition-all duration-200 ease-in-out
        ${collapsed ? 'w-16' : 'w-56'}
      `}
    >
      {/* Navigation items */}
      <nav className="flex-1 py-2 overflow-y-auto overflow-x-hidden">
        {navItems.map(({ to, label, Icon, badge }) => (
          <NavLink
            key={to}
            to={to}
            end={to === '/'}
            className={({ isActive }) =>
              `relative flex items-center gap-3 mx-2 px-3 py-2.5 rounded-lg text-sm font-medium transition-colors
               ${isActive
                 ? 'bg-blue-50 dark:bg-blue-950 text-blue-600 dark:text-blue-400'
                 : 'text-zinc-600 dark:text-zinc-400 hover:bg-zinc-100 dark:hover:bg-zinc-800 hover:text-zinc-900 dark:hover:text-zinc-100'
               }`
            }
            title={collapsed ? label : undefined}
          >
            <span className="relative flex-shrink-0">
              <Icon size={18} />
              {badge !== undefined && badge > 0 && (
                <span className="absolute -top-1.5 -right-1.5 min-w-[16px] h-4 px-0.5 flex items-center justify-center rounded-full bg-red-500 text-white text-[10px] font-bold leading-none">
                  {badge > 99 ? '99+' : badge}
                </span>
              )}
            </span>
            {!collapsed && (
              <span className="truncate transition-opacity duration-150">{label}</span>
            )}
          </NavLink>
        ))}
      </nav>

      {/* Bottom: own presence + collapse toggle */}
      <div className="border-t border-zinc-200 dark:border-zinc-800 py-2">
        {/* Own presence */}
        <div
          className={`flex items-center gap-2 mx-2 px-3 py-2 rounded-lg ${
            collapsed ? 'justify-center' : ''
          }`}
          title={collapsed ? (ownStatus ?? 'Offline') : undefined}
        >
          <StatusBadge status={ownStatus ?? 'Offline'} size="md" />
          {!collapsed && (
            <span className="text-xs text-zinc-500 dark:text-zinc-400 truncate">
              {ownStatus ?? 'Offline'}
            </span>
          )}
        </div>

        {/* Collapse toggle */}
        <button
          onClick={() => setSidebarCollapsed(!collapsed)}
          title={collapsed ? 'Sidebar öffnen' : 'Sidebar schließen'}
          className="flex items-center justify-center w-full h-8 text-zinc-400 dark:text-zinc-500 hover:text-zinc-600 dark:hover:text-zinc-300 transition-colors"
        >
          {collapsed ? <ChevronRight size={14} /> : <ChevronLeft size={14} />}
        </button>
      </div>
    </aside>
  )
}
