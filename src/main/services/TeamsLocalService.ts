import { EventEmitter } from 'events';
import { BridgeManager } from '../bridge/BridgeManager';
import { TeamsLocalStatus, TeamsLocalAccount } from '../../shared/types';

export class TeamsLocalService extends EventEmitter {
  private bridgeManager: BridgeManager;
  private pollTimer: ReturnType<typeof setInterval> | null = null;
  private _connected = false;

  constructor(bridgeManager: BridgeManager) {
    super();
    this.bridgeManager = bridgeManager;
  }

  async connect(): Promise<TeamsLocalStatus> {
    const result = await this.bridgeManager.sendRequest('teams.local.connect') as TeamsLocalStatus;
    this._connected = result?.connected ?? false;
    if (this._connected) {
      this.startPolling();
    }
    this.emit('stateChanged', result);
    return result;
  }

  async disconnect(): Promise<void> {
    this.stopPolling();
    await this.bridgeManager.sendRequest('teams.local.disconnect');
    this._connected = false;
    this.emit('stateChanged', { connected: false, availability: 'Unknown', activity: 'Unknown', currentUser: null, clientVersion: null });
  }

  async getStatus(): Promise<TeamsLocalStatus> {
    return await this.bridgeManager.sendRequest('teams.local.getStatus') as TeamsLocalStatus;
  }

  async getAvailability(): Promise<string> {
    const result = await this.bridgeManager.sendRequest('teams.local.getAvailability') as { availability: string };
    return result?.availability ?? 'Unknown';
  }

  async setAvailability(availability: string): Promise<boolean> {
    const result = await this.bridgeManager.sendRequest('teams.local.setAvailability', { availability }) as { ok: boolean };
    return result?.ok ?? false;
  }

  async makeCall(phoneNumber: string): Promise<boolean> {
    const result = await this.bridgeManager.sendRequest('teams.local.makeCall', { phoneNumber }) as { ok: boolean };
    return result?.ok ?? false;
  }

  async getAccounts(): Promise<TeamsLocalAccount[]> {
    const result = await this.bridgeManager.sendRequest('teams.local.getAccounts') as { accounts: TeamsLocalAccount[] };
    return result?.accounts ?? [];
  }

  get isConnected(): boolean {
    return this._connected;
  }

  private startPolling(): void {
    this.stopPolling();
    this.pollTimer = setInterval(async () => {
      try {
        const status = await this.getStatus();
        this.emit('presenceChanged', status);
      } catch {
        // Bridge might not be ready
      }
    }, 15000);
  }

  private stopPolling(): void {
    if (this.pollTimer) {
      clearInterval(this.pollTimer);
      this.pollTimer = null;
    }
  }

  stop(): void {
    this.stopPolling();
  }
}
