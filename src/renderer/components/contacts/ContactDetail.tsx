import React from 'react';
import { Phone, Mail, X, MessageSquare, Building2 } from 'lucide-react';
import { Contact, PresenceStatus } from '../../types/swyx';
import Avatar from '../common/Avatar';
import StatusBadge from '../common/StatusBadge';

interface ContactDetailProps {
  contact: Contact;
  onDial: (number: string) => void;
  onClose: () => void;
}

const PRESENCE_LABEL: Record<PresenceStatus, string> = {
  [PresenceStatus.Available]: 'Verfügbar',
  [PresenceStatus.Away]: 'Abwesend',
  [PresenceStatus.Busy]: 'Beschäftigt',
  [PresenceStatus.DND]: 'Nicht stören',
  [PresenceStatus.Offline]: 'Offline',
};

export default function ContactDetail({
  contact,
  onDial,
  onClose,
}: ContactDetailProps): React.JSX.Element {
  return (
    <div className="flex flex-col h-full bg-white dark:bg-zinc-900">
      {/* Header */}
      <div className="flex items-center justify-between px-5 pt-4 pb-3 border-b border-zinc-200 dark:border-zinc-800">
        <span className="text-sm font-semibold text-zinc-700 dark:text-zinc-300">Kontaktdetails</span>
        <button
          type="button"
          onClick={onClose}
          className="p-1.5 rounded-md text-zinc-400 hover:text-zinc-700 dark:hover:text-zinc-200 hover:bg-zinc-100 dark:hover:bg-zinc-800 transition-colors"
          title="Schließen"
        >
          <X className="w-4 h-4" />
        </button>
      </div>

      {/* Profile section */}
      <div className="flex flex-col items-center gap-3 px-6 py-6 border-b border-zinc-200 dark:border-zinc-800">
        <div className="relative">
          <Avatar name={contact.name} size="xl" />
          {contact.presence !== undefined && (
            <span className="absolute -bottom-1 -right-1">
              <StatusBadge status={contact.presence} size="md" />
            </span>
          )}
        </div>
        <div className="text-center">
          <h2 className="text-base font-semibold text-zinc-900 dark:text-zinc-100">
            {contact.name}
          </h2>
          {contact.presence !== undefined && (
            <p className="text-xs text-zinc-500 dark:text-zinc-400 mt-0.5">
              {PRESENCE_LABEL[contact.presence]}
            </p>
          )}
        </div>

        {/* Action buttons */}
        <div className="flex gap-2 mt-1">
          <button
            type="button"
            onClick={() => onDial(contact.number)}
            className="flex items-center gap-2 px-4 py-2 rounded-lg bg-green-500 hover:bg-green-600 text-white text-sm font-medium shadow-sm transition-colors"
          >
            <Phone className="w-4 h-4" />
            Anrufen
          </button>
          <button
            type="button"
            disabled
            title="Funktion noch nicht verfügbar"
            className="flex items-center gap-2 px-4 py-2 rounded-lg bg-zinc-100 dark:bg-zinc-800 text-zinc-400 dark:text-zinc-500 text-sm font-medium cursor-not-allowed"
          >
            <MessageSquare className="w-4 h-4" />
            Nachricht
          </button>
        </div>
      </div>

      {/* Detail rows */}
      <div className="flex-1 overflow-y-auto px-5 py-4 space-y-3">
        {/* Primary number */}
        <DetailRow
          label="Telefon"
          icon={<Phone className="w-4 h-4 text-zinc-400 dark:text-zinc-500" />}
        >
          <button
            type="button"
            onClick={() => onDial(contact.number)}
            className="text-sm text-blue-600 dark:text-blue-400 hover:underline font-mono"
          >
            {contact.number}
          </button>
        </DetailRow>

        {/* Email */}
        {contact.email && (
          <DetailRow
            label="E-Mail"
            icon={<Mail className="w-4 h-4 text-zinc-400 dark:text-zinc-500" />}
          >
            <a
              href={`mailto:${contact.email}`}
              className="text-sm text-blue-600 dark:text-blue-400 hover:underline truncate"
            >
              {contact.email}
            </a>
          </DetailRow>
        )}

        {/* Department */}
        {contact.department && (
          <DetailRow
            label="Abteilung"
            icon={<Building2 className="w-4 h-4 text-zinc-400 dark:text-zinc-500" />}
          >
            <span className="text-sm text-zinc-700 dark:text-zinc-300">{contact.department}</span>
          </DetailRow>
        )}

        {/* Presence */}
        {contact.presence !== undefined && (
          <DetailRow
            label="Status"
            icon={<StatusBadge status={contact.presence} size="sm" />}
          >
            <span className="text-sm text-zinc-700 dark:text-zinc-300">
              {PRESENCE_LABEL[contact.presence]}
            </span>
          </DetailRow>
        )}
      </div>
    </div>
  );
}

/* ---- helper ---- */

interface DetailRowProps {
  label: string;
  icon: React.ReactNode;
  children: React.ReactNode;
}

function DetailRow({ label, icon, children }: DetailRowProps): React.JSX.Element {
  return (
    <div className="flex items-start gap-3 py-2 border-b border-zinc-100 dark:border-zinc-800 last:border-0">
      <span className="flex-none mt-0.5">{icon}</span>
      <div className="flex-1 min-w-0">
        <p className="text-xs text-zinc-400 dark:text-zinc-500 mb-0.5">{label}</p>
        <div className="truncate">{children}</div>
      </div>
    </div>
  );
}
