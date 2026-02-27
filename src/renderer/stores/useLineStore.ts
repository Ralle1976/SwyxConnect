import { create } from 'zustand';
import { LineInfo, CallDetails, LineState } from '../types/swyx';

interface LineStoreState {
  lines: LineInfo[];
  selectedLineId: number | null;
  activeCall: CallDetails | null;
  setLines: (lines: LineInfo[]) => void;
  updateLine: (lineId: number, updates: Partial<LineInfo>) => void;
  selectLine: (lineId: number | null) => void;
  setActiveCall: (call: CallDetails | null) => void;
  clearActiveCall: () => void;
}

const MOCK_LINES: LineInfo[] = [
  { id: 1, state: LineState.Inactive, isSelected: true },
  { id: 2, state: LineState.Inactive, isSelected: false },
  { id: 3, state: LineState.Inactive, isSelected: false },
  { id: 4, state: LineState.Inactive, isSelected: false },
];

export const useLineStore = create<LineStoreState>((set) => ({
  lines: MOCK_LINES,
  selectedLineId: 1,
  activeCall: null,

  setLines: (lines) => set({ lines }),

  updateLine: (lineId, updates) =>
    set((state) => ({
      lines: state.lines.map((line) =>
        line.id === lineId ? { ...line, ...updates } : line
      ),
    })),

  selectLine: (lineId) =>
    set((state) => ({
      selectedLineId: lineId,
      lines: state.lines.map((line) => ({
        ...line,
        isSelected: line.id === lineId,
      })),
    })),

  setActiveCall: (call) => set({ activeCall: call }),
  clearActiveCall: () => set({ activeCall: null }),
}));

export function hasActiveCall(lines: LineInfo[]): boolean {
  return lines.some(
    (l) =>
      l.state === LineState.Active ||
      l.state === LineState.OnHold ||
      l.state === LineState.ConferenceActive ||
      l.state === LineState.ConferenceOnHold
  );
}

export function getRingingLines(lines: LineInfo[]): LineInfo[] {
  return lines.filter((l) => l.state === LineState.Ringing);
}
