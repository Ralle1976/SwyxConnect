import { useCallback, useRef } from 'react';
import { useLineStore } from '../stores/useLineStore';
import { useSettingsStore } from '../stores/useSettingsStore';
import { LineState, LineInfo } from '../types/swyx';

export interface CallHookResult {
  dial: (number: string) => Promise<void>;
  answer: (lineId: number) => Promise<void>;
  hangup: (lineId: number) => Promise<void>;
  hold: (lineId: number) => Promise<void>;
  unhold: (lineId: number) => Promise<void>;
  transfer: (lineId: number, target: string) => Promise<void>;
  sendDtmf: (lineId: number, digit: string) => Promise<void>;
  mute: (lineId: number) => Promise<void>;
  unmute: (lineId: number) => Promise<void>;
}

// States that indicate a call is ongoing (not idle)
const ACTIVE_STATES: LineState[] = [
  LineState.Ringing,
  LineState.Dialing,
  LineState.Alerting,
  LineState.Active,
  LineState.OnHold,
  LineState.Busy,
  LineState.ConferenceActive,
  LineState.ConferenceOnHold,
  LineState.Transferring,
  LineState.Knocking,
];

function isCallActive(state: LineState): boolean {
  return ACTIVE_STATES.includes(state);
}

export function useCall(): CallHookResult {
  const updateLine = useLineStore((s) => s.updateLine);
  const setLines   = useLineStore((s) => s.setLines);
  const trunkPrefix = useSettingsStore((s) => s.trunkPrefix);
  const trunkPrefixEnabled = useSettingsStore((s) => s.trunkPrefixEnabled);

  // Active polling timer — keeps line state fresh while a call is ongoing.
  // COM push events (PubOnLineMgrNotification) are unreliable in Auto-Attach mode,
  // so we poll getLines() every 1.5s until all lines go back to Inactive.
  const pollTimerRef = useRef<ReturnType<typeof setInterval> | null>(null);

  const fetchLines = useCallback(async (): Promise<LineInfo[]> => {
    try {
      const result = await window.swyxApi.getLines();
      const lines = Array.isArray(result)
        ? result
        : (result as { lines: unknown[] } | null)?.lines ?? [];
      return lines as LineInfo[];
    } catch {
      return [];
    }
  }, []);

  const startPolling = useCallback(() => {
    if (pollTimerRef.current) return; // already polling

    pollTimerRef.current = setInterval(async () => {
      const lines = await fetchLines();
      if (lines.length > 0) {
        setLines(lines);
        // Stop polling when all lines are inactive
        const anyActive = lines.some((l) => isCallActive(l.state));
        if (!anyActive) {
          if (pollTimerRef.current) {
            clearInterval(pollTimerRef.current);
            pollTimerRef.current = null;
          }
        }
      }
    }, 1500);
  }, [fetchLines, setLines]);

  const dial = useCallback(async (number: string) => {
    let target = number.trim();
    if (trunkPrefixEnabled && trunkPrefix && target.length > 4 && !target.startsWith(trunkPrefix)) {
      target = trunkPrefix + target;
    }
    await window.swyxApi.dial(target);

    // Fetch lines immediately, then start polling until call ends.
    // COM events are unreliable in Auto-Attach mode, so polling is the fallback.
    const lines = await fetchLines();
    if (lines.length > 0) setLines(lines);
    startPolling();
  }, [fetchLines, setLines, startPolling, trunkPrefix, trunkPrefixEnabled]);

  const answer = useCallback(async (lineId: number) => {
    await window.swyxApi.answer(lineId);
    updateLine(lineId, { state: LineState.Active });
    startPolling();
  }, [updateLine, startPolling]);

  const hangup = useCallback(async (lineId: number) => {
    await window.swyxApi.hangup(lineId);
    updateLine(lineId, {
      state: LineState.Inactive,
      callerName: undefined,
      callerNumber: undefined,
      duration: undefined,
    });
    // Keep polling for 2 more cycles to catch any server-side state changes
    setTimeout(async () => {
      const lines = await fetchLines();
      if (lines.length > 0) setLines(lines);
      const anyActive = lines.some((l) => isCallActive(l.state));
      if (!anyActive && pollTimerRef.current) {
        clearInterval(pollTimerRef.current);
        pollTimerRef.current = null;
      }
    }, 2000);
  }, [updateLine, fetchLines, setLines]);

  const hold = useCallback(async (lineId: number) => {
    await window.swyxApi.hold(lineId);
    updateLine(lineId, { state: LineState.OnHold });
  }, [updateLine]);

  const unhold = useCallback(async (lineId: number) => {
    await window.swyxApi.hold(lineId);
    updateLine(lineId, { state: LineState.Active });
  }, [updateLine]);

  const transfer = useCallback(
    async (lineId: number, target: string) => {
      await window.swyxApi.transfer(lineId, target);
      updateLine(lineId, { state: LineState.Transferring });
    },
    [updateLine]
  );

  const sendDtmf = useCallback(async (lineId: number, digit: string) => {
    await window.swyxApi.sendDtmf(lineId, digit);
  }, []);

  const mute = useCallback(async (lineId: number) => {
    await window.swyxApi.mute(lineId);
  }, []);

  const unmute = useCallback(async (lineId: number) => {
    await window.swyxApi.unmute(lineId);
  }, []);

  return { dial, answer, hangup, hold, unhold, transfer, sendDtmf, mute, unmute };
}
