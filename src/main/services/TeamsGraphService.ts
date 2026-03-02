import { EventEmitter } from 'events';
import * as fs from 'fs';
import * as path from 'path';
import {
  PublicClientApplication,
  Configuration,
  InteractionRequiredAuthError,
  AccountInfo,
  ICachePlugin,
  TokenCacheContext,
  AuthenticationResult,
} from '@azure/msal-node';
import { Client } from '@microsoft/microsoft-graph-client';
import { app, BrowserWindow } from 'electron';

// ─── Constants ─────────────────────────────────────────────────────────────

const TEAMS_CLIENT_ID = 'd3590ed6-52b3-4102-aeff-aad2292ab01c'; // Microsoft Office Desktop (public, well-known)
const TEAMS_AUTHORITY = 'https://login.microsoftonline.com/common';
const TEAMS_SCOPES = ['Presence.Read', 'User.Read'];
const DEFAULT_POLL_INTERVAL_MS = 15000;

// ─── Presence Types ────────────────────────────────────────────────────────

export interface PresenceData {
  availability: string;
  activity: string;
}

type PresenceAvailability =
  | 'Available'
  | 'AvailableIdle'
  | 'Busy'
  | 'BusyIdle'
  | 'DoNotDisturb'
  | 'BeRightBack'
  | 'Away'
  | 'Offline'
  | 'PresenceUnknown';

interface GraphPresenceResponse {
  availability: PresenceAvailability;
  activity: string;
}

// ─── Disk Cache Plugin ─────────────────────────────────────────────────────

function createCachePlugin(cacheFilePath: string): ICachePlugin {
  return {
    beforeCacheAccess(tokenCacheContext: TokenCacheContext): Promise<void> {
      return new Promise((resolve) => {
        if (fs.existsSync(cacheFilePath)) {
          try {
            const cacheData = fs.readFileSync(cacheFilePath, 'utf-8');
            tokenCacheContext.tokenCache.deserialize(cacheData);
          } catch (err) {
            console.error('[TeamsGraphService] Failed to read token cache:', err);
          }
        }
        resolve();
      });
    },

    afterCacheAccess(tokenCacheContext: TokenCacheContext): Promise<void> {
      return new Promise((resolve) => {
        if (tokenCacheContext.cacheHasChanged) {
          try {
            const serialized = tokenCacheContext.tokenCache.serialize();
            fs.writeFileSync(cacheFilePath, serialized, 'utf-8');
          } catch (err) {
            console.error('[TeamsGraphService] Failed to write token cache:', err);
          }
        }
        resolve();
      });
    },
  };
}

// ─── TeamsGraphService ────────────────────────────────────────────────────

export class TeamsGraphService extends EventEmitter {
  private msalApp: PublicClientApplication | null = null;
  private account: AccountInfo | null = null;
  private pollTimer: ReturnType<typeof setInterval> | null = null;
  private cacheFilePath: string | null = null;
  private graphClient: Client | null = null;

  constructor() {
    super();
  }

  // ── Lifecycle ─────────────────────────────────────────────────────────────

  async initialize(): Promise<void> {
    try {
      this.cacheFilePath = path.join(app.getPath('userData'), 'teams-token-cache.json');

      const cachePlugin = createCachePlugin(this.cacheFilePath);

      const msalConfig: Configuration = {
        auth: {
          clientId: TEAMS_CLIENT_ID,
          authority: TEAMS_AUTHORITY,
        },
        cache: {
          cachePlugin,
        },
      };

      this.msalApp = new PublicClientApplication(msalConfig);

      // Attempt to load existing cached accounts
      const tokenCache = this.msalApp.getTokenCache();
      const accounts = await tokenCache.getAllAccounts();
      if (accounts.length > 0) {
        this.account = accounts[0];
        this.buildGraphClient();
        console.log('[TeamsGraphService] Restored account from cache:', this.account.username);
      }
    } catch (err) {
      console.error('[TeamsGraphService] initialize() failed:', err);
      this.emit('error', { message: String(err) });
    }
  }

