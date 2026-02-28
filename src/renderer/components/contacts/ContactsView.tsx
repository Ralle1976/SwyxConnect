import React, { useState, useEffect, useCallback } from 'react';
import { Users, Star, Plus, Pencil, Trash2, Phone, Building2, Mail } from 'lucide-react';
import { useContactStore } from '../../stores/useContactStore';
import { useLocalContactStore, LocalContact } from '../../stores/useLocalContactStore';
import { useCall } from '../../hooks/useCall';
import { Contact } from '../../types/swyx';
import SearchInput from '../common/SearchInput';
import EmptyState from '../common/EmptyState';
import ContactCard from './ContactCard';
import ContactDetail from './ContactDetail';
import AddContactDialog from './AddContactDialog';
import Avatar from '../common/Avatar';

type TabKey = 'intern' | 'eigene' | 'favoriten';

const TABS: { key: TabKey; label: string }[] = [
  { key: 'intern', label: 'Intern' },
  { key: 'eigene', label: 'Eigene' },
  { key: 'favoriten', label: 'Favoriten' },
];

export default function ContactsView(): React.JSX.Element {
  const { contacts, searchQuery, loading, searchContacts, setQuery } = useContactStore();
  const localContacts = useLocalContactStore((s) => s.contacts);
  const toggleFavorite = useLocalContactStore((s) => s.toggleFavorite);
  const deleteContact = useLocalContactStore((s) => s.deleteContact);
  const { dial } = useCall();

  const [activeTab, setActiveTab] = useState<TabKey>('intern');
  const [selectedContact, setSelectedContact] = useState<Contact | null>(null);
  const [showAddDialog, setShowAddDialog] = useState(false);
  const [editingContact, setEditingContact] = useState<LocalContact | undefined>(undefined);

  // Kontakte laden: bei Suchänderung UND initial beim Mounten
  useEffect(() => {
    searchContacts(searchQuery);
  }, [searchQuery, searchContacts]);

  // Retry-Mechanismus: Wenn beim ersten Laden keine Kontakte kamen (Bridge noch nicht bereit),
  // nach kurzer Verzögerung erneut versuchen
  useEffect(() => {
    if (contacts.length === 0 && !loading) {
      const retryTimer = setTimeout(() => {
        searchContacts(searchQuery);
      }, 3000);
      return () => clearTimeout(retryTimer);
    }
  }, [contacts.length, loading, searchContacts, searchQuery]);

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

  function handleOpenAdd(): void {
    setEditingContact(undefined);
    setShowAddDialog(true);
  }

  function handleEditLocalContact(contact: LocalContact): void {
    setEditingContact(contact);
    setShowAddDialog(true);
  }

  function handleCloseDialog(): void {
    setShowAddDialog(false);
    setEditingContact(undefined);
  }

  function handleDeleteLocalContact(id: string): void {
    deleteContact(id);
  }

  // Lokale Kontakte gefiltert nach Suchbegriff
  const filteredLocalContacts = searchQuery
    ? localContacts.filter(
        (c) =>
          c.name.toLowerCase().includes(searchQuery.toLowerCase()) ||
          c.numbers.some((n) => n.includes(searchQuery)) ||
          c.company?.toLowerCase().includes(searchQuery.toLowerCase())
      )
    : localContacts;

  // Favoriten: alle lokalen Kontakte mit isFavorite === true
  const favoriteContacts = localContacts.filter((c) => c.isFavorite);

  return (
    <div className="flex h-full w-full bg-white dark:bg-zinc-900 overflow-hidden">
      {/* Main list panel */}
      <div
        className={`flex flex-col h-full transition-all duration-200 ${
          selectedContact ? 'w-1/2 border-r border-zinc-200 dark:border-zinc-800' : 'w-full'
        }`}
      >
        {/* Header */}
        <div className="flex-none flex items-center justify-between px-6 pt-5 pb-3 border-b border-zinc-200 dark:border-zinc-800">
          <div>
            <h1 className="text-lg font-semibold text-zinc-900 dark:text-zinc-100 tracking-tight">
              Kontakte
            </h1>
            <p className="text-xs text-zinc-500 dark:text-zinc-400 mt-0.5">
              {activeTab === 'intern' && `${contacts.length} interne Einträge`}
              {activeTab === 'eigene' && `${localContacts.length} eigene Einträge`}
              {activeTab === 'favoriten' && `${favoriteContacts.length} Favoriten`}
            </p>
          </div>
          <button
            type="button"
            onClick={handleOpenAdd}
            title="Neuen Kontakt erstellen"
            className="flex items-center gap-1.5 px-3 py-1.5 rounded-lg bg-blue-500 hover:bg-blue-600 text-white text-xs font-medium transition-colors"
          >
            <Plus className="w-4 h-4" />
            Neu
          </button>
        </div>

        {/* Tabs */}
        <div className="flex-none flex gap-1 px-4 py-2 border-b border-zinc-200 dark:border-zinc-800 bg-zinc-50 dark:bg-zinc-900/50">
          {TABS.map((tab) => (
            <button
              key={tab.key}
              type="button"
              onClick={() => setActiveTab(tab.key)}
              className={`px-3 py-1.5 rounded-md text-xs font-medium transition-colors ${
                activeTab === tab.key
                  ? 'bg-white dark:bg-zinc-700 text-zinc-900 dark:text-zinc-100 shadow-sm'
                  : 'text-zinc-500 dark:text-zinc-400 hover:text-zinc-700 dark:hover:text-zinc-200 hover:bg-zinc-100 dark:hover:bg-zinc-800'
              }`}
            >
              {tab.label}
              {tab.key === 'favoriten' && favoriteContacts.length > 0 && (
                <span className="ml-1.5 text-xs px-1.5 py-0.5 rounded-full bg-amber-100 dark:bg-amber-900/40 text-amber-700 dark:text-amber-300 font-mono">
                  {favoriteContacts.length}
                </span>
              )}
            </button>
          ))}
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
          {/* ── Intern Tab ── */}
          {activeTab === 'intern' && (
            <>
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
                      : 'Es sind noch keine internen Kontakte vorhanden.'
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
            </>
          )}

          {/* ── Eigene Tab ── */}
          {activeTab === 'eigene' && (
            <>
              {filteredLocalContacts.length === 0 && (
                <EmptyState
                  icon={<Users className="w-8 h-8 text-zinc-400 dark:text-zinc-600" />}
                  title="Keine eigenen Kontakte"
                  description={
                    searchQuery
                      ? `Keine Ergebnisse für „${searchQuery}"`
                      : 'Erstellen Sie Ihren ersten eigenen Kontakt mit dem „Neu"-Button.'
                  }
                />
              )}
              {filteredLocalContacts.length > 0 && (
                <ul className="divide-y divide-zinc-100 dark:divide-zinc-800">
                  {filteredLocalContacts.map((contact) => (
                    <li key={contact.id}>
                      <LocalContactCard
                        contact={contact}
                        onDial={handleDial}
                        onEdit={handleEditLocalContact}
                        onDelete={handleDeleteLocalContact}
                        onToggleFavorite={toggleFavorite}
                      />
                    </li>
                  ))}
                </ul>
              )}
            </>
          )}

          {/* ── Favoriten Tab ── */}
          {activeTab === 'favoriten' && (
            <>
              {favoriteContacts.length === 0 && (
                <EmptyState
                  icon={<Star className="w-8 h-8 text-zinc-400 dark:text-zinc-600" />}
                  title="Keine Favoriten"
                  description="Markieren Sie Kontakte mit dem Stern-Symbol als Favorit."
                />
              )}
              {favoriteContacts.length > 0 && (
                <ul className="divide-y divide-zinc-100 dark:divide-zinc-800">
                  {favoriteContacts.map((contact) => (
                    <li key={contact.id}>
                      <LocalContactCard
                        contact={contact}
                        onDial={handleDial}
                        onEdit={handleEditLocalContact}
                        onDelete={handleDeleteLocalContact}
                        onToggleFavorite={toggleFavorite}
                      />
                    </li>
                  ))}
                </ul>
              )}
            </>
          )}
        </div>
      </div>

      {/* Detail panel (für interne Kontakte) */}
      {selectedContact && (
        <div className="flex-1 h-full overflow-y-auto">
          <ContactDetail
            contact={selectedContact}
            onDial={handleDetailDial}
            onClose={handleDetailClose}
          />
        </div>
      )}

      {/* Add/Edit Dialog */}
      {showAddDialog && (
        <AddContactDialog
          editContact={editingContact}
          onClose={handleCloseDialog}
        />
      )}
    </div>
  );
}

