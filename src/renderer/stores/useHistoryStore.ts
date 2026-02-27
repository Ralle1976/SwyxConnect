import { create } from 'zustand';
import { persist } from 'zustand/middleware';
import { CallHistoryEntry } from '../types/swyx';

interface HistoryStoreState {
  entries: CallHistoryEntry[];
  loading: boolean;
  addEntry: (entry: CallHistoryEntry) => void;
  clearHistory: () => void;
}

export const useHistoryStore = create<HistoryStoreState>()(
  persist(
    (set) => ({
      entries: [],
      loading: false,

      addEntry: (entry) =>
        set((state) => {
          // Prevent duplicates if an entry with the same ID arrives
          if (state.entries.some((e) => e.id === entry.id)) return state;
          return { entries: [entry, ...state.entries] };
        }),

      clearHistory: () => set({ entries: [] }),
    }),
    {
      name: 'swyxit-history-storage',
    }
  )
);

export function getMissedCount(entries: CallHistoryEntry[]): number {
  if (!Array.isArray(entries)) return 0;
  return entries.filter((e) => e.direction === 'missed').length;
}


