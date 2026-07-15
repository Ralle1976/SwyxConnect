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

export const useLineStore = create<LineStoreState>((set) => ({
  lines: [],
  selectedLineId: null,
  activeCall: null,

  setLines: (lines) => set(() => {
    // Auto-select erste aktive Leitung (state != Inactive), damit ActiveCallPanel erscheint
    const activeLine = lines.find(
      (l) => l.state !== LineState.Inactive && l.state !== LineState.Terminated && l.state !== LineState.Disabled
    );
    const selectedLineId = activeLine ? activeLine.id : (lines.length > 0 ? lines[0].id : null);
    return {
      lines: lines.map((l) => ({ ...l, isSelected: l.id === selectedLineId })),
      selectedLineId,
    };
  }),

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