  stop(): void {
    this.stopPolling();
  }

  // ── Auth ──────────────────────────────────────────────────────────────────

  async login(): Promise<{ ok: boolean; userName?: string; error?: string }> {
    if (!this.msalApp) {
      return { ok: false, error: 'Service not initialized. Call initialize() first.' };
    }

    try {
      // 1. Try silent acquisition first if we have a cached account
      if (this.account) {
        try {
          const silentResult = await this.msalApp.acquireTokenSilent({
            scopes: TEAMS_SCOPES,
            account: this.account,
          });

          if (silentResult) {
            this.account = silentResult.account ?? this.account;
            this.buildGraphClient();
            const userName = this.account.username;
            this.emit('stateChanged', { loggedIn: true, userName });
            return { ok: true, userName };
          }
        } catch (silentErr) {
          if (!(silentErr instanceof InteractionRequiredAuthError)) {
            throw silentErr;
          }
          // Fall through to interactive flow
          console.log('[TeamsGraphService] Silent token acquisition failed, switching to interactive.');
        }
      }

      // 2. Interactive flow via BrowserWindow popup
      const result = await this.acquireTokenInteractive();

      if (!result || !result.account) {
        return { ok: false, error: 'Interactive login did not return an account.' };
      }

      this.account = result.account;
      this.buildGraphClient();

      const userName = this.account.username;
      this.emit('stateChanged', { loggedIn: true, userName });
      return { ok: true, userName };
    } catch (err) {
      console.error('[TeamsGraphService] login() error:', err);
      this.emit('error', { message: String(err) });
      return { ok: false, error: String(err) };
    }
  }

  async logout(): Promise<void> {
    try {
      this.stopPolling();

      if (this.msalApp) {
        const tokenCache = this.msalApp.getTokenCache();
        const accounts = await tokenCache.getAllAccounts();
        for (const acct of accounts) {
          await tokenCache.removeAccount(acct);
        }
      }

      if (this.cacheFilePath && fs.existsSync(this.cacheFilePath)) {
        try {
          fs.unlinkSync(this.cacheFilePath);
        } catch (err) {
          console.error('[TeamsGraphService] Could not delete cache file:', err);
        }
      }

      this.account = null;
      this.graphClient = null;

      this.emit('stateChanged', { loggedIn: false, userName: null });
    } catch (err) {
      console.error('[TeamsGraphService] logout() error:', err);
      this.emit('error', { message: String(err) });
    }
  }

  // ── Interactive Auth Window ────────────────────────────────────────────────

  private acquireTokenInteractive(): Promise<AuthenticationResult> {
    return new Promise((resolve, reject) => {
      if (!this.msalApp) {
        reject(new Error('MSAL app not initialized'));
        return;
      }

      let authWindow: BrowserWindow | null = null;

      const closeWindow = (): void => {
        if (authWindow && !authWindow.isDestroyed()) {
          authWindow.close();
          authWindow = null;
        }
      };

      authWindow = new BrowserWindow({
        width: 600,
        height: 700,
        show: true,
        alwaysOnTop: true,
        webPreferences: {
          nodeIntegration: false,
          contextIsolation: true,
        },
      });

      authWindow.on('closed', () => {
        authWindow = null;
      });

      this.msalApp
        .acquireTokenInteractive({
          scopes: TEAMS_SCOPES,
          openBrowser: async (url: string) => {
            if (authWindow && !authWindow.isDestroyed()) {
              await authWindow.loadURL(url);
            }
          },
          successTemplate:
            '<html><body><h2>Login successful! You may close this window.</h2></body></html>',
          errorTemplate:
            '<html><body><h2>Authentication failed: {error}. You may close this window.</h2></body></html>',
        })
        .then((result) => {
          closeWindow();
          resolve(result);
        })
        .catch((err: unknown) => {
          closeWindow();
          reject(err);
        });
    });
  }

