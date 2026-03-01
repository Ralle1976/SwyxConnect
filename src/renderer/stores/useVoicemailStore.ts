import { create } from 'zustand';
import { VoicemailEntry } from '../types/swyx';

interface VoicemailStoreState {
  messages: VoicemailEntry[];
  loading: boolean;
  fetchVoicemails: () => Promise<void>;
  markRead: (id: string) => void;
  deleteMessage: (id: string) => void;
}


export const useVoicemailStore = create<VoicemailStoreState>((set) => ({
  messages: [],
  loading: false,

  fetchVoicemails: async () => {
    set({ loading: true });
    try {
      const raw = await window.swyxApi.getVoicemails();
      const messages = Array.isArray(raw) ? raw : [];
      set({ messages: messages, loading: false });
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
  if (!Array.isArray(messages)) return 0;
  return messages.filter((m) => m.isNew).length;
}
