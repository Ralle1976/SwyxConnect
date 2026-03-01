import { create } from 'zustand';
import { Contact, PresenceStatus } from '../types/swyx';

interface ContactStoreState {
  contacts: Contact[];
  searchQuery: string;
  loading: boolean;
  searchContacts: (query: string) => Promise<void>;
  setQuery: (query: string) => void;
}

export const useContactStore = create<ContactStoreState>((set) => ({
  contacts: [],
  searchQuery: '',
  loading: false,

  searchContacts: async (query) => {
    set({ searchQuery: query, loading: true });
    try {
      // Leerer Query = Phonebook laden (alle Kontakte)
      const raw = await window.swyxApi.getContacts(query.trim());
      const contacts = Array.isArray(raw) ? raw : [];
      set({ contacts, loading: false });
    } catch (err) {
      console.error('Failed to get contacts', err);
      set({ contacts: [], loading: false });
    }
  },

  setQuery: (query) => set({ searchQuery: query }),
}));
