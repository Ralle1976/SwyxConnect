import { useEffect, useRef } from 'react';
import { useLineStore } from './useLineStore';
import { useHistoryStore } from './useHistoryStore';
import { LineInfo, LineState } from '../types/swyx';

/** Trackt Leitungsänderungen und erstellt automatisch History-Einträge. */
export function useCallHistoryTracker() {
    const lines = useLineStore((s) => s.lines);
    const addEntry = useHistoryStore((s) => s.addEntry);

    const previousLines = useRef<LineInfo[]>([]);
    const callMetadata = useRef(new Map<number, { direction: 'inbound' | 'outbound', startTime: number, callerName: string, callerNumber: string }>());

    useEffect(() => {
        lines.forEach((currentLine) => {
            const prevLine = previousLines.current.find((l) => l.id === currentLine.id);
            const metadata = callMetadata.current.get(currentLine.id);
            const prevState = prevLine?.state ?? LineState.Inactive;

            // === Neuen Anruf erkennen (Inactive/neu → Dialing/Ringing/HookOff) ===
            if (
                !metadata &&
                (prevState === LineState.Inactive || !prevLine) &&
                currentLine.state !== LineState.Inactive &&
                currentLine.state !== LineState.Terminated &&
                currentLine.state !== LineState.Disabled
            ) {
                const isInbound = currentLine.state === LineState.Ringing || currentLine.state === LineState.Knocking;
                callMetadata.current.set(currentLine.id, {
                    direction: isInbound ? 'inbound' : 'outbound',
                    startTime: Date.now(),
                    callerName: currentLine.callerName ?? '',
                    callerNumber: currentLine.callerNumber ?? '',
                });
            }

            // === Caller-Info updaten während des Anrufs (z.B. wenn Name später kommt) ===
            if (metadata) {
                if (currentLine.callerName && !metadata.callerName)
                    metadata.callerName = currentLine.callerName;
                if (currentLine.callerNumber && !metadata.callerNumber)
                    metadata.callerNumber = currentLine.callerNumber;
            }

            // === Anruf-Ende erkennen ===
            const wasActive = prevState !== LineState.Inactive && prevState !== LineState.Terminated;
            const isNowInactive = currentLine.state === LineState.Inactive || currentLine.state === LineState.Terminated;

            if (wasActive && isNowInactive && metadata) {
                const duration = currentLine.duration ?? prevLine?.duration ?? 0;
                const wasAnswered = duration > 0 || prevState === LineState.Active;
                const isMissed = metadata.direction === 'inbound' && !wasAnswered;

                const callerName = metadata.callerName || currentLine.callerName || prevLine?.callerName || '';
                const callerNumber = metadata.callerNumber || currentLine.callerNumber || prevLine?.callerNumber || '';

                if (callerNumber || callerName) {
                    addEntry({
                        id: crypto.randomUUID(),
                        callerName,
                        callerNumber: callerNumber || 'Unbekannt',
                        direction: isMissed ? 'missed' : metadata.direction,
                        timestamp: metadata.startTime,
                        duration,
                    });
                }

                callMetadata.current.delete(currentLine.id);
            }

            // === Leitung verschwunden (war aktiv, ist jetzt nicht mehr in lines) ===
            // Wird unten nach der Schleife behandelt
        });

        // Prüfe ob Leitungen aus previousLines verschwunden sind (Anruf beendet)
        previousLines.current.forEach((prevLine) => {
            const stillExists = lines.find((l) => l.id === prevLine.id);
            if (!stillExists) {
                const metadata = callMetadata.current.get(prevLine.id);
                if (metadata && prevLine.state !== LineState.Inactive) {
                    const callerName = metadata.callerName || prevLine.callerName || '';
                    const callerNumber = metadata.callerNumber || prevLine.callerNumber || '';
                    if (callerNumber || callerName) {
                        addEntry({
                            id: crypto.randomUUID(),
                            callerName,
                            callerNumber: callerNumber || 'Unbekannt',
                            direction: metadata.direction,
                            timestamp: metadata.startTime,
                            duration: prevLine.duration ?? 0,
                        });
                    }
                    callMetadata.current.delete(prevLine.id);
                }
            }
        });

        previousLines.current = lines;
    }, [lines, addEntry]);
}
