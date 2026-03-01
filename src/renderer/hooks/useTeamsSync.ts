import { useState, useCallback, useEffect } from 'react';
import { TeamsSyncStatus } from '../types/teams';
import { TeamsAccountInfo } from '../types/swyx';
import { useSettingsStore } from '../stores/useSettingsStore';

export interface TeamsDeviceCodeInfo {
  userCode: string;
  verificationUri: string;
  message: string;
}

export interface TeamsSyncHookResult {
  syncStatus: TeamsSyncStatus;
  accounts: TeamsAccountInfo[];
  deviceCode: TeamsDeviceCodeInfo | null;
  enable: () => Promise<void>;
  disable: () => Promise<void>;
  login: () => Promise<void>;
  logout: (accountId: string) => Promise<void>;
}

export function useTeamsSync(): TeamsSyncHookResult {
  const { teamsEnabled, teamsAccounts, setTeamsEnabled, setTeamsAccounts } = useSettingsStore();

  const [syncStatus, setSyncStatus] = useState<TeamsSyncStatus>({
    enabled: teamsEnabled,
    connected: teamsAccounts.length > 0,
    lastSync: null,
    error: null,
  });

  const [deviceCode, setDeviceCode] = useState<TeamsDeviceCodeInfo | null>(null);

  // Keep syncStatus.enabled in sync with store
  useEffect(() => {
    setSyncStatus((prev) => ({
      ...prev,
      enabled: teamsEnabled,
      connected: teamsAccounts.length > 0,
    }));
  }, [teamsEnabled, teamsAccounts]);

  // Subscribe to Teams events from main process
  useEffect(() => {
    if (!window.swyxApi) return;

    const unsubDeviceCode = window.swyxApi.onTeamsDeviceCode((data) => {
      setDeviceCode(data);
    });

    const unsubAccountAdded = window.swyxApi.onTeamsAccountAdded((account) => {
      // Auto-dismiss device code flow
      setDeviceCode(null);
      setSyncStatus((prev) => ({
        ...prev,
        connected: true,
        lastSync: Date.now(),
        error: null,
      }));
      // Refresh accounts list from main process
      void window.swyxApi.teamsGetAccounts().then((rawAccounts) => {
        const accounts = (rawAccounts as TeamsAccountInfo[]).map((a) => ({
          id: a.id,
          displayName: a.displayName,
          email: a.email,
          tenantId: a.tenantId,
        }));
        setTeamsAccounts(accounts);
      });
      void account; // suppress unused warning
    });

    const unsubAccountRemoved = window.swyxApi.onTeamsAccountRemoved((_accountId) => {
      // Refresh accounts list
      void window.swyxApi.teamsGetAccounts().then((rawAccounts) => {
        const accounts = (rawAccounts as TeamsAccountInfo[]).map((a) => ({
          id: a.id,
          displayName: a.displayName,
          email: a.email,
          tenantId: a.tenantId,
        }));
        setTeamsAccounts(accounts);
        setSyncStatus((prev) => ({
          ...prev,
          connected: accounts.length > 0,
        }));
      });
    });

    const unsubPresenceChanged = window.swyxApi.onTeamsPresenceChanged((_data) => {
      setSyncStatus((prev) => ({ ...prev, lastSync: Date.now() }));
    });

    const unsubError = window.swyxApi.onTeamsError((error) => {
      setSyncStatus((prev) => ({ ...prev, error: error.message }));
      // Also dismiss device code on error
      setDeviceCode(null);
    });

    // Initial accounts load
    void window.swyxApi.teamsGetAccounts().then((rawAccounts) => {
      const accounts = (rawAccounts as TeamsAccountInfo[]).map((a) => ({
        id: a.id,
        displayName: a.displayName,
        email: a.email,
        tenantId: a.tenantId,
      }));
      setTeamsAccounts(accounts);
    });

    return () => {
      unsubDeviceCode();
      unsubAccountAdded();
      unsubAccountRemoved();
      unsubPresenceChanged();
      unsubError();
    };
  }, [setTeamsAccounts]);

  const enable = useCallback(async () => {
    setSyncStatus((prev) => ({ ...prev, enabled: true, error: null }));
    setTeamsEnabled(true);
    if (window.swyxApi?.teamsSetEnabled) {
      await window.swyxApi.teamsSetEnabled(true);
    }
  }, [setTeamsEnabled]);

  const disable = useCallback(async () => {
    setSyncStatus((prev) => ({ ...prev, enabled: false, connected: false }));
    setTeamsEnabled(false);
    if (window.swyxApi?.teamsSetEnabled) {
      await window.swyxApi.teamsSetEnabled(false);
    }
  }, [setTeamsEnabled]);

  const login = useCallback(async () => {
    setSyncStatus((prev) => ({ ...prev, error: null }));
    try {
      await window.swyxApi.teamsAddAccount();
      // accountAdded event will handle the rest
    } catch (err) {
      setSyncStatus((prev) => ({
        ...prev,
        connected: false,
        error: err instanceof Error ? err.message : 'Anmeldefehler',
      }));
      setDeviceCode(null);
    }
  }, []);

  const logout = useCallback(async (accountId: string) => {
    try {
      await window.swyxApi.teamsRemoveAccount(accountId);
      // accountRemoved event will handle the rest
    } catch (err) {
      setSyncStatus((prev) => ({
        ...prev,
        error: err instanceof Error ? err.message : 'Abmeldefehler',
      }));
    }
  }, []);

  return {
    syncStatus,
    accounts: teamsAccounts,
    deviceCode,
    enable,
    disable,
    login,
    logout,
  };
}
