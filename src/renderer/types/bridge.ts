import { BridgeState } from '../../shared/types';

export interface BridgeStatusInfo {
  state: BridgeState;
  isConnected: boolean;
  isFailed: boolean;
  isRestarting: boolean;
  lastConnectedAt: number | null;
  restartCount: number;
}

export interface BridgeEventPayload<T = unknown> {
  method: string;
  params: T;
  receivedAt: number;
}
