import { PresenceStatus } from '../../shared/types';

export interface TeamsPresence {
  availability: string;
  activity: string;
  mappedStatus: PresenceStatus;
  lastSyncedAt: number | null;
}

export interface TeamsSyncStatus {
  enabled: boolean;
  connected: boolean;
  lastSync: number | null;
  error: string | null;
}
