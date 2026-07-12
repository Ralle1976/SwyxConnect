import { useState, useCallback } from 'react';
import { TeamsSyncStatus } from '../types/teams';

export interface TeamsSyncHookResult {
  syncStatus: TeamsSyncStatus;
  enable: () => void;
  disable: () => void;
  login: () => Promise<void>;
  logout: () => Promise<void>;
}

export function useTeamsSync(): TeamsSyncHookResult {
  const [syncStatus, setSyncStatus] = useState<TeamsSyncStatus>({
    enabled: false,
    connected: false,
    lastSync: null,
    error: null,
  });

  const enable = useCallback(() => {
    setSyncStatus((prev) => ({ ...prev, enabled: true }));
  }, []);

  const disable = useCallback(() => {
    setSyncStatus((prev) => ({ ...prev, enabled: false, connected: false }));
  }, []);

  const login = useCallback(async () => {
    setSyncStatus((prev) => ({ ...prev, error: null }));
    try {
      setSyncStatus((prev) => ({
        ...prev,
        connected: true,
        lastSync: Date.now(),
      }));
    } catch (err) {
      setSyncStatus((prev) => ({
        ...prev,
        connected: false,
        error: err instanceof Error ? err.message : 'Anmeldefehler',
      }));
    }
  }, []);

  const logout = useCallback(async () => {
    setSyncStatus({
      enabled: false,
      connected: false,
      lastSync: null,
      error: null,
    });
  }, []);

  return { syncStatus, enable, disable, login, logout };
}
