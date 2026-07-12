import { create } from 'zustand';
import { PresenceStatus, ColleaguePresence } from '../types/swyx';

interface PresenceStoreState {
  ownStatus: PresenceStatus;
  colleagues: ColleaguePresence[];
  setOwnStatus: (status: PresenceStatus) => void;
  updateColleague: (userId: string, updates: Partial<ColleaguePresence>) => void;
  setColleagues: (colleagues: ColleaguePresence[]) => void;
}

export const usePresenceStore = create<PresenceStoreState>((set) => ({
  ownStatus: PresenceStatus.Available,
  colleagues: [],

  setOwnStatus: (status) => set({ ownStatus: status }),

  updateColleague: (userId, updates) =>
    set((state) => ({
      colleagues: state.colleagues.map((c) =>
        c.userId === userId ? { ...c, ...updates } : c
      ),
    })),

  setColleagues: (colleagues) => {
    if (Array.isArray(colleagues)) {
      set({ colleagues });
    }
  },
}));
