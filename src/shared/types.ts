// ─── Enums ───────────────────────────────────────────────────────────────────

export enum LineState {
  Inactive = 'Inactive',
  HookOffInternal = 'HookOffInternal',
  HookOffExternal = 'HookOffExternal',
  Ringing = 'Ringing',
  Dialing = 'Dialing',
  Alerting = 'Alerting',
  Knocking = 'Knocking',
  Busy = 'Busy',
  Active = 'Active',
  OnHold = 'OnHold',
  ConferenceActive = 'ConferenceActive',
  ConferenceOnHold = 'ConferenceOnHold',
  Terminated = 'Terminated',
  Transferring = 'Transferring',
  Disabled = 'Disabled',
  DirectCall = 'DirectCall',
}

export enum BridgeState {
  Starting = 'Starting',
  Connected = 'Connected',
  Disconnected = 'Disconnected',
  Restarting = 'Restarting',
  Failed = 'Failed',
}

export enum PresenceStatus {
  Available = 'Available',
  Away = 'Away',
  Busy = 'Busy',
  DND = 'DND',
  Offline = 'Offline',
}

// ─── Core domain interfaces ──────────────────────────────────────────────────

export interface LineInfo {
  id: number;
  state: LineState;
  callerName?: string;
  callerNumber?: string;
  duration?: number;
  isSelected: boolean;
}

export interface CallDetails {
  lineId: number;
  direction: 'inbound' | 'outbound';
  callerName: string;
  callerNumber: string;
  startTime: number;
  state: LineState;
}

export interface Contact {
  id: string;
  name: string;
  number: string;
  email?: string;
  department?: string;
  presence?: PresenceStatus;
}

export interface CallHistoryEntry {
  id: string;
  callerName: string;
  callerNumber: string;
  direction: 'inbound' | 'outbound' | 'missed';
  timestamp: number;
  duration: number;
}

export interface VoicemailEntry {
  id: string;
  callerName: string;
  callerNumber: string;
  timestamp: number;
  duration: number;
  isNew: boolean;
}

export interface ColleaguePresence {
  userId: string;
  name: string;
  status: PresenceStatus;
  statusText?: string;
}

export interface WindowBounds {
  x: number;
  y: number;
  width: number;
  height: number;
}

// ─── JSON-RPC 2.0 types ──────────────────────────────────────────────────────

export interface BridgeError {
  code: number;
  message: string;
  data?: unknown;
}

export interface BridgeMessage {
  jsonrpc: '2.0';
  id?: number;
  method?: string;
  params?: unknown;
  result?: unknown;
  error?: BridgeError;
}

export interface BridgeRequest {
  jsonrpc: '2.0';
  id: number;
  method: string;
  params?: unknown;
}

export interface BridgeResponse {
  jsonrpc: '2.0';
  id: number;
  result?: unknown;
  error?: BridgeError;
}

export interface BridgeEvent {
  jsonrpc: '2.0';
  method: string;
  params?: unknown;
}

// ─── App settings ────────────────────────────────────────────────────────────

export interface AppSettings {
  theme: 'light' | 'dark' | 'system';
  audioInputDevice: string;
  audioOutputDevice: string;
  audioInputVolume: number;
  audioOutputVolume: number;
  teamsEnabled: boolean;
  teamsTokens: TeamsTokens | null;
  windowBounds: WindowBounds | null;
  sidebarCollapsed: boolean;
  pluginsDirectory: string;
  startMinimized: boolean;
  closeToTray: boolean;
  numberOfLines: number;
}

export interface TeamsTokens {
  accessToken: string;
  refreshToken: string;
  expiresAt: number;
  userId: string;
  tenantId: string;
}
