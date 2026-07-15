import React from 'react';

interface EmptyStateProps {
  icon: React.ReactNode;
  title: string;
  description?: string;
}

export function EmptyState({ icon, title, description }: EmptyStateProps) {
  return (
    <div className="flex flex-col items-center justify-center py-16 px-4 text-center">
      <div className="mb-4 text-zinc-300 dark:text-zinc-600">
        {icon}
      </div>
      <p className="text-sm font-medium text-zinc-500 dark:text-zinc-400">
        {title}
      </p>
      {description && (
        <p className="text-xs text-zinc-400 dark:text-zinc-500 mt-1 max-w-xs">
          {description}
        </p>
      )}
    </div>
  );
}

export default EmptyState;
