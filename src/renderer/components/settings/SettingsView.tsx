import {
  Sun,
  Moon,
  Monitor,
  Volume2,
  Wifi,
  Info,
  ToggleLeft,
  ToggleRight,
} from 'lucide-react';
import { useSettingsStore, applyTheme } from '../../stores/useSettingsStore';
import { useTeamsSync } from '../../hooks/useTeamsSync';

type Theme = 'light' | 'dark' | 'system';

const THEME_OPTIONS: { label: string; value: Theme; icon: React.ReactNode }[] = [
  { label: 'Hell', value: 'light', icon: <Sun size={14} /> },
  { label: 'Dunkel', value: 'dark', icon: <Moon size={14} /> },
  { label: 'System', value: 'system', icon: <Monitor size={14} /> },
];

function SectionCard({
  title,
  icon,
  children,
}: {
  title: string;
  icon: React.ReactNode;
  children: React.ReactNode;
}) {
  return (
    <div className="rounded-xl border border-zinc-200 dark:border-zinc-700 bg-white dark:bg-zinc-900 p-5">
      <div className="flex items-center gap-2 mb-4">
        <span className="text-zinc-400 dark:text-zinc-500">{icon}</span>
        <h2 className="text-sm font-semibold text-zinc-700 dark:text-zinc-300">{title}</h2>
      </div>
      {children}
    </div>
  );
}

export default function SettingsView() {
  const { theme, sidebarCollapsed, setTheme, toggleSidebar } = useSettingsStore();
  const { syncStatus, enable, disable, login, logout } = useTeamsSync();

  function handleThemeChange(t: Theme) {
    setTheme(t);
    applyTheme(t);
  }

  return (
    <div className="flex flex-col gap-5 p-5 max-w-2xl mx-auto">
      <h1 className="text-lg font-bold text-zinc-800 dark:text-zinc-100">Einstellungen</h1>

      {/* Darstellung */}
      <SectionCard title="Darstellung" icon={<Monitor size={16} />}>
        <div className="flex flex-col gap-4">
          <div>
            <p className="text-xs text-zinc-500 dark:text-zinc-400 mb-2">Farbschema</p>
            <div className="flex gap-2">
              {THEME_OPTIONS.map(({ label, value, icon }) => (
                <button
                  key={value}
                  onClick={() => handleThemeChange(value)}
                  className={[
                    'flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-xs font-medium transition-all',
                    theme === value
                      ? 'ring-2 ring-blue-500 ring-offset-1 bg-blue-50 dark:bg-blue-950/40 text-blue-700 dark:text-blue-300'
                      : 'bg-zinc-100 dark:bg-zinc-800 text-zinc-600 dark:text-zinc-400 hover:bg-zinc-200 dark:hover:bg-zinc-700',
                  ].join(' ')}
                >
                  {icon}
                  {label}
                </button>
              ))}
            </div>
          </div>

          <div className="flex items-center justify-between">
            <span className="text-xs text-zinc-600 dark:text-zinc-400">Seitenleiste einklappen</span>
            <button
              onClick={toggleSidebar}
              className="text-zinc-400 dark:text-zinc-500 hover:text-blue-500 transition-colors"
              aria-label="Seitenleiste umschalten"
            >
              {sidebarCollapsed ? <ToggleRight size={22} className="text-blue-500" /> : <ToggleLeft size={22} />}
            </button>
          </div>
        </div>
      </SectionCard>

      {/* Audio */}
      <SectionCard title="Audiogeräte" icon={<Volume2 size={16} />}>
        <div className="flex flex-col gap-3">
          {(['Eingabegerät', 'Ausgabegerät'] as const).map((label) => (
            <div key={label} className="flex flex-col gap-1">
              <label className="text-xs text-zinc-500 dark:text-zinc-400">{label}</label>
              <select
                disabled
                className="w-full px-3 py-2 rounded-lg text-xs bg-zinc-50 dark:bg-zinc-800 border border-zinc-200 dark:border-zinc-700 text-zinc-400 dark:text-zinc-500 cursor-not-allowed"
              >
                <option>Wird über SwyxIt! konfiguriert</option>
              </select>
            </div>
          ))}
        </div>
      </SectionCard>

      {/* Microsoft Teams */}
      <SectionCard title="Microsoft Teams" icon={<Wifi size={16} />}>
        <div className="flex flex-col gap-3">
          <div className="flex items-center justify-between">
            <span className="text-xs text-zinc-600 dark:text-zinc-400">
              Teams-Status Synchronisation aktivieren
            </span>
            <button
              onClick={syncStatus.enabled ? disable : enable}
              className="text-zinc-400 dark:text-zinc-500 hover:text-blue-500 transition-colors"
              aria-label="Teams-Synchronisation umschalten"
            >
              {syncStatus.enabled ? (
                <ToggleRight size={22} className="text-blue-500" />
              ) : (
                <ToggleLeft size={22} />
              )}
            </button>
          </div>

          {syncStatus.enabled && (
            <>
              <div className="flex items-center gap-3">
                {syncStatus.connected ? (
                  <>
                    <span className="text-xs font-medium text-emerald-600 dark:text-emerald-400">
                      Verbunden
                    </span>
                    <button
                      onClick={logout}
                      className="text-xs px-3 py-1.5 rounded-lg bg-zinc-100 dark:bg-zinc-800 text-zinc-600 dark:text-zinc-400 hover:bg-zinc-200 dark:hover:bg-zinc-700 transition-colors"
                    >
                      Abmelden
                    </button>
                  </>
                ) : syncStatus.error ? (
                  <>
                    <span className="text-xs font-medium text-red-600 dark:text-red-400 flex-1 truncate">
                      {syncStatus.error}
                    </span>
                    <button
                      onClick={login}
                      className="text-xs px-3 py-1.5 rounded-lg bg-blue-500 text-white hover:bg-blue-600 transition-colors"
                    >
                      Anmelden
                    </button>
                  </>
                ) : (
                  <span className="text-xs text-zinc-400 dark:text-zinc-500">Verbindung wird hergestellt…</span>
                )}
              </div>
              {!syncStatus.connected && !syncStatus.error && (
                <button
                  onClick={login}
                  className="self-start text-xs px-3 py-1.5 rounded-lg bg-blue-500 text-white hover:bg-blue-600 transition-colors"
                >
                  Mit Microsoft anmelden
                </button>
              )}
            </>
          )}
        </div>
      </SectionCard>

      {/* Plugins */}
      <SectionCard title="Plugins" icon={<Info size={16} />}>
        <p className="text-xs text-zinc-500 dark:text-zinc-400 leading-relaxed">
          Noch keine Plugins geladen. Plugin-System wird in einem zukünftigen Update verfügbar.
        </p>
      </SectionCard>

      {/* Über */}
      <SectionCard title="Über" icon={<Info size={16} />}>
        <div className="flex flex-col gap-1">
          <p className="text-sm font-semibold text-zinc-800 dark:text-zinc-100">SwyIt by Ralle197</p>
          <p className="text-xs text-zinc-500 dark:text-zinc-400">Version 0.1.0</p>
          <p className="text-xs text-zinc-400 dark:text-zinc-500 mt-1 leading-relaxed">
            Moderner Softphone-Client für Swyx/Enreach
          </p>
        </div>
      </SectionCard>
    </div>
  );
}
