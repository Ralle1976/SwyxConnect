/**
 * TeamsPresenceService — Microsoft Teams Präsenz-Synchronisierung
 *
 * Unterstützt mehrere Teams-Konten (privat/geschäftlich) über separate
 * MSAL PublicClientApplication-Instanzen pro Tenant.
 *
 * Architektur:
 * - MSAL Device Code Flow für Authentifizierung
 * - MS Graph API für Presence Read/Write
 * - Polling alle 30s für bidirektionale Synchronisierung
 *
 * Benötigte Graph-Berechtigungen:
 * - Presence.Read (eigenen Status lesen)
 * - Presence.ReadWrite (eigenen Status setzen)
 * - User.Read (Profil)
 */

import { EventEmitter } from 'events';
import { BrowserWindow } from 'electron';
import { PresenceStatus } from '../../shared/types';

// ── Types ────────────────────────────────────────────────────────────────────

export interface TeamsAccount {
  id: string;
  displayName: string;
  email: string;
  tenantId: string;
  accessToken: string;
  refreshToken: string;
  expiresAt: number;
}

export interface TeamsPresenceInfo {
  accountId: string;
  availability: string; // Available, Busy, DoNotDisturb, Away, Offline, etc.
  activity: string;     // InACall, InAConferenceCall, Presenting, etc.
}

interface GraphPresenceResponse {
  availability: string;
  activity: string;
}

interface GraphTokenResponse {
  access_token: string;
  refresh_token?: string;
  expires_in: number;
  token_type: string;
}

interface GraphUserResponse {
  id: string;
  displayName: string;
  mail: string;
  userPrincipalName: string;
}

// ── Config ───────────────────────────────────────────────────────────────────

// Multi-tenant Azure AD App (muss in Azure Portal registriert werden)
// Für Entwicklung: Platzhalter — User muss eigene App-ID konfigurieren
const AZURE_CLIENT_ID = 'CONFIGURE_IN_SETTINGS';
const AZURE_AUTHORITY = 'https://login.microsoftonline.com/common';
const GRAPH_SCOPES = ['Presence.ReadWrite', 'User.Read'];
const GRAPH_BASE = 'https://graph.microsoft.com/v1.0';
const POLL_INTERVAL_MS = 30_000;

// ── Presence Mapping ─────────────────────────────────────────────────────────

function teamsToSwyx(availability: string): PresenceStatus {
  switch (availability) {
    case 'Available':
    case 'AvailableIdle':
      return PresenceStatus.Available;
    case 'Busy':
    case 'BusyIdle':
    case 'InACall':
    case 'InAConferenceCall':
    case 'InAMeeting':
    case 'Presenting':
    case 'UrgentInterruptionsOnly':
      return PresenceStatus.Busy;
    case 'DoNotDisturb':
      return PresenceStatus.DND;
    case 'Away':
    case 'BeRightBack':
      return PresenceStatus.Away;
    case 'Offline':
    case 'PresenceUnknown':
    default:
      return PresenceStatus.Offline;
  }
}

function swyxToTeams(status: PresenceStatus): { availability: string; activity: string } {
  switch (status) {
    case PresenceStatus.Available:
      return { availability: 'Available', activity: 'Available' };
    case PresenceStatus.Busy:
      return { availability: 'Busy', activity: 'InACall' };
    case PresenceStatus.DND:
      return { availability: 'DoNotDisturb', activity: 'Presenting' };
    case PresenceStatus.Away:
      return { availability: 'Away', activity: 'Away' };
    case PresenceStatus.Offline:
    default:
      return { availability: 'Offline', activity: 'OffWork' };
  }
}

// ── Service ──────────────────────────────────────────────────────────────────

export class TeamsPresenceService extends EventEmitter {
  private accounts: Map<string, TeamsAccount> = new Map();
  private pollTimer: ReturnType<typeof setInterval> | null = null;
  private lastTeamsPresence: Map<string, string> = new Map();
  private enabled = false;
  private clientId = AZURE_CLIENT_ID;

  /**
   * Service konfigurieren mit Azure App-Registrierungs-ID
   */
  setClientId(clientId: string): void {
    this.clientId = clientId;
  }

  /**
   * Bekannte Konten laden (aus gespeicherten Tokens)
   */
  loadAccounts(accounts: TeamsAccount[]): void {
    for (const account of accounts) {
      this.accounts.set(account.id, account);
    }
  }

