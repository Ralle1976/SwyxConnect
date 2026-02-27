import {
  BridgeMessage,
  BridgeRequest,
  BridgeResponse,
  BridgeEvent,
} from '../../shared/types';
import { BRIDGE_REQUEST_TIMEOUT } from '../../shared/constants';

// ─── Pending request tracking ────────────────────────────────────────────────

interface PendingRequest {
  resolve: (value: unknown) => void;
  reject: (reason: Error) => void;
  timer: ReturnType<typeof setTimeout>;
  method: string;
}

// ─── BridgeProtocol ──────────────────────────────────────────────────────────

export class BridgeProtocol {
  private nextId = 1;
  private readonly pending = new Map<number, PendingRequest>();
  private readonly requestTimeout: number;

  constructor(requestTimeout = BRIDGE_REQUEST_TIMEOUT) {
    this.requestTimeout = requestTimeout;
  }

  /**
   * Serialize a JSON-RPC 2.0 request to a newline-terminated string.
   * Returns both the assigned ID and the serialized line.
   */
  serializeRequest(
    method: string,
    params?: unknown
  ): { id: number; line: string } {
    const id = this.nextId++;
    const request: BridgeRequest = {
      jsonrpc: '2.0',
      id,
      method,
      ...(params !== undefined ? { params } : {}),
    };
    return { id, line: JSON.stringify(request) + '\n' };
  }

  /**
   * Parse a raw line from the bridge stdout.
   * Returns null if the line is invalid or not JSON-RPC 2.0.
   */
  parseMessage(line: string): BridgeMessage | null {
    const trimmed = line.trim();
    if (!trimmed) return null;
    try {
      const parsed: unknown = JSON.parse(trimmed);
      if (
        typeof parsed !== 'object' ||
        parsed === null ||
        (parsed as Record<string, unknown>)['jsonrpc'] !== '2.0'
      ) {
        return null;
      }
      return parsed as BridgeMessage;
    } catch {
      return null;
    }
  }

  /** Check whether a message is a response (has numeric id, no method). */
  isResponse(msg: BridgeMessage): msg is BridgeResponse {
    return typeof msg.id === 'number' && msg.method === undefined;
  }

  /** Check whether a message is a server-sent event (method present, no id). */
  isEvent(msg: BridgeMessage): msg is BridgeEvent {
    return typeof msg.method === 'string' && msg.id === undefined;
  }

  /**
   * Register a pending request. Returns a Promise that resolves/rejects
   * when the corresponding response arrives or the timeout fires.
   */
  createPending(id: number, method: string): Promise<unknown> {
    return new Promise<unknown>((resolve, reject) => {
      const timer = setTimeout(() => {
        this.pending.delete(id);
        reject(new Error(`Request '${method}' (id=${id}) timed out after ${this.requestTimeout}ms`));
      }, this.requestTimeout);

      this.pending.set(id, { resolve, reject, timer, method });
    });
  }

  /**
   * Dispatch an incoming response to the matching pending request.
   */
  handleResponse(response: BridgeResponse): void {
    const pending = this.pending.get(response.id);
    if (!pending) return;

    clearTimeout(pending.timer);
    this.pending.delete(response.id);

    if (response.error) {
      pending.reject(
        new Error(
          `Bridge error [${response.error.code}]: ${response.error.message}`
        )
      );
    } else {
      pending.resolve(response.result);
    }
  }

  /**
   * Reject all pending requests (used when the bridge disconnects).
   */
  rejectAll(reason: Error): void {
    for (const [id, pending] of this.pending.entries()) {
      clearTimeout(pending.timer);
      pending.reject(reason);
      this.pending.delete(id);
    }
  }

  get pendingCount(): number {
    return this.pending.size;
  }
}
