import { create } from 'zustand';
import { CallHistoryEntry } from '../types/swyx';

interface HistoryStoreState {
  entries: CallHistoryEntry[];
  loading: boolean;
  fetchHistory: () => Promise<void>;
  addEntry: (entry: CallHistoryEntry) => void;
}

const MOCK_HISTORY: CallHistoryEntry[] = [
  {
    id: 'h1',
    callerName: 'Klaus Müller',
    callerNumber: '+49 211 123456',
    direction: 'inbound',
    timestamp: Date.now() - 1000 * 60 * 5,
    duration: 127,
  },
  {
    id: 'h2',
    callerName: 'Kundenservice Bosch',
    callerNumber: '+49 711 400040',
    direction: 'outbound',
    timestamp: Date.now() - 1000 * 60 * 30,
    duration: 48,
  },
  {
    id: 'h3',
    callerName: 'Anna Schmidt',
    callerNumber: '101',
    direction: 'missed',
    timestamp: Date.now() - 1000 * 60 * 75,
    duration: 0,
  },
  {
    id: 'h4',
    callerName: 'Hans Weber',
    callerNumber: '+49 89 543210',
    direction: 'inbound',
    timestamp: Date.now() - 1000 * 60 * 120,
    duration: 302,
  },
  {
    id: 'h5',
    callerName: 'Zentrale',
    callerNumber: '0',
    direction: 'outbound',
    timestamp: Date.now() - 1000 * 60 * 240,
    duration: 15,
  },
  {
    id: 'h6',
    callerName: '',
    callerNumber: '+49 30 987654',
    direction: 'missed',
    timestamp: Date.now() - 1000 * 60 * 60 * 5,
    duration: 0,
  },
];

export const useHistoryStore = create<HistoryStoreState>((set) => ({
  entries: MOCK_HISTORY,
  loading: false,

  fetchHistory: async () => {
    set({ loading: true });
    try {
      const entries = await window.swyxApi.getHistory();
      set({ entries, loading: false });
    } catch {
      set({ loading: false });
    }
  },

  addEntry: (entry) =>
    set((state) => ({ entries: [entry, ...state.entries] })),
}));

export function getMissedCount(entries: CallHistoryEntry[]): number {
  return entries.filter((e) => e.direction === 'missed').length;
}
