import { create } from 'zustand';
import { LineInfo, CallDetails, LineState } from '../types/swyx';

interface LineStoreState {
  lines: LineInfo[];
  selectedLineId: number | null;
  activeCall: CallDetails | null;
  /** Tracks the last dialed number per line so we can display it before COM reports back */
  dialedNumbers: Record<number, string>;
  setLines: (lines: LineInfo[]) => void;
  updateLine: (lineId: number, updates: Partial<LineInfo>) => void;
  selectLine: (lineId: number | null) => void;
  setActiveCall: (call: CallDetails | null) => void;
  clearActiveCall: () => void;
  setDialedNumber: (lineId: number, number: string) => void;
  getDisplayNumber: (line: LineInfo) => string;
  getDisplayName: (line: LineInfo) => string;
}

export const useLineStore = create<LineStoreState>((set) => ({
  lines: [],
  selectedLineId: null,
  activeCall: null,
  dialedNumbers: {},

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

  setDialedNumber: (lineId, number) =>
    set((state) => ({
      dialedNumbers: { ...state.dialedNumbers, [lineId]: number },
    })),

  getDisplayNumber: (line) => {
    const { dialedNumbers } = useLineStore.getState();
    return line.callerNumber || dialedNumbers[line.id] || '';
  },

  getDisplayName: (line) => {
    // Name from COM, or fallback to number, or 'Unbekannt'
    if (line.callerName) return line.callerName;
    const num = useLineStore.getState().getDisplayNumber(line);
    return num || 'Unbekannt';
  },
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

export function getActiveLines(lines: LineInfo[]): LineInfo[] {
  return lines.filter(
    (l) =>
      l.state === LineState.Active ||
      l.state === LineState.ConferenceActive ||
      l.state === LineState.Dialing ||
      l.state === LineState.Alerting ||
      l.state === LineState.Busy
  );
}

export function getHeldLines(lines: LineInfo[]): LineInfo[] {
  return lines.filter(
    (l) => l.state === LineState.OnHold || l.state === LineState.ConferenceOnHold
  );
}
