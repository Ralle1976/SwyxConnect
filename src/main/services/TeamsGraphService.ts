import { PresenceStatus, TeamsTokens } from '../../shared/types';

const TEAMS_AVAILABILITY_MAP: Record<string, PresenceStatus> = {
  Available: PresenceStatus.Available,
  Away: PresenceStatus.Away,
  BeRightBack: PresenceStatus.Away,
  Busy: PresenceStatus.Busy,
  DoNotDisturb: PresenceStatus.DND,
  Offline: PresenceStatus.Offline,
  PresenceUnknown: PresenceStatus.Offline,
};

export class TeamsGraphService {
  private tokens: TeamsTokens | null = null;

  setTokens(tokens: TeamsTokens | null): void {
    this.tokens = tokens;
  }

  hasTokens(): boolean {
    return this.tokens !== null && this.tokens.expiresAt > Date.now();
  }

  async login(): Promise<TeamsTokens> {
    throw new Error('Teams OAuth flow not yet implemented');
  }

  async logout(): Promise<void> {
    this.tokens = null;
  }

  async getPresence(): Promise<PresenceStatus> {
    if (!this.hasTokens()) return PresenceStatus.Offline;

    const response = await fetch(
      'https://graph.microsoft.com/v1.0/me/presence',
      {
        headers: {
          Authorization: `Bearer ${this.tokens!.accessToken}`,
          'Content-Type': 'application/json',
        },
      }
    );

    if (!response.ok) throw new Error(`Graph API error: ${response.status}`);

    const data = (await response.json()) as { availability: string };
    return TEAMS_AVAILABILITY_MAP[data.availability] ?? PresenceStatus.Offline;
  }

  async setPresence(status: PresenceStatus): Promise<void> {
    if (!this.hasTokens()) return;

    const availability = this.mapToTeamsAvailability(status);

    await fetch(
      `https://graph.microsoft.com/v1.0/users/${this.tokens!.userId}/presence/setUserPreferredPresence`,
      {
        method: 'POST',
        headers: {
          Authorization: `Bearer ${this.tokens!.accessToken}`,
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({
          availability,
          activity: availability,
          expirationDuration: 'PT1H',
        }),
      }
    );
  }

  private mapToTeamsAvailability(status: PresenceStatus): string {
    const map: Record<PresenceStatus, string> = {
      [PresenceStatus.Available]: 'Available',
      [PresenceStatus.Away]: 'Away',
      [PresenceStatus.Busy]: 'Busy',
      [PresenceStatus.DND]: 'DoNotDisturb',
      [PresenceStatus.Offline]: 'Offline',
    };
    return map[status];
  }
}
