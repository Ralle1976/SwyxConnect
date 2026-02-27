import { create } from 'zustand';
import { PresenceStatus, ColleaguePresence } from '../types/swyx';

interface PresenceStoreState {
  ownStatus: PresenceStatus;
  colleagues: ColleaguePresence[];
  setOwnStatus: (status: PresenceStatus) => void;
  updateColleague: (userId: string, updates: Partial<ColleaguePresence>) => void;
  setColleagues: (colleagues: ColleaguePresence[]) => void;
}

const MOCK_COLLEAGUES: ColleaguePresence[] = [
  { userId: 'u1', name: 'Klaus Müller', status: PresenceStatus.Available },
  { userId: 'u2', name: 'Anna Schmidt', status: PresenceStatus.Busy, statusText: 'Im Gespräch' },
  { userId: 'u3', name: 'Hans Weber', status: PresenceStatus.Away, statusText: 'Mittagspause' },
  { userId: 'u4', name: 'Maria Fischer', status: PresenceStatus.Available },
  { userId: 'u5', name: 'Petra Braun', status: PresenceStatus.DND, statusText: 'Bitte nicht stören' },
  { userId: 'u6', name: 'Stefan Hoffmann', status: PresenceStatus.Offline },
  { userId: 'u7', name: 'Laura Bauer', status: PresenceStatus.Available },
  { userId: 'u8', name: 'Thomas Wolf', status: PresenceStatus.Away },
];

export const usePresenceStore = create<PresenceStoreState>((set) => ({
  ownStatus: PresenceStatus.Available,
  colleagues: MOCK_COLLEAGUES,

  setOwnStatus: (status) => set({ ownStatus: status }),

  updateColleague: (userId, updates) =>
    set((state) => ({
      colleagues: state.colleagues.map((c) =>
        c.userId === userId ? { ...c, ...updates } : c
      ),
    })),

  setColleagues: (colleagues) => set({ colleagues }),
}));
