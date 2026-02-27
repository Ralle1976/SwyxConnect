import { create } from 'zustand';
import { persist } from 'zustand/middleware';
import { CallHistoryEntry } from '../types/swyx';

interface HistoryStoreState {
  entries: CallHistoryEntry[];
  loading: boolean;
  addEntry: (entry: CallHistoryEntry) => void;
  clearHistory: () => void;
  fetchHistory: () => Promise<void>;
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

      fetchHistory: async () => {
        set({ loading: true });
        try {
          const data = await window.swyxApi.getHistory();
          if (Array.isArray(data) && data.length > 0) {
            set({ entries: data });
          }
        } catch {
          // Bridge not available or no history — keep existing entries
        } finally {
          set({ loading: false });
        }
      },
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


