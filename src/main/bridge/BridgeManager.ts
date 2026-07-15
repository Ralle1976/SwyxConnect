import { EventEmitter } from 'events';
import { ChildProcess, spawn } from 'child_process';
import * as readline from 'readline';
import * as path from 'path';
import { app } from 'electron';
import {
  BridgeState,
  BridgeMessage,
  BridgeResponse,
  BridgeEvent,
} from '../../shared/types';
import {
  BRIDGE_HEARTBEAT_INTERVAL,
  BRIDGE_HEARTBEAT_TIMEOUT,
} from '../../shared/constants';
import { BridgeProtocol } from './BridgeProtocol';
import { ReconnectPolicy } from './BridgeReconnect';
import { getBridgeCredentials } from './bridge-env';

export interface BridgeManagerEvents {
  stateChanged: (state: BridgeState) => void;
  event: (event: BridgeEvent) => void;
  error: (err: Error) => void;
}

export declare interface BridgeManager {
  on<K extends keyof BridgeManagerEvents>(
    event: K,
    listener: BridgeManagerEvents[K]
  ): this;
  emit<K extends keyof BridgeManagerEvents>(
    event: K,
    ...args: Parameters<BridgeManagerEvents[K]>
  ): boolean;
}

export class BridgeManager extends EventEmitter {
  private process: ChildProcess | null = null;
  private readonly protocol = new BridgeProtocol();
  private readonly reconnect = new ReconnectPolicy();

  private state: BridgeState = BridgeState.Disconnected;
  private heartbeatTimer: ReturnType<typeof setInterval> | null = null;
  private heartbeatTimeoutTimer: ReturnType<typeof setTimeout> | null = null;
  private lastHeartbeat = 0;
  private isShuttingDown = false;

  /**
   * Bridge path: SwyxMessenger.exe (UseAppHost=true → native apphost).
   * The process name "SwyxMessenger.exe" matches the ComSocket auth whitelist.
   */
  private get bridgeExe(): string {
    if (app.isPackaged) {
      return path.join(process.resourcesPath, 'bridge', 'SwyxMessenger.exe');
    }
    // Dev mode: app.getAppPath() returns .../out/main/ (where index.js lives).
    // Go up one level to reach .../out/ then into bridge/.
    const appPath = app.getAppPath();
    return path.join(appPath, '..', 'bridge', 'SwyxMessenger.exe');
  }

  start(): void {
    if (this.state === BridgeState.Starting || this.state === BridgeState.Connected) return;
    this.isShuttingDown = false;
    this.setState(BridgeState.Starting);
    this.spawnProcess();
  }

  stop(): void {
    this.isShuttingDown = true;
    this.clearHeartbeat();
    this.killProcess();
    this.setState(BridgeState.Disconnected);
  }

  async sendRequest(method: string, params?: unknown): Promise<unknown> {
    if (this.state !== BridgeState.Connected || !this.process?.stdin) {
      throw new Error(`Bridge not connected (state: ${this.state})`);
    }

    const { id, line } = this.protocol.serializeRequest(method, params);
    const pending = this.protocol.createPending(id, method);

    this.process.stdin.write(line, 'utf8');
    return pending;
  }

  getState(): BridgeState {
    return this.state;
  }

