import { create } from 'zustand';
import { persist } from 'zustand/middleware';

export interface LocalContact {
  id: string;
  name: string;
  numbers: string[];
  email?: string;
  company?: string;
  notes?: string;
  isFavorite: boolean;
  createdAt: number;
  updatedAt: number;
}

/** Normalisiert Rufnummern für Vergleich: Leerzeichen/Trennzeichen entfernen, +49 ↔ 0049 normalisieren */
function normalizeNumber(num: string): string {
  // Leerzeichen, Bindestriche, Klammern, Schrägstriche entfernen
  let n = num.replace(/[\s\-()./]/g, '');
  // +49… → 0049…
  if (n.startsWith('+')) {
    n = '00' + n.slice(1);
  }
  return n.toLowerCase();
}

type AddPayload = Omit<LocalContact, 'id' | 'createdAt' | 'updatedAt'>;
type UpdatePayload = Partial<Omit<LocalContact, 'id' | 'createdAt'>>;

interface LocalContactStoreState {
  contacts: LocalContact[];
  addContact: (contact: AddPayload) => void;
  updateContact: (id: string, updates: UpdatePayload) => void;
  deleteContact: (id: string) => void;
  toggleFavorite: (id: string) => void;
  findByNumber: (number: string) => LocalContact | undefined;
}

export const useLocalContactStore = create<LocalContactStoreState>()(
  persist(
    (set, get) => ({
      contacts: [],

      addContact: (contact) =>
        set((state) => ({
          contacts: [
            ...state.contacts,
            {
              ...contact,
              id: crypto.randomUUID(),
              createdAt: Date.now(),
              updatedAt: Date.now(),
            },
          ],
        })),

      updateContact: (id, updates) =>
        set((state) => ({
          contacts: state.contacts.map((c) =>
            c.id === id ? { ...c, ...updates, updatedAt: Date.now() } : c
          ),
        })),

      deleteContact: (id) =>
        set((state) => ({
          contacts: state.contacts.filter((c) => c.id !== id),
        })),

      toggleFavorite: (id) =>
        set((state) => ({
          contacts: state.contacts.map((c) =>
            c.id === id
              ? { ...c, isFavorite: !c.isFavorite, updatedAt: Date.now() }
              : c
          ),
        })),

      findByNumber: (number) => {
        if (!number) return undefined;
        const normalized = normalizeNumber(number);
        return get().contacts.find((c) =>
          c.numbers.some((n) => normalizeNumber(n) === normalized)
        );
      },
    }),
    {
      name: 'swyxconnect-local-contacts',
    }
  )
);
