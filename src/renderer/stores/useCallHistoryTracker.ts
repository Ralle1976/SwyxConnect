import { useEffect, useRef } from 'react';
import { useLineStore } from './useLineStore';
import { useHistoryStore } from './useHistoryStore';
import { LineState } from '../types/swyx';

export function useCallHistoryTracker() {
    const lines = useLineStore((s) => s.lines);
    const addEntry = useHistoryStore((s) => s.addEntry);

    const previousLines = useRef(lines);
    const callMetadata = useRef(new Map<number, { direction: 'inbound' | 'outbound', startTime: number }>());

    useEffect(() => {
        lines.forEach((currentLine) => {
            const prevLine = previousLines.current.find((l) => l.id === currentLine.id);
            if (!prevLine) return;

            const metadata = callMetadata.current.get(currentLine.id);

            // Track incoming call start
            if (prevLine.state === LineState.Inactive && currentLine.state === LineState.Ringing) {
                callMetadata.current.set(currentLine.id, { direction: 'inbound', startTime: Date.now() });
            }

            // Track outgoing call start
            if (
                prevLine.state === LineState.Inactive &&
                (currentLine.state === LineState.Dialing || currentLine.state === LineState.Alerting || currentLine.state === LineState.HookOffInternal || currentLine.state === LineState.HookOffExternal)
            ) {
                callMetadata.current.set(currentLine.id, { direction: 'outbound', startTime: Date.now() });
            }

            // Track call end
            const wasActive = prevLine.state !== LineState.Inactive && prevLine.state !== LineState.Terminated;
            const isNowInactive = currentLine.state === LineState.Inactive || currentLine.state === LineState.Terminated;

            if (wasActive && isNowInactive && metadata) {
                // Determine missed
                // If it was ringing and became inactive without becoming active
                const wasAnswered = currentLine.duration !== undefined && currentLine.duration > 0;
                const isMissed = metadata.direction === 'inbound' && !wasAnswered && prevLine.state === LineState.Ringing;

                const duration = currentLine.duration ?? prevLine.duration ?? 0;

                // Only log if we have a recognizable caller (or we tried dialing at least something)
                const callerName = currentLine.callerName || prevLine.callerName || '';
                const callerNumber = currentLine.callerNumber || prevLine.callerNumber || 'Unbekannt';

                if (callerNumber !== 'Unbekannt' || callerName !== '') {
                    addEntry({
                        id: crypto.randomUUID(),
                        callerName,
                        callerNumber,
                        direction: isMissed ? 'missed' : metadata.direction,
                        timestamp: metadata.startTime,
                        duration,
                    });
                }

                callMetadata.current.delete(currentLine.id);
            }
        });

        previousLines.current = lines;
    }, [lines, addEntry]);
}
