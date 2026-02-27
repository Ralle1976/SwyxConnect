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
      if (query.trim().length === 0) {
        set({ contacts: [], loading: false });
        return;
      }
      const raw = await window.swyxApi.getContacts(query);
      const contacts = Array.isArray(raw) ? raw : [];
      set({ contacts, loading: false });
    } catch (err) {
      console.error('Failed to get contacts', err);
      set({ contacts: [], loading: false });
    }
  },

  setQuery: (query) => set({ searchQuery: query }),
}));
