import { useCallback } from 'react';
import { useLineStore } from '../stores/useLineStore';
import { useSettingsStore } from '../stores/useSettingsStore';
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
  const updateLine      = useLineStore((s) => s.updateLine);
  const setLines        = useLineStore((s) => s.setLines);
  const setDialedNumber = useLineStore((s) => s.setDialedNumber);

  const dial = useCallback(async (number: string) => {
    // Amtsholung: Präfix für externe Anrufe hinzufügen
    let dialNumber = number;
    const { externalLinePrefixEnabled, externalLinePrefix } = useSettingsStore.getState();
    if (externalLinePrefixEnabled && externalLinePrefix && dialNumber.length > 0) {
      // Präfix nur hinzufügen wenn die Nummer nicht bereits damit beginnt
      // und nicht mit + oder * oder # anfängt (Sonderzeichen/international)
      if (!dialNumber.startsWith(externalLinePrefix) && !dialNumber.startsWith('+') && !dialNumber.startsWith('*') && !dialNumber.startsWith('#')) {
        dialNumber = externalLinePrefix + dialNumber;
      }
    }

    // Gewählte Nummer SOFORT speichern bevor COM antwortet
    // Wir wissen noch nicht welche Leitung, daher auf selectedLine oder 0 setzen
    const selectedId = useLineStore.getState().selectedLineId ?? 0;
    setDialedNumber(selectedId, dialNumber);

    await window.swyxApi.dial(dialNumber);
    // Sofort nach dem Wählen den Leitungsstatus abfragen und UI aktualisieren
    try {
      const result = await window.swyxApi.getLines();
      const lines = Array.isArray(result)
        ? result
        : (result as { lines: unknown[] } | null)?.lines ?? [];
      if (lines.length > 0) {
        setLines(lines as import('../types/swyx').LineInfo[]);
        // Finde die aktive Leitung und setze die gewählte Nummer darauf
        const active = (lines as import('../types/swyx').LineInfo[]).find(
          (l) => l.state !== 'Inactive' && l.state !== 'Terminated' && l.state !== 'Disabled'
        );
        if (active) {
          setDialedNumber(active.id, dialNumber);
        }
      }
    } catch {
      // Fehler ignorieren — lineStateChanged-Event folgt ohnehin
    }
  }, [setLines, setDialedNumber]);

  const answer = useCallback(async (lineId: number) => {
    await window.swyxApi.answer(lineId);
    updateLine(lineId, { state: LineState.Active });
  }, [updateLine]);

  const hangup = useCallback(async (lineId: number) => {
    await window.swyxApi.hangup(lineId);
    // State wird NUR auf Inactive gesetzt — callerName/callerNumber bewusst
    // NICHT löschen, damit useCallHistoryTracker den Anruf noch erfassen kann.
    // Die nächste lineStateChanged-Nachricht vom Bridge setzt den vollen State.
    updateLine(lineId, { state: LineState.Inactive });
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
