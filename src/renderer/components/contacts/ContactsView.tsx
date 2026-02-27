import React, { useState, useEffect } from 'react';
import { Users } from 'lucide-react';
import { useContactStore } from '../../stores/useContactStore';
import { useCall } from '../../hooks/useCall';
import { Contact } from '../../types/swyx';
import SearchInput from '../common/SearchInput';
import EmptyState from '../common/EmptyState';
import ContactCard from './ContactCard';
import ContactDetail from './ContactDetail';

export default function ContactsView(): React.JSX.Element {
  const { contacts, searchQuery, loading, searchContacts, setQuery } = useContactStore();
  const { dial } = useCall();
  const [selectedContact, setSelectedContact] = useState<Contact | null>(null);

  useEffect(() => {
    searchContacts(searchQuery);
  }, [searchQuery, searchContacts]);

  function handleSearchChange(value: string): void {
    setQuery(value);
  }

  function handleDial(number: string): void {
    dial(number);
  }

  function handleContactClick(contact: Contact): void {
    setSelectedContact(contact);
  }

  function handleDetailClose(): void {
    setSelectedContact(null);
  }

  function handleDetailDial(number: string): void {
    dial(number);
    setSelectedContact(null);
  }

  return (
    <div className="flex h-full w-full bg-white dark:bg-zinc-900 overflow-hidden">
      {/* Main list panel */}
      <div
        className={`flex flex-col h-full transition-all duration-200 ${
          selectedContact ? 'w-1/2 border-r border-zinc-200 dark:border-zinc-800' : 'w-full'
        }`}
      >
        {/* Header */}
        <div className="flex-none px-6 pt-5 pb-3 border-b border-zinc-200 dark:border-zinc-800">
          <h1 className="text-lg font-semibold text-zinc-900 dark:text-zinc-100 tracking-tight">
            Kontakte
          </h1>
          <p className="text-xs text-zinc-500 dark:text-zinc-400 mt-0.5">
            {contacts.length} Einträge
          </p>
        </div>

        {/* Search */}
        <div className="flex-none px-4 py-3 border-b border-zinc-200 dark:border-zinc-800">
          <SearchInput
            value={searchQuery}
            onChange={handleSearchChange}
            placeholder="Kontakt suchen…"
          />
        </div>

        {/* List */}
        <div className="flex-1 overflow-y-auto">
          {loading && (
            <div className="flex items-center justify-center h-24 text-sm text-zinc-400 dark:text-zinc-500">
              Lade Kontakte…
            </div>
          )}

          {!loading && contacts.length === 0 && (
            <EmptyState
              icon={<Users className="w-8 h-8 text-zinc-400 dark:text-zinc-600" />}
              title="Keine Kontakte gefunden"
              description={
                searchQuery
                  ? `Keine Ergebnisse für „${searchQuery}"`
                  : 'Es sind noch keine Kontakte vorhanden.'
              }
            />
          )}

          {!loading && contacts.length > 0 && (
            <ul className="divide-y divide-zinc-100 dark:divide-zinc-800">
              {contacts.map((contact) => (
                <li key={contact.id}>
                  <ContactCard
                    contact={contact}
                    onDial={handleDial}
                    onClick={() => handleContactClick(contact)}
                    isSelected={selectedContact?.id === contact.id}
                  />
                </li>
              ))}
            </ul>
          )}
        </div>
      </div>

      {/* Detail panel */}
      {selectedContact && (
        <div className="flex-1 h-full overflow-y-auto">
          <ContactDetail
            contact={selectedContact}
            onDial={handleDetailDial}
            onClose={handleDetailClose}
          />
        </div>
      )}
    </div>
  );
}
