import { BRIDGE_MAX_RESTARTS, BRIDGE_RESTART_WINDOW } from '../../shared/constants';

// ─── ReconnectPolicy ─────────────────────────────────────────────────────────

/**
 * Tracks bridge restart attempts within a rolling time window.
 * Once BRIDGE_MAX_RESTARTS restarts occur within BRIDGE_RESTART_WINDOW ms,
 * canRestart() returns false and the bridge is marked as failed.
 */
export class ReconnectPolicy {
  private restartTimestamps: number[] = [];
  private readonly maxRestarts: number;
  private readonly windowMs: number;

  constructor(maxRestarts = BRIDGE_MAX_RESTARTS, windowMs = BRIDGE_RESTART_WINDOW) {
    this.maxRestarts = maxRestarts;
    this.windowMs = windowMs;
  }

  /**
   * Returns true if we are still within the allowed restart budget.
   */
  canRestart(): boolean {
    this.pruneOld();
    return this.restartTimestamps.length < this.maxRestarts;
  }

  /**
   * Records a restart attempt at the current time.
   */
  recordRestart(): void {
    this.restartTimestamps.push(Date.now());
    this.pruneOld();
  }

  /**
   * Resets the restart counter (e.g. after a stable connection period).
   */
  reset(): void {
    this.restartTimestamps = [];
  }

  /** How many restarts have been recorded within the current window. */
  get recentRestartCount(): number {
    this.pruneOld();
    return this.restartTimestamps.length;
  }

  /** Milliseconds until the oldest restart in the window expires. */
  get nextWindowReset(): number {
    this.pruneOld();
    if (this.restartTimestamps.length === 0) return 0;
    const oldest = this.restartTimestamps[0];
    return Math.max(0, oldest + this.windowMs - Date.now());
  }

  private pruneOld(): void {
    const cutoff = Date.now() - this.windowMs;
    this.restartTimestamps = this.restartTimestamps.filter((t) => t > cutoff);
  }
}