  /**
   * Neues Teams-Konto hinzufügen via Device Code Flow
   * Öffnet den Browser zur Authentifizierung
   */
  async addAccount(parentWindow: BrowserWindow | null): Promise<TeamsAccount | null> {
    if (this.clientId === 'CONFIGURE_IN_SETTINGS') {
      throw new Error(
        'Azure App-ID nicht konfiguriert. ' +
        'Bitte registrieren Sie eine App unter https://portal.azure.com/#view/Microsoft_AAD_RegisteredApps ' +
        'und tragen Sie die Client-ID in den Einstellungen ein.'
      );
    }

    try {
      // Device Code Flow: User bekommt Code + URL angezeigt
      const deviceCodeResponse = await this.requestDeviceCode();

      // Event emittieren damit UI den Code anzeigen kann
      this.emit('deviceCode', {
        userCode: deviceCodeResponse.user_code,
        verificationUri: deviceCodeResponse.verification_uri,
        message: deviceCodeResponse.message,
      });

      // Auf Token warten (pollt Microsoft bis User den Code eingibt)
      const tokenResponse = await this.pollForToken(deviceCodeResponse.device_code, deviceCodeResponse.interval);

      if (!tokenResponse) return null;

      // User-Info abrufen
      const userInfo = await this.graphGet<GraphUserResponse>('/me', tokenResponse.access_token);

      const account: TeamsAccount = {
        id: userInfo.id,
        displayName: userInfo.displayName,
        email: userInfo.mail || userInfo.userPrincipalName,
        tenantId: this.extractTenantFromToken(tokenResponse.access_token),
        accessToken: tokenResponse.access_token,
        refreshToken: tokenResponse.refresh_token ?? '',
        expiresAt: Date.now() + tokenResponse.expires_in * 1000,
      };

      this.accounts.set(account.id, account);
      this.emit('accountAdded', account);

      return account;
    } catch (err) {
      console.error('[Teams] Anmeldung fehlgeschlagen:', err);
      return null;
    }
  }

  /**
   * Konto entfernen
   */
  removeAccount(accountId: string): void {
    this.accounts.delete(accountId);
    this.lastTeamsPresence.delete(accountId);
    this.emit('accountRemoved', accountId);
  }

  /**
   * Alle registrierten Konten
   */
  getAccounts(): TeamsAccount[] {
    return Array.from(this.accounts.values());
  }

  /**
   * Synchronisierung starten
   */
  start(): void {
    if (this.enabled) return;
    this.enabled = true;

    // Sofort erste Abfrage
    this.pollPresence();

    // Regelmäßig pollen
    this.pollTimer = setInterval(() => this.pollPresence(), POLL_INTERVAL_MS);
    console.log('[Teams] Präsenz-Synchronisierung gestartet');
  }

  /**
   * Synchronisierung stoppen
   */
  stop(): void {
    this.enabled = false;
    if (this.pollTimer) {
      clearInterval(this.pollTimer);
      this.pollTimer = null;
    }
    console.log('[Teams] Präsenz-Synchronisierung gestoppt');
  }

  /**
   * Swyx-Status an alle verbundenen Teams-Konten senden
   */
  async pushSwyxStatus(swyxStatus: PresenceStatus): Promise<void> {
    const teamsPresence = swyxToTeams(swyxStatus);

    for (const [accountId, account] of this.accounts) {
      try {
        const token = await this.ensureValidToken(account);
        await this.graphPost(
          '/me/presence/setPresenceByAppInstance',
          token,
          {
            sessionId: `swyxconnect-${accountId}`,
            availability: teamsPresence.availability,
            activity: teamsPresence.activity,
            expirationDuration: 'PT5M', // 5 Minuten, wird durch nächsten Poll erneuert
          }
        );
      } catch (err) {
        console.error(`[Teams] Status-Push fehlgeschlagen für ${account.email}:`, err);
      }
    }
  }

  // ── Private: Device Code Flow ────────────────────────────────────────────

  private async requestDeviceCode(): Promise<{
    device_code: string;
    user_code: string;
    verification_uri: string;
    interval: number;
    message: string;
  }> {
    const response = await fetch(
      `${AZURE_AUTHORITY}/oauth2/v2.0/devicecode`,
      {
        method: 'POST',
        headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
        body: new URLSearchParams({
          client_id: this.clientId,
          scope: GRAPH_SCOPES.join(' '),
        }).toString(),
      }
    );

    if (!response.ok) {
      throw new Error(`Device Code Anfrage fehlgeschlagen: ${response.status}`);
    }

    return response.json();
  }

