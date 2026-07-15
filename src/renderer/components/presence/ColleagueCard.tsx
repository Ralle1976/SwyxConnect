import { useState } from 'react';
import { Phone } from 'lucide-react';
import { ColleaguePresence } from '../../types/swyx';
import Avatar from '../common/Avatar';
import StatusBadge from '../common/StatusBadge';

interface Props {
  colleague: ColleaguePresence;
  onDial: (userId: string) => void;
}

export default function ColleagueCard({ colleague, onDial }: Props) {
  const [hovered, setHovered] = useState(false);

  return (
    <div
      className="flex items-center gap-3 px-4 py-3 hover:bg-zinc-50 dark:hover:bg-zinc-800/60 transition-colors cursor-default"
      onMouseEnter={() => setHovered(true)}
      onMouseLeave={() => setHovered(false)}
    >
      <div className="relative shrink-0">
        <Avatar name={colleague.name} size="md" presence={colleague.status} />
      </div>

      <div className="flex-1 min-w-0">
        <div className="flex items-center gap-2">
          <span className="text-sm font-semibold text-zinc-800 dark:text-zinc-100 truncate">
            {colleague.name}
          </span>
          <StatusBadge status={colleague.status} size="sm" />
        </div>
        {colleague.statusText && (
          <p className="text-xs text-zinc-400 dark:text-zinc-500 italic truncate mt-0.5">
            {colleague.statusText}
          </p>
        )}
      </div>

      <button
        onClick={() => onDial(colleague.userId)}
        aria-label={`${colleague.name} anrufen`}
        className={[
          'shrink-0 p-1.5 rounded-full transition-all',
          hovered
            ? 'opacity-100 bg-emerald-100 dark:bg-emerald-900/40 text-emerald-600 dark:text-emerald-400 hover:bg-emerald-200 dark:hover:bg-emerald-800/60'
            : 'opacity-0 pointer-events-none',
        ].join(' ')}
      >
        <Phone size={15} />
      </button>
    </div>
  );
}
