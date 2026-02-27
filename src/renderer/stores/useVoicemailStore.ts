import { create } from 'zustand';
import { VoicemailEntry } from '../types/swyx';

interface VoicemailStoreState {
  messages: VoicemailEntry[];
  loading: boolean;
  fetchVoicemails: () => Promise<void>;
  markRead: (id: string) => void;
  deleteMessage: (id: string) => void;
}

const MOCK_VOICEMAILS: VoicemailEntry[] = [
  {
    id: 'v1',
    callerName: 'Klaus Müller',
    callerNumber: '+49 211 123456',
    timestamp: Date.now() - 1000 * 60 * 15,
    duration: 42,
    isNew: true,
  },
  {
    id: 'v2',
    callerName: 'Kundenhotline Telekom',
    callerNumber: '+49 800 330 1000',
    timestamp: Date.now() - 1000 * 60 * 90,
    duration: 18,
    isNew: true,
  },
  {
    id: 'v3',
    callerName: 'Hans Weber',
    callerNumber: '+49 89 543210',
    timestamp: Date.now() - 1000 * 60 * 60 * 3,
    duration: 95,
    isNew: false,
  },
];

export const useVoicemailStore = create<VoicemailStoreState>((set) => ({
  messages: MOCK_VOICEMAILS,
  loading: false,

  fetchVoicemails: async () => {
    set({ loading: true });
    try {
      const messages = await window.swyxApi.getVoicemails();
      set({ messages, loading: false });
    } catch {
      set({ loading: false });
    }
  },

  markRead: (id) =>
    set((state) => ({
      messages: state.messages.map((m) =>
        m.id === id ? { ...m, isNew: false } : m
      ),
    })),

  deleteMessage: (id) =>
    set((state) => ({
      messages: state.messages.filter((m) => m.id !== id),
    })),
}));

export function getNewVoicemailCount(messages: VoicemailEntry[]): number {
  return messages.filter((m) => m.isNew).length;
}
