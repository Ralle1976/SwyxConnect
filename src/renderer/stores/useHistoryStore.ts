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
            // COM-History mit lokalen Einträgen zusammenführen
            // COM-Einträge haben andere IDs als lokale (crypto.randomUUID)
            // Deduplizierung: gleiche Nummer + ähnlicher Zeitstempel (±30s) = gleicher Anruf
            set((state) => {
              const existing = state.entries;
              const merged = [...existing];
              for (const comEntry of data) {
                const isDuplicate = existing.some((local) => {
                  if (local.callerNumber !== comEntry.callerNumber) return false;
                  const timeDiff = Math.abs(
                    (local.timestamp || 0) - (comEntry.timestamp || 0)
                  );
                  return timeDiff < 30_000; // 30 Sekunden Toleranz
                });
                if (!isDuplicate) {
                  merged.push(comEntry);
                }
              }
              // Nach Zeitstempel sortieren, neueste zuerst
              merged.sort((a, b) => (b.timestamp || 0) - (a.timestamp || 0));
              return { entries: merged };
            });
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


