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

// ─── ComSocket (SignalR) types ───────────────────────────────────────────────

export interface PhoneBookEntry {
  id: number;
  name: string;
  number: string;
  description?: string;
  editable: number;
  hide: number;
  curState: number; // 0=Offline, 3=Away, 4=Busy
  entityType: number; // 3=User, 4=Group
  entityId: number;
  siteId: number;
  numberId: number;
  numberType: number;
}

export interface PhoneBookReply {
  totalEntriesFiltered: number;
  startIndex: number;
  entries: PhoneBookEntry[];
}

export interface JournalEntry {
  id: string;
  kind: number; // 0=incoming, 1=?, 2=missed, ...
  name: string;
  number: string;
  callStart: string; // ISO-8601
  callDuration: number; // seconds
  callState: number;
  callbackState: number;
  dialedNumber?: string;
  dialedName?: string;
  connectedName?: string;
  viewed?: boolean;
  callbackRequested?: boolean;
}

export interface SpeedDialEntry {
  keyIndex?: number;
  name: string;
  number: string;
  state: number; // 0=Offline, 2=Available, 3=Busy, 4=DND, 5=Away
}

export interface ForwardingConfig {
  unconditional?: { status: number; number: string };
  busy?: { status: number; number: string };
  noReply?: { status: number; number: string; timeout: number };
}

export interface AudioModes {
  isIdle: boolean;
  isOpenListening: boolean;
  currentAudioMode: number;
  defaultAudioMode: number;
  isHandsetAvailable: boolean;
  isHandsfreeAvailable: boolean;
  isHeadsetAvailable: boolean;
  isOpenListeningAvailable: boolean;
}

export interface AudioVolumes {
  handsetVolume: number;
  handsfreeVolume: number;
  headsetVolume: number;
  ringingVolume: number;
  openListeningVolume: number;
  isMicrophoneEnabled: boolean;
}

export interface VersionInfo {
  swyxItVersion: string;
  comSocketInterfaceVersion: number;
  comSocketVersion: string;
}

export interface UserGroup {
  groupId: number;
  name: string;
}

export type JournalPart = 'all' | 'missed' | 'outgoing' | 'incoming';

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
  teamsIntegrationMode: TeamsIntegrationMode;
  windowBounds: WindowBounds | null;
  sidebarCollapsed: boolean;
  pluginsDirectory: string;
  startMinimized: boolean;
  closeToTray: boolean;
  numberOfLines: number;
  trunkPrefix: string;
  trunkPrefixEnabled: boolean;
}

export interface TeamsTokens {
  accessToken: string;
  refreshToken: string;
  expiresAt: number;
  userId: string;
  tenantId: string;
}

export type TeamsIntegrationMode = 'local' | 'graph' | 'off';

export interface TeamsLocalAccount {
  id: string;
  email: string;
  clientVersion: 'Legacy' | 'New2023' | 'Unknown';
  isRunning: boolean;
}

export interface TeamsLocalStatus {
  connected: boolean;
  availability: string;
  activity: string;
  currentUser: string | null;
  clientVersion: string | null;
}

export interface SwyxConnectionInfo {
  connected: boolean;
  serverName: string | null;
  userName: string | null;
  ownNumber: string | null;
  version: string | null;
  isRegistered: boolean;
}

// ─── Auth Session types ──────────────────────────────────────────────────────

export enum AuthMode {
  Windows = 0,
  UsernamePassword = 1,
}

export interface AuthCredentials {
  server: string;
  backupServer?: string;
  username: string;
  password: string;
  authMode: AuthMode;
  ctiMaster: boolean;
}

export interface AuthSessionInfo {
  isAuthenticated: boolean;
  server?: string;
  username?: string;
  error?: string;
}

export interface AuthState {
  status: 'idle' | 'authenticating' | 'authenticated' | 'failed' | 'logging_out';
  session: AuthSessionInfo | null;
  error?: string;
}