// ── Lokale Kontaktkarte ────────────────────────────────────────────────────

interface LocalContactCardProps {
  contact: LocalContact;
  onDial: (number: string) => void;
  onEdit: (contact: LocalContact) => void;
  onDelete: (id: string) => void;
  onToggleFavorite: (id: string) => void;
}

function LocalContactCard({
  contact,
  onDial,
  onEdit,
  onDelete,
  onToggleFavorite,
}: LocalContactCardProps): React.JSX.Element {
  const [hovered, setHovered] = useState(false);
  const primaryNumber = contact.numbers[0] ?? '';

  function handleDialClick(e: React.MouseEvent<HTMLButtonElement>): void {
    e.stopPropagation();
    if (primaryNumber) onDial(primaryNumber);
  }

  function handleEditClick(e: React.MouseEvent<HTMLButtonElement>): void {
    e.stopPropagation();
    onEdit(contact);
  }

  function handleDeleteClick(e: React.MouseEvent<HTMLButtonElement>): void {
    e.stopPropagation();
    onDelete(contact.id);
  }

  function handleFavoriteClick(e: React.MouseEvent<HTMLButtonElement>): void {
    e.stopPropagation();
    onToggleFavorite(contact.id);
  }

  return (
    <div
      onMouseEnter={() => setHovered(true)}
      onMouseLeave={() => setHovered(false)}
      className="flex items-center gap-3 px-4 py-3 hover:bg-zinc-50 dark:hover:bg-zinc-800/60 transition-colors"
    >
      {/* Avatar */}
      <div className="relative flex-none">
        <Avatar name={contact.name} size="md" />
      </div>

      {/* Info */}
      <div className="flex-1 min-w-0">
        <div className="flex items-center gap-2">
          <span className="font-medium text-sm text-zinc-900 dark:text-zinc-100 truncate">
            {contact.name}
          </span>
          {contact.company && (
            <span className="hidden sm:inline-flex flex-none items-center gap-1 text-xs px-1.5 py-0.5 rounded bg-zinc-100 dark:bg-zinc-700 text-zinc-500 dark:text-zinc-400 font-normal">
              <Building2 className="w-3 h-3" />
              {contact.company}
            </span>
          )}
        </div>
        <div className="text-xs text-zinc-500 dark:text-zinc-400 truncate mt-0.5 font-mono">
          {primaryNumber}
          {contact.numbers.length > 1 && (
            <span className="ml-1.5 text-zinc-400 dark:text-zinc-500 font-sans">
              +{contact.numbers.length - 1} weitere
            </span>
          )}
        </div>
        {contact.email && (
          <div className="flex items-center gap-1 text-xs text-zinc-400 dark:text-zinc-500 mt-0.5 truncate">
            <Mail className="w-3 h-3 flex-none" />
            <span className="truncate">{contact.email}</span>
          </div>
        )}
      </div>

      {/* Actions */}
      <div
        className={`flex items-center gap-1 flex-none transition-opacity duration-100 ${
          hovered ? 'opacity-100' : 'opacity-0 pointer-events-none'
        }`}
      >
        {/* Favorit-Stern */}
        <button
          type="button"
          onClick={handleFavoriteClick}
          title={contact.isFavorite ? 'Aus Favoriten entfernen' : 'Zu Favoriten hinzufügen'}
          className={`flex items-center justify-center w-7 h-7 rounded-full transition-colors ${
            contact.isFavorite
              ? 'text-amber-500 hover:text-amber-600 bg-amber-50 dark:bg-amber-900/20'
              : 'text-zinc-400 hover:text-amber-500 hover:bg-amber-50 dark:hover:bg-amber-900/20'
          }`}
        >
          <Star className="w-3.5 h-3.5" fill={contact.isFavorite ? 'currentColor' : 'none'} />
        </button>

        {/* Bearbeiten */}
        <button
          type="button"
          onClick={handleEditClick}
          title="Kontakt bearbeiten"
          className="flex items-center justify-center w-7 h-7 rounded-full text-zinc-400 hover:text-blue-500 hover:bg-blue-50 dark:hover:bg-blue-900/20 transition-colors"
        >
          <Pencil className="w-3.5 h-3.5" />
        </button>

        {/* Löschen */}
        <button
          type="button"
          onClick={handleDeleteClick}
          title="Kontakt löschen"
          className="flex items-center justify-center w-7 h-7 rounded-full text-zinc-400 hover:text-red-500 hover:bg-red-50 dark:hover:bg-red-900/20 transition-colors"
        >
          <Trash2 className="w-3.5 h-3.5" />
        </button>

        {/* Anrufen */}
        {primaryNumber && (
          <button
            type="button"
            onClick={handleDialClick}
            title={`Anrufen: ${primaryNumber}`}
            className="flex items-center justify-center w-8 h-8 rounded-full bg-green-500 hover:bg-green-600 text-white shadow-sm transition-colors"
          >
            <Phone className="w-4 h-4" />
          </button>
        )}
      </div>

      {/* Stern immer sichtbar wenn Favorit */}
      {contact.isFavorite && !hovered && (
        <div className="flex-none">
          <Star className="w-3.5 h-3.5 text-amber-500" fill="currentColor" />
        </div>
      )}
    </div>
  );
}

