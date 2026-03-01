import { useCallback } from 'react';
import { usePresenceStore } from '../stores/usePresenceStore';
import { PresenceStatus, ColleaguePresence } from '../types/swyx';

export interface PresenceHookResult {
  ownStatus: PresenceStatus;
  colleagues: ColleaguePresence[];
  setPresence: (status: PresenceStatus) => Promise<void>;
  refreshPresence: () => Promise<void>;
}

export function usePresence(): PresenceHookResult {
  const ownStatus = usePresenceStore((s) => s.ownStatus);
  const colleagues = usePresenceStore((s) => s.colleagues);
  const setOwnStatus = usePresenceStore((s) => s.setOwnStatus);
  const setColleagues = usePresenceStore((s) => s.setColleagues);

  const setPresence = useCallback(
    async (status: PresenceStatus) => {
      setOwnStatus(status);
      try {
        await window.swyxApi.setPresence(status);
      } catch {
        // bridge presence update — non-critical
      }
    },
    [setOwnStatus]
  );

  const refreshPresence = useCallback(async () => {
    try {
      const data = await window.swyxApi.getPresence();
      setColleagues(data);
    } catch {
      // retain current state on error
    }
  }, [setColleagues]);

  return { ownStatus, colleagues, setPresence, refreshPresence };
}
