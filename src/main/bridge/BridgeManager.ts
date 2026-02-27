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

  private get bridgePath(): string {
    const resourcesPath = app.isPackaged
      ? process.resourcesPath
      : path.join(app.getAppPath(), 'out', 'bridge');
    return path.join(resourcesPath, 'SwyxBridge.exe');
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
      const child = spawn(this.bridgePath, [], {
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