  // ── Token Acquisition ──────────────────────────────────────────────────────

  private async getAccessToken(): Promise<string | null> {
    if (!this.msalApp || !this.account) {
      return null;
    }

    try {
      const result = await this.msalApp.acquireTokenSilent({
        scopes: TEAMS_SCOPES,
        account: this.account,
      });

      if (result) {
        this.account = result.account ?? this.account;
        return result.accessToken;
      }

      return null;
    } catch (err) {
      if (err instanceof InteractionRequiredAuthError) {
        console.warn('[TeamsGraphService] Silent token refresh failed — interaction required.');
        this.emit('authRequired');
      } else {
        console.error('[TeamsGraphService] getAccessToken() error:', err);
        this.emit('error', { message: String(err) });
      }
      return null;
    }
  }

  // ── Graph Client ───────────────────────────────────────────────────────────

  private buildGraphClient(): void {
    this.graphClient = Client.init({
      authProvider: async (done) => {
        try {
          const token = await this.getAccessToken();
          if (token) {
            done(null, token);
          } else {
            done(new Error('Could not acquire access token'), null);
          }
        } catch (err) {
          done(err instanceof Error ? err : new Error(String(err)), null);
        }
      },
    });
  }

  // ── Presence ───────────────────────────────────────────────────────────────

  async getPresence(): Promise<PresenceData | null> {
    if (!this.graphClient || !this.account) {
      return null;
    }

    try {
      const response = (await this.graphClient
        .api('/me/presence')
        .get()) as GraphPresenceResponse;

      return {
        availability: response.availability,
        activity: response.activity,
      };
    } catch (err) {
      const isUnauthorized =
        err instanceof Error &&
        (err.message.includes('401') || err.message.includes('Unauthorized'));

      if (isUnauthorized) {
        console.warn('[TeamsGraphService] 401 on presence fetch — attempting silent refresh.');
        const token = await this.getAccessToken();
        if (!token) {
          this.emit('authRequired');
        } else {
          // Rebuild client with fresh token and retry once
          this.buildGraphClient();
          try {
            const retryResponse = (await this.graphClient
              .api('/me/presence')
              .get()) as GraphPresenceResponse;
            return {
              availability: retryResponse.availability,
              activity: retryResponse.activity,
            };
          } catch (retryErr) {
            console.error('[TeamsGraphService] Retry after token refresh failed:', retryErr);
            this.emit('authRequired');
          }
        }
      } else {
        console.error('[TeamsGraphService] getPresence() error:', err);
        this.emit('error', { message: String(err) });
      }

      return null;
    }
  }

  async startPolling(intervalMs?: number): Promise<void> {
    this.stopPolling();

    if (!this.account) {
      console.warn('[TeamsGraphService] startPolling() called but not logged in.');
      return;
    }

    const interval = intervalMs ?? DEFAULT_POLL_INTERVAL_MS;

    // Fire immediately, then on interval
    const poll = async (): Promise<void> => {
      try {
        const presence = await this.getPresence();
        if (presence) {
          this.emit('presenceChanged', presence);
        }
      } catch (err) {
        console.error('[TeamsGraphService] Poll error:', err);
      }
    };

    await poll();

    this.pollTimer = setInterval(() => {
      poll().catch((err) => {
        console.error('[TeamsGraphService] Polling interval error:', err);
      });
    }, interval);
  }

  stopPolling(): void {
    if (this.pollTimer !== null) {
      clearInterval(this.pollTimer);
      this.pollTimer = null;
    }
  }

  // ── Getters ────────────────────────────────────────────────────────────────

  get isLoggedIn(): boolean {
    return this.account !== null;
  }

  get currentUser(): string | null {
    return this.account?.username ?? null;
  }
}
