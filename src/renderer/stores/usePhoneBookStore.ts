import { create } from 'zustand';
import type {
  PhoneBookEntry,
  PhoneBookReply,
  JournalEntry,
  JournalPart,
} from '../types/swyx';

interface PhoneBookStoreState {
  phoneBook: PhoneBookEntry[];
  journal: JournalEntry[];
  available: boolean;
  loading: boolean;
  error: string | null;

  refreshPhoneBook: () => Promise<void>;
  refreshJournal: (part?: JournalPart) => Promise<void>;
  setAvailable: (ok: boolean) => void;
}

// Part codes for cs.getCallJournal (0=all, 1=missed, 2=outgoing, 3=incoming)
const PART_CODE: Record<JournalPart, number> = {
  all: 0,
  missed: 1,
  outgoing: 2,
  incoming: 3,
};

function asPhoneBookReply(payload: unknown): PhoneBookEntry[] {
  if (!payload || typeof payload !== 'object') return [];
  const root = payload as Partial<PhoneBookReply> & { entries?: unknown };
  if (Array.isArray(root.entries)) return root.entries as PhoneBookEntry[];
  if (Array.isArray(payload)) return payload as PhoneBookEntry[];
  return [];
}

function asJournal(payload: unknown): JournalEntry[] {
  if (Array.isArray(payload)) return payload as JournalEntry[];
  return [];
}

let markedAvailable = false;

export const usePhoneBookStore = create<PhoneBookStoreState>((set, get) => ({
  phoneBook: [],
  journal: [],
  available: false,
  loading: false,
  error: null,

  refreshPhoneBook: async () => {
    try {
      const payload = await window.swyxApi.csGetPhoneBook();
      const entries = asPhoneBookReply(payload);
      set({ phoneBook: entries });
      if (entries.length > 0 || !markedAvailable) {
        markedAvailable = true;
        set({ available: true, error: null });
      }
    } catch (err) {
      if (!markedAvailable) {
        set({
          available: false,
          error: err instanceof Error ? err.message : 'ComSocket nicht verfügbar',
        });
      }
    }
  },

  refreshJournal: async (part: JournalPart = 'all') => {
    try {
      const payload = await window.swyxApi.csGetCallJournal(PART_CODE[part]);
      set({ journal: asJournal(payload) });
      if (!markedAvailable) {
        markedAvailable = true;
        set({ available: true });
      }
    } catch (err) {
      if (!markedAvailable) {
        set({
          available: false,
          error: err instanceof Error ? err.message : 'ComSocket nicht verfügbar',
        });
      }
    }
  },

  setAvailable: (ok: boolean) => set({ available: ok }),
}));
