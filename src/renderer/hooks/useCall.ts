import { useCallback } from 'react';
import { useLineStore } from '../stores/useLineStore';
import { LineState } from '../types/swyx';

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

export function useCall(): CallHookResult {
  const updateLine = useLineStore((s) => s.updateLine);
  const setLines   = useLineStore((s) => s.setLines);

  const dial = useCallback(async (number: string) => {
    await window.swyxApi.dial(number);
    // Sofort nach dem Wählen den Leitungsstatus abfragen und UI aktualisieren
    try {
      const result = await window.swyxApi.getLines();
      const lines = Array.isArray(result)
        ? result
        : (result as { lines: unknown[] } | null)?.lines ?? [];
      if (lines.length > 0) setLines(lines as import('../types/swyx').LineInfo[]);
    } catch {
      // Fehler ignorieren — lineStateChanged-Event folgt ohnehin
    }
  }, [setLines]);

  const answer = useCallback(async (lineId: number) => {
    await window.swyxApi.answer(lineId);
    updateLine(lineId, { state: LineState.Active });
  }, [updateLine]);

  const hangup = useCallback(async (lineId: number) => {
    await window.swyxApi.hangup(lineId);
    updateLine(lineId, {
      state: LineState.Inactive,
      callerName: undefined,
      callerNumber: undefined,
      duration: undefined,
    });
  }, [updateLine]);

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