  private spawnProcess(): void {
    try {
      // v1.6.0: Load credentials from .env for standalone RC tunnel login.
      // If .env found → standalone mode (no SwyxIt! needed).
      // If .env missing → Auto-Attach mode (attaches to SwyxIt! session).
      const creds = getBridgeCredentials();
      const args: string[] = [];
      if (creds && creds.username && creds.password) {
        args.push('--server', creds.server);
        args.push('--public-server', creds.publicServer);
        args.push('--user', creds.username);
        args.push('--password', creds.password);
        args.push('--auth-mode', String(creds.authMode));
        if (creds.backupServer) args.push('--backup-server', creds.backupServer);
      }

      const child = spawn(this.bridgeExe, args, {
        stdio: ['pipe', 'pipe', 'pipe'],
        windowsHide: true,
      });

      this.process = child;

      const rl = readline.createInterface({
        input: child.stdout!,
        crlfDelay: Infinity,
      });

      rl.on('line', (line) => this.handleLine(line));

      child.stderr?.on('data', (chunk: Buffer) => {
        const text = chunk.toString('utf8').trim();
        if (text) this.emit('error', new Error(`Bridge stderr: ${text}`));
      });

      child.on('spawn', () => {
        this.setState(BridgeState.Connected);
        this.reconnect.reset();
        this.startHeartbeat();
      });

      child.on('error', (err) => {
        this.emit('error', err);
        this.handleProcessDeath();
      });

      child.on('exit', (code) => {
        if (!this.isShuttingDown) {
          this.emit('error', new Error(`Bridge exited with code ${code}`));
          this.handleProcessDeath();
        }
      });
    } catch (err) {
      this.emit('error', err instanceof Error ? err : new Error(String(err)));
      this.handleProcessDeath();
    }
  }

  private handleLine(line: string): void {
    this.updateHeartbeatTimeout();

    const message: BridgeMessage | null = this.protocol.parseMessage(line);
    if (!message) return;

    if (this.protocol.isResponse(message)) {
      this.protocol.handleResponse(message as BridgeResponse);
    } else if (this.protocol.isEvent(message)) {
      if ((message as BridgeEvent).method === 'heartbeat') {
        this.lastHeartbeat = Date.now();
      } else {
        this.emit('event', message as BridgeEvent);
      }
    }
  }

  private startHeartbeat(): void {
    this.lastHeartbeat = Date.now();

    this.heartbeatTimer = setInterval(() => {
      if (this.state !== BridgeState.Connected) return;

      const elapsed = Date.now() - this.lastHeartbeat;
      if (elapsed > BRIDGE_HEARTBEAT_TIMEOUT) {
        this.emit('error', new Error(`Heartbeat timeout (${elapsed}ms)`));
        this.handleProcessDeath();
      }
    }, BRIDGE_HEARTBEAT_INTERVAL);
  }

  private updateHeartbeatTimeout(): void {
    this.lastHeartbeat = Date.now();
    if (this.heartbeatTimeoutTimer) {
      clearTimeout(this.heartbeatTimeoutTimer);
    }
    this.heartbeatTimeoutTimer = setTimeout(() => {
      if (this.state === BridgeState.Connected) {
        this.emit('error', new Error('Heartbeat timeout'));
        this.handleProcessDeath();
      }
    }, BRIDGE_HEARTBEAT_TIMEOUT);
  }

  private clearHeartbeat(): void {
    if (this.heartbeatTimer) {
      clearInterval(this.heartbeatTimer);
      this.heartbeatTimer = null;
    }
    if (this.heartbeatTimeoutTimer) {
      clearTimeout(this.heartbeatTimeoutTimer);
      this.heartbeatTimeoutTimer = null;
    }
  }

  private handleProcessDeath(): void {
    this.clearHeartbeat();
    this.killProcess();
    this.protocol.rejectAll(new Error('Bridge disconnected'));

    if (this.isShuttingDown) {
      this.setState(BridgeState.Disconnected);
      return;
    }

    if (this.reconnect.canRestart()) {
      this.reconnect.recordRestart();
      this.setState(BridgeState.Restarting);
      setTimeout(() => {
        if (!this.isShuttingDown) this.spawnProcess();
      }, 2000);
    } else {
      this.setState(BridgeState.Failed);
    }
  }

  private killProcess(): void {
    if (!this.process) return;

    const pid = this.process.pid;
    this.process.removeAllListeners();
    this.process.stdout?.removeAllListeners();
    this.process.stderr?.removeAllListeners();

    if (pid) {
      if (process.platform === 'win32') {
        spawn('taskkill', ['/PID', String(pid), '/F', '/T'], {
          windowsHide: true,
        }).unref();
      } else {
        try {
          process.kill(pid, 'SIGTERM');
        } catch {
          // process already gone
        }
      }
    }

    this.process = null;
  }

  private setState(next: BridgeState): void {
    if (this.state === next) return;
    this.state = next;
    this.emit('stateChanged', next);
  }
}