  private async pollForToken(
    deviceCode: string,
    intervalSeconds: number
  ): Promise<GraphTokenResponse | null> {
    const maxAttempts = 120; // max ~10 Minuten
    const interval = Math.max(intervalSeconds, 5) * 1000;

    for (let i = 0; i < maxAttempts; i++) {
      await new Promise((resolve) => setTimeout(resolve, interval));

      try {
        const response = await fetch(
          `${AZURE_AUTHORITY}/oauth2/v2.0/token`,
          {
            method: 'POST',
            headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
            body: new URLSearchParams({
              client_id: this.clientId,
              grant_type: 'urn:ietf:params:oauth:grant-type:device_code',
              device_code: deviceCode,
            }).toString(),
          }
        );

        if (response.ok) {
          return response.json();
        }

        const error = await response.json();
        if (error.error === 'authorization_pending') {
          continue; // User hat Code noch nicht eingegeben
        }
        if (error.error === 'slow_down') {
          await new Promise((resolve) => setTimeout(resolve, 5000));
          continue;
        }
        if (error.error === 'expired_token') {
          console.warn('[Teams] Device Code abgelaufen');
          return null;
        }

        throw new Error(`Token-Abfrage fehlgeschlagen: ${error.error_description || error.error}`);
      } catch (err) {
        if (i === maxAttempts - 1) throw err;
      }
    }

    return null;
  }

  // ── Private: Token Refresh ───────────────────────────────────────────────

  private async ensureValidToken(account: TeamsAccount): Promise<string> {
    // Token noch gültig (mit 5 Min Puffer)
    if (account.expiresAt > Date.now() + 300_000) {
      return account.accessToken;
    }

    // Token erneuern
    if (!account.refreshToken) {
      throw new Error('Kein Refresh-Token verfügbar — erneute Anmeldung erforderlich');
    }

    const response = await fetch(
      `${AZURE_AUTHORITY}/oauth2/v2.0/token`,
      {
        method: 'POST',
        headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
        body: new URLSearchParams({
          client_id: this.clientId,
          grant_type: 'refresh_token',
          refresh_token: account.refreshToken,
          scope: GRAPH_SCOPES.join(' '),
        }).toString(),
      }
    );

    if (!response.ok) {
      throw new Error(`Token-Refresh fehlgeschlagen: ${response.status}`);
    }

    const data: GraphTokenResponse = await response.json();

    // Account aktualisieren
    account.accessToken = data.access_token;
    account.refreshToken = data.refresh_token ?? account.refreshToken;
    account.expiresAt = Date.now() + data.expires_in * 1000;
    this.accounts.set(account.id, account);
    this.emit('tokenRefreshed', account);

    return data.access_token;
  }

  // ── Private: Graph API ───────────────────────────────────────────────────

  private async graphGet<T>(path: string, token: string): Promise<T> {
    const response = await fetch(`${GRAPH_BASE}${path}`, {
      headers: { Authorization: `Bearer ${token}` },
    });

    if (!response.ok) {
      throw new Error(`Graph GET ${path}: ${response.status}`);
    }

    return response.json();
  }

  private async graphPost(path: string, token: string, body: unknown): Promise<void> {
    const response = await fetch(`${GRAPH_BASE}${path}`, {
      method: 'POST',
      headers: {
        Authorization: `Bearer ${token}`,
        'Content-Type': 'application/json',
      },
      body: JSON.stringify(body),
    });

    if (!response.ok) {
      const text = await response.text();
      throw new Error(`Graph POST ${path}: ${response.status} ${text}`);
    }
  }

  // ── Private: Polling ─────────────────────────────────────────────────────

  private async pollPresence(): Promise<void> {
    for (const [accountId, account] of this.accounts) {
      try {
        const token = await this.ensureValidToken(account);
        const presence = await this.graphGet<GraphPresenceResponse>('/me/presence', token);

        const previousAvailability = this.lastTeamsPresence.get(accountId);
        this.lastTeamsPresence.set(accountId, presence.availability);

        // Nur Event emittieren wenn sich der Status geändert hat
        if (previousAvailability !== presence.availability) {
          const swyxStatus = teamsToSwyx(presence.availability);
          this.emit('presenceChanged', {
            accountId,
            email: account.email,
            availability: presence.availability,
            activity: presence.activity,
            swyxStatus,
          } as TeamsPresenceInfo & { swyxStatus: PresenceStatus; email: string });
        }
      } catch (err) {
        console.error(`[Teams] Presence-Abfrage fehlgeschlagen für ${account.email}:`, err);
      }
    }
  }

  // ── Private: Utils ───────────────────────────────────────────────────────

  private extractTenantFromToken(accessToken: string): string {
    try {
      const payload = accessToken.split('.')[1];
      const decoded = JSON.parse(Buffer.from(payload, 'base64').toString());
      return decoded.tid || 'unknown';
    } catch {
      return 'unknown';
    }
  }
}
