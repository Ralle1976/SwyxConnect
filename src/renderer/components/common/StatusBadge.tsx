import React from 'react';

interface StatusBadgeProps {
  status: string;
  size?: 'sm' | 'md' | 'lg';
  showLabel?: boolean;
}

const sizeMap = {
  sm: 'w-2 h-2',
  md: 'w-3 h-3',
  lg: 'w-4 h-4',
};

function getStatusColor(status: string): string {
  switch (status.toLowerCase()) {
    case 'available':
      return 'bg-emerald-500';
    case 'away':
      return 'bg-amber-400';
    case 'busy':
      return 'bg-red-500';
    case 'dnd':
      return 'bg-red-700';
    case 'offline':
      return 'bg-zinc-400';
    default:
      return 'bg-zinc-400';
  }
}

export function StatusBadge({ status, size = 'md', showLabel = false }: StatusBadgeProps) {
  const dotSize = sizeMap[size];
  const dotColor = getStatusColor(status);

  return (
    <span className="flex items-center gap-1.5">
      <span
        className={`${dotSize} ${dotColor} rounded-full inline-block flex-shrink-0`}
      />
      {showLabel && (
        <span className="text-sm text-zinc-600 dark:text-zinc-400">{status}</span>
      )}
    </span>
  );
}

export default StatusBadge;
