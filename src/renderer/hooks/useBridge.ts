import { useState, useEffect } from 'react';
import { BridgeState } from '../types/swyx';

export interface BridgeHookResult {
  bridgeState: BridgeState;
  isConnected: boolean;
  isFailed: boolean;
  isStarting: boolean;
  isRestarting: boolean;
}

export function useBridge(): BridgeHookResult {
  const [bridgeState, setBridgeState] = useState<BridgeState>(
    BridgeState.Disconnected
  );

  useEffect(() => {
    window.swyxApi
      .getBridgeState()
      .then(setBridgeState)
      .catch(() => setBridgeState(BridgeState.Disconnected));

    const unsubscribe = window.swyxApi.onBridgeStateChanged(setBridgeState);
    return unsubscribe;
  }, []);

  return {
    bridgeState,
    isConnected: bridgeState === BridgeState.Connected,
    isFailed: bridgeState === BridgeState.Failed,
    isStarting: bridgeState === BridgeState.Starting,
    isRestarting: bridgeState === BridgeState.Restarting,
  };
}
