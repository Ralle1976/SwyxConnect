import { create } from 'zustand';
import { Contact, PresenceStatus } from '../types/swyx';

interface ContactStoreState {
  contacts: Contact[];
  searchQuery: string;
  loading: boolean;
  searchContacts: (query: string) => Promise<void>;
  setQuery: (query: string) => void;
}

const MOCK_CONTACTS: Contact[] = [
  {
    id: 'c1',
    name: 'Klaus Müller',
    number: '+49 211 123456',
    email: 'k.mueller@firma.de',
    department: 'Vertrieb',
    presence: PresenceStatus.Available,
  },
  {
    id: 'c2',
    name: 'Anna Schmidt',
    number: '101',
    email: 'a.schmidt@firma.de',
    department: 'Support',
    presence: PresenceStatus.Busy,
  },
  {
    id: 'c3',
    name: 'Hans Weber',
    number: '+49 89 543210',
    email: 'h.weber@firma.de',
    department: 'IT',
    presence: PresenceStatus.Away,
  },
  {
    id: 'c4',
    name: 'Maria Fischer',
    number: '102',
    email: 'm.fischer@firma.de',
    department: 'Buchhaltung',
    presence: PresenceStatus.Available,
  },
  {
    id: 'c5',
    name: 'Petra Braun',
    number: '+49 40 112233',
    email: 'p.braun@firma.de',
    department: 'Personalwesen',
    presence: PresenceStatus.DND,
  },
  {
    id: 'c6',
    name: 'Stefan Hoffmann',
    number: '103',
    email: 's.hoffmann@firma.de',
    department: 'Geschäftsführung',
    presence: PresenceStatus.Offline,
  },
  {
    id: 'c7',
    name: 'Laura Bauer',
    number: '+49 69 778899',
    email: 'l.bauer@firma.de',
    department: 'Marketing',
    presence: PresenceStatus.Available,
  },
  {
    id: 'c8',
    name: 'Thomas Wolf',
    number: '104',
    email: 't.wolf@firma.de',
    department: 'Einkauf',
    presence: PresenceStatus.Away,
  },
];

export const useContactStore = create<ContactStoreState>((set) => ({
  contacts: MOCK_CONTACTS,
  searchQuery: '',
  loading: false,

  searchContacts: async (query) => {
    set({ searchQuery: query, loading: true });
    try {
      if (query.trim().length === 0) {
        set({ contacts: MOCK_CONTACTS, loading: false });
        return;
      }
      const contacts = await window.swyxApi.getContacts(query);
      set({ contacts, loading: false });
    } catch {
      const q = query.toLowerCase();
      const filtered = MOCK_CONTACTS.filter(
        (c) =>
          c.name.toLowerCase().includes(q) ||
          c.number.includes(q) ||
          c.email?.toLowerCase().includes(q) ||
          c.department?.toLowerCase().includes(q)
      );
      set({ contacts: filtered, loading: false });
    }
  },

  setQuery: (query) => set({ searchQuery: query }),
}));
