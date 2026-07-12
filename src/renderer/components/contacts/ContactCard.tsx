import React, { useState } from 'react';
import { Phone, Mail } from 'lucide-react';
import { Contact } from '../../types/swyx';
import Avatar from '../common/Avatar';
import StatusBadge from '../common/StatusBadge';

interface ContactCardProps {
  contact: Contact;
  onDial: (number: string) => void;
  onClick?: () => void;
  isSelected?: boolean;
}

export default function ContactCard({
  contact,
  onDial,
  onClick,
  isSelected = false,
}: ContactCardProps): React.JSX.Element {
  const [hovered, setHovered] = useState(false);

  function handleDialClick(e: React.MouseEvent<HTMLButtonElement>): void {
    e.stopPropagation();
    onDial(contact.number);
  }

  return (
    <div
      role="button"
      tabIndex={0}
      onClick={onClick}
      onKeyDown={(e) => e.key === 'Enter' && onClick?.()}
      onMouseEnter={() => setHovered(true)}
      onMouseLeave={() => setHovered(false)}
      className={`flex items-center gap-3 px-4 py-3 cursor-pointer transition-colors duration-100 outline-none
        focus-visible:ring-2 focus-visible:ring-blue-500
        ${
          isSelected
            ? 'bg-blue-50 dark:bg-blue-950/30'
            : 'hover:bg-zinc-50 dark:hover:bg-zinc-800/60'
        }`}
    >
      {/* Avatar with presence badge */}
      <div className="relative flex-none">
        <Avatar name={contact.name} size="md" />
        {contact.presence !== undefined && (
          <span className="absolute -bottom-0.5 -right-0.5">
            <StatusBadge status={contact.presence} size="sm" />
          </span>
        )}
      </div>

      {/* Info */}
      <div className="flex-1 min-w-0">
        <div className="flex items-center gap-2">
          <span className="font-medium text-sm text-zinc-900 dark:text-zinc-100 truncate">
            {contact.name}
          </span>
          {contact.department && (
            <span className="hidden sm:inline-flex flex-none text-xs px-1.5 py-0.5 rounded bg-zinc-100 dark:bg-zinc-700 text-zinc-500 dark:text-zinc-400 font-normal">
              {contact.department}
            </span>
          )}
        </div>
        <div className="text-xs text-zinc-500 dark:text-zinc-400 truncate mt-0.5">
          {contact.number}
        </div>
        {contact.email && (
          <div className="flex items-center gap-1 text-xs text-zinc-400 dark:text-zinc-500 mt-0.5 truncate">
            <Mail className="w-3 h-3 flex-none" />
            <span className="truncate">{contact.email}</span>
          </div>
        )}
      </div>

      {/* Quick-dial button (visible on hover or always on mobile) */}
      <div
        className={`flex-none transition-opacity duration-100 ${
          hovered ? 'opacity-100' : 'opacity-0 pointer-events-none'
        }`}
      >
        <button
          type="button"
          onClick={handleDialClick}
          title={`Anrufen: ${contact.number}`}
          className="flex items-center justify-center w-8 h-8 rounded-full bg-green-500 hover:bg-green-600 text-white shadow-sm transition-colors"
        >
          <Phone className="w-4 h-4" />
        </button>
      </div>
    </div>
  );
}
