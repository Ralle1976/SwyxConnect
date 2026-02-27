import { Headphones, Users, Settings } from 'lucide-react';
import EmptyState from '../common/EmptyState';

const CARDS: {
  title: string;
  description: string;
  icon: React.ReactNode;
}[] = [
    { title: 'Warteschlangen', icon: <Headphones size={32} />, description: 'Warteschlangen-Übersicht wird in einem zukünftigen Update verfügbar.' },
    { title: 'Agenten-Übersicht', icon: <Users size={32} />, description: 'Agenten-Status und -Verwaltung wird in einem zukünftigen Update verfügbar.' },
    { title: 'Supervisor-Panel', icon: <Settings size={32} />, description: 'Supervisor-Funktionen werden in einem zukünftigen Update verfügbar.' },
  ];

export default function CallcenterDashboard() {
  return (
    <div className="flex flex-col gap-6 p-5">
      <h1 className="text-lg font-bold text-zinc-800 dark:text-zinc-100">
        Callcenter Dashboard
      </h1>

      <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
        {CARDS.map(({ title, icon: Icon, description }) => (
          <div
            key={title}
            className="rounded-xl border border-zinc-200 dark:border-zinc-700 bg-zinc-50 dark:bg-zinc-900/60 p-5 flex flex-col items-center text-center"
          >
            <EmptyState
              icon={Icon}
              title={title}
              description={description}
            />
          </div>
        ))}
      </div>
    </div>
  );
}
