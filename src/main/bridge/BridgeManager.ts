import { EventEmitter } from 'events';
import { ChildProcess, spawn } from 'child_process';
import * as readline from 'readline';
import * as path from 'path';
import * as fs from 'fs';
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
  private _resolvedBridgePath: string | null = null;

  private get bridgePath(): string {
    if (this._resolvedBridgePath) return this._resolvedBridgePath;

    if (app.isPackaged) {
      this._resolvedBridgePath = path.join(process.resourcesPath, 'bridge', 'SwyxBridge.exe');
      return this._resolvedBridgePath;
    }

    // Dev mode: app.getAppPath() = .../out/main, bridge is at .../out/bridge/
    const appDir = app.getAppPath();
    const outDir = path.dirname(appDir);
    const bridgeDir = path.join(outDir, 'bridge');
    const bridgeExe = path.join(bridgeDir, 'SwyxBridge.exe');

    // WSL2 fix: .NET loads stale/cached assemblies from UNC paths (\\wsl.localhost\...)
    // Copy bridge files to a Windows-native temp directory for reliable loading
    try {
      const winTempDir = '/mnt/c/temp/SwyxBridge';
      fs.mkdirSync(winTempDir, { recursive: true });
      for (const file of fs.readdirSync(bridgeDir)) {
        fs.copyFileSync(path.join(bridgeDir, file), path.join(winTempDir, file));
      }
      this._resolvedBridgePath = path.join(winTempDir, 'SwyxBridge.exe');
      console.log(`[Bridge] Copied to Windows path: ${this._resolvedBridgePath}`);
      return this._resolvedBridgePath;
    } catch {
      // Fallback 1: Check Windows temp directory (pre-deployed bridge)
      const winFallback = '/mnt/c/temp/SwyxBridge/SwyxBridge.exe';
      if (fs.existsSync(winFallback)) {
        this._resolvedBridgePath = winFallback;
        console.log(`[Bridge] Using Windows fallback: ${this._resolvedBridgePath}`);
        return this._resolvedBridgePath;
      }
      // Fallback 2: use original WSL path (may cause stale assembly issues)
      console.warn('[Bridge] Failed to copy to Windows path, using WSL path');
      this._resolvedBridgePath = bridgeExe;
      return this._resolvedBridgePath;
    }
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
        if (!text) return;
        // C# Bridge schreibt Logs auf stderr — nach Level unterscheiden
        if (text.includes(' ERR]') || text.includes(' FTL]')) {
          this.emit('error', new Error(text));
        } else {
          this.emit('log', text);
        }
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
