import React from 'react';
import type { PresenceStatus } from '../../types/swyx';

interface AvatarProps {
  name: string;
  size?: 'sm' | 'md' | 'lg' | 'xl';
  className?: string;
  presence?: PresenceStatus;
}

const sizeMap = {
  sm: 'w-8 h-8 text-xs',
  md: 'w-10 h-10 text-sm',
  lg: 'w-14 h-14 text-lg',
  xl: 'w-20 h-20 text-2xl',
};

const colorPalette = [
  'bg-blue-500',
  'bg-emerald-500',
  'bg-purple-500',
  'bg-amber-500',
  'bg-rose-500',
  'bg-cyan-500',
  'bg-indigo-500',
  'bg-teal-500',
];

function getInitials(name: string): string {
  const parts = name.trim().split(/\s+/);
  if (parts.length === 1) {
    return parts[0].slice(0, 2).toUpperCase();
  }
  const first = parts[0][0] ?? '';
  const last = parts[parts.length - 1][0] ?? '';
  return (first + last).toUpperCase();
}

function getColorIndex(name: string): number {
  let sum = 0;
  for (let i = 0; i < name.length; i++) {
    sum += name.charCodeAt(i);
  }
  return sum % colorPalette.length;
}

const presenceDot: Record<string, string> = {
  Available: 'bg-emerald-500',
  Away: 'bg-amber-400',
  Busy: 'bg-red-500',
  DND: 'bg-red-700',
  Offline: 'bg-zinc-400',
};

export function Avatar({ name, size = 'md', className = '', presence }: AvatarProps) {
  const initials = getInitials(name);
  const colorClass = colorPalette[getColorIndex(name)];
  const sizeClass = sizeMap[size];

  return (
    <div className="relative inline-flex flex-shrink-0">
      <div
        className={`${sizeClass} ${colorClass} rounded-full flex items-center justify-center font-semibold text-white select-none ${className}`}
        title={name}
      >
        {initials}
      </div>
      {presence && (
        <span
          className={`absolute bottom-0 right-0 w-2.5 h-2.5 rounded-full border-2 border-white dark:border-zinc-900 ${presenceDot[presence] ?? 'bg-zinc-400'}`}
        />
      )}
    </div>
  );
}

export default Avatar;
