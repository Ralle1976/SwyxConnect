import { Wifi, WifiOff, RefreshCw } from 'lucide-react';
import { useTeamsSync } from '../../hooks/useTeamsSync';

function relativeMinutes(ts: number): string {
  const diff = Math.floor((Date.now() - ts) / 60000);
  if (diff < 1) return 'gerade eben';
  if (diff === 1) return 'vor 1 Min.';
  return `vor ${diff} Min.`;
}

export default function TeamsStatusBanner() {
  const { syncStatus } = useTeamsSync();

  if (!syncStatus.enabled) return null;

  if (syncStatus.connected) {
    return (
      <div className="flex items-center gap-2 h-8 px-3 rounded-lg bg-emerald-50 dark:bg-emerald-950/30 text-emerald-700 dark:text-emerald-400 text-xs font-medium">
        <Wifi size={13} className="shrink-0" />
        <span>Microsoft Teams verbunden</span>
        {syncStatus.lastSync !== null && (
          <span className="text-emerald-500 dark:text-emerald-600 font-normal">
            · Letzte Sync: {relativeMinutes(syncStatus.lastSync)}
          </span>
        )}
      </div>
    );
  }

  if (syncStatus.error) {
    return (
      <div className="flex items-center gap-2 h-8 px-3 rounded-lg bg-red-50 dark:bg-red-950/30 text-red-700 dark:text-red-400 text-xs font-medium">
        <WifiOff size={13} className="shrink-0" />
        <span className="truncate">{syncStatus.error}</span>
      </div>
    );
  }

  // Enabled but not yet connected
  return (
    <div className="flex items-center gap-2 h-8 px-3 rounded-lg bg-zinc-100 dark:bg-zinc-800/60 text-zinc-500 dark:text-zinc-400 text-xs">
      <RefreshCw size={13} className="shrink-0 animate-spin" />
      <span>Teams wird verbunden…</span>
    </div>
  );
}
