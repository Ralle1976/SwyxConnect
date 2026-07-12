import { EventEmitter } from 'events';
import { HubConnection, HubConnectionBuilder, HubConnectionState } from '@microsoft/signalr';

/**
 * ComSocket Service - Optional SignalR connection to Swyx ComSocket plugin
 * Provides real-time events as alternative to COM polling
 * 
 * ComSocket läuft als ASP.NET Core Plugin in CLMgr.exe
 * SignalR Hub endpoint ist typisch: http://localhost:{port}/clmgrhub
 * Port muss konfiguriert werden (nicht standardmäßig aktiviert)
 */

export interface ComSocketLineInfo {
  id: number;
  label: string;
  state: string;
  isDefaultLine: boolean;
  outgoingExtension: string;
}

export interface ComSocketLineDetails {
  id: number;
  peerName: string;
  peerNumber: string;
  callerName: string;
  callerNumber: string;
  callId: number;
  state: string;
  isOutgoingCall: boolean;
  connectionStartTime: string;
  encryption: boolean;
  hdAudio: boolean;
}

export interface ComSocketEvent {
  type: string;
  data: unknown;
  timestamp: number;
}

export class ComSocketService extends EventEmitter {
  private connection: HubConnection | null = null;
  private port: number = 5000; // Default ComSocket port (muss konfiguriert werden)
  private isConnected: boolean = false;
  private reconnectAttempts: number = 0;
  private maxReconnectAttempts: number = 5;
  private reconnectDelay: number = 3000;

  constructor(port?: number) {
    super();
    if (port) {
      this.port = port;
    }
  }

  /**
   * Verbindet sich mit ComSocket SignalR Hub
   */
  async connect(): Promise<boolean> {
    if (this.connection?.state === HubConnectionState.Connected) {
      return true;
    }

    try {
      this.connection = new HubConnectionBuilder()
        .withUrl(`http://localhost:${this.port}/clmgrhub`, {
          transport: undefined, // Auto-select (WebSockets preferred)
        })
        .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
        .build();

      // Register event handlers
      this.connection.on('NotifyLineStateChanged', (data) => {
        this.emit('lineStateChanged', data);
      });

      this.connection.on('NotifyLineDetailsChanged', (data) => {
        this.emit('lineDetailsChanged', data);
      });

      this.connection.on('NotifyCallRecordingAdded', (data) => {
        this.emit('callRecordingAdded', data);
      });

      this.connection.on('NotifyCallRecordingChanged', (data) => {
        this.emit('callRecordingChanged', data);
      });

      this.connection.on('NotifyCallRecordingRemoved', (data) => {
        this.emit('callRecordingRemoved', data);
      });

      this.connection.on('NotifyCtiStateChanged', (data) => {
        this.emit('ctiStateChanged', data);
      });

      this.connection.on('NotifyUserDataChanged', (data) => {
        this.emit('userDataChanged', data);
      });

      this.connection.on('SwyxServerConnectionStateChanged', (data) => {
        this.emit('serverConnectionStateChanged', data);
      });

      this.connection.on('UnreadChatMessageCountChanged', (data) => {
        this.emit('chatMessageCountChanged', data);
      });

      // Connection state handlers
      this.connection.onclose((error) => {
        this.isConnected = false;
        this.emit('disconnected', { error: error?.message });
        this.attemptReconnect();
      });

      this.connection.onreconnecting(() => {
        this.emit('reconnecting');
      });

      this.connection.onreconnected(() => {
        this.isConnected = true;
        this.reconnectAttempts = 0;
        this.emit('reconnected');
      });

      await this.connection.start();
      this.isConnected = true;
      this.reconnectAttempts = 0;

      console.log(`[ComSocket] Connected to localhost:${this.port}`);
      this.emit('connected');

      return true;
    } catch (error) {
      console.error(`[ComSocket] Connection failed: ${error}`);
      this.emit('error', { message: (error as Error).message });
      return false;
    }
  }

  /**
   * Trennt die Verbindung
   */
  async disconnect(): Promise<void> {
    if (this.connection) {
      await this.connection.stop();
      this.connection = null;
      this.isConnected = false;
      console.log('[ComSocket] Disconnected');
      this.emit('disconnected');
    }
  }

  /**
   * Versucht automatisch wiederzuverbinden
   */
  private async attemptReconnect(): Promise<void> {
    if (this.reconnectAttempts >= this.maxReconnectAttempts) {
      console.error('[ComSocket] Max reconnect attempts reached');
      this.emit('error', { message: 'Max reconnect attempts reached' });
      return;
    }

    this.reconnectAttempts++;
    const delay = this.reconnectDelay * this.reconnectAttempts;

    console.log(`[ComSocket] Reconnect attempt ${this.reconnectAttempts}/${this.maxReconnectAttempts} in ${delay}ms`);

    setTimeout(async () => {
      const success = await this.connect();
      if (!success) {
        this.attemptReconnect();
      }
    }, delay);
  }

  /**
   * Ruft aktuelle Leitungs-Details ab
   */
  async getLineDetails(lineId: number): Promise<ComSocketLineDetails | null> {
    if (!this.isConnected || !this.connection) {
      return null;
    }

    try {
      const result = await this.connection.invoke('GetLineDetails', lineId);
      return result as ComSocketLineDetails;
    } catch (error) {
      console.error(`[ComSocket] Failed to get line details: ${error}`);
      return null;
    }
  }

  /**
   * Ruft Call Recordings ab
   */
  async getCallRecordings(): Promise<unknown[]> {
    if (!this.isConnected || !this.connection) {
      return [];
    }

    try {
      const result = await this.connection.invoke('GetCallRecordings');
      return result as unknown[];
    } catch (error) {
      console.error(`[ComSocket] Failed to get recordings: ${error}`);
      return [];
    }
  }

  /**
   * Status der Verbindung
   */
  getStatus(): { connected: boolean; port: number; reconnectAttempts: number } {
    return {
      connected: this.isConnected,
      port: this.port,
      reconnectAttempts: this.reconnectAttempts,
    };
  }
}
