import { contextBridge, ipcRenderer } from 'electron'
import { IPC_CHANNELS } from '../shared/constants'
import type {
  BridgeState,
  LineInfo,
  CallDetails,
  Contact,
  CallHistoryEntry,
  VoicemailEntry,
  ColleaguePresence,
  AppSettings,
  PresenceStatus,
  SwyxConnectionInfo,
} from '../shared/types'

// ─── Exposed API ──────────────────────────────────────────────────────────────

const swyxApi = {
  // --- Anruf-Steuerung ---
  dial: (number: string): Promise<unknown> =>
    ipcRenderer.invoke(IPC_CHANNELS.DIAL, number),

  answer: (lineId: number): Promise<unknown> =>
    ipcRenderer.invoke(IPC_CHANNELS.ANSWER, lineId),

  hangup: (lineId: number): Promise<unknown> =>
    ipcRenderer.invoke(IPC_CHANNELS.HANGUP, lineId),

  hold: (lineId: number): Promise<unknown> =>
    ipcRenderer.invoke(IPC_CHANNELS.HOLD, lineId),

  transfer: (lineId: number, target: string): Promise<unknown> =>
    ipcRenderer.invoke(IPC_CHANNELS.TRANSFER, lineId, target),

  sendDtmf: (lineId: number, digit: string): Promise<unknown> =>
    ipcRenderer.invoke(IPC_CHANNELS.SEND_DTMF, lineId, digit),

  mute: (lineId: number): Promise<unknown> =>
    ipcRenderer.invoke(IPC_CHANNELS.MUTE, lineId),

  unmute: (lineId: number): Promise<unknown> =>
    ipcRenderer.invoke(IPC_CHANNELS.UNMUTE, lineId),

  setNumberOfLines: (count: number): Promise<unknown> =>
    ipcRenderer.invoke(IPC_CHANNELS.SET_NUMBER_OF_LINES, count),

  // --- Abfragen ---
  getLines: (): Promise<LineInfo[]> =>
    ipcRenderer.invoke(IPC_CHANNELS.GET_LINES),

  getContacts: (query: string): Promise<Contact[]> =>
    ipcRenderer.invoke(IPC_CHANNELS.GET_CONTACTS, query),

  getHistory: (): Promise<CallHistoryEntry[]> =>
    ipcRenderer.invoke(IPC_CHANNELS.GET_HISTORY),

  getVoicemails: (): Promise<VoicemailEntry[]> =>
    ipcRenderer.invoke(IPC_CHANNELS.GET_VOICEMAILS),

  getPresence: (): Promise<ColleaguePresence[]> =>
    ipcRenderer.invoke(IPC_CHANNELS.GET_PRESENCE),

  setPresence: (status: PresenceStatus): Promise<unknown> =>
    ipcRenderer.invoke(IPC_CHANNELS.SET_PRESENCE, status),

  getColleaguePresence: (): Promise<ColleaguePresence[]> =>
    ipcRenderer.invoke(IPC_CHANNELS.GET_COLLEAGUE_PRESENCE),
  getBridgeState: (): Promise<BridgeState> =>
    ipcRenderer.invoke(IPC_CHANNELS.GET_BRIDGE_STATE),

  getSettings: (): Promise<AppSettings> =>
    ipcRenderer.invoke(IPC_CHANNELS.GET_SETTINGS),

  setSettings: (patch: Partial<AppSettings>): Promise<void> =>
    ipcRenderer.invoke(IPC_CHANNELS.SET_SETTINGS, patch),

  getConnectionInfo: (): Promise<SwyxConnectionInfo> =>
    ipcRenderer.invoke(IPC_CHANNELS.GET_CONNECTION_INFO),
  // --- Event Listener ---
  onLineStateChanged: (callback: (lines: LineInfo[]) => void): (() => void) => {
    const handler = (_event: Electron.IpcRendererEvent, lines: LineInfo[]): void => callback(lines)
    ipcRenderer.on(IPC_CHANNELS.LINE_STATE_CHANGED, handler)
    return () => ipcRenderer.removeListener(IPC_CHANNELS.LINE_STATE_CHANGED, handler)
  },

  onBridgeStateChanged: (callback: (state: BridgeState) => void): (() => void) => {
    const handler = (_event: Electron.IpcRendererEvent, state: BridgeState): void => callback(state)
    ipcRenderer.on(IPC_CHANNELS.BRIDGE_STATE_CHANGED, handler)
    return () => ipcRenderer.removeListener(IPC_CHANNELS.BRIDGE_STATE_CHANGED, handler)
  },

  onIncomingCall: (callback: (call: CallDetails) => void): (() => void) => {
    const handler = (_event: Electron.IpcRendererEvent, call: CallDetails): void => callback(call)
    ipcRenderer.on(IPC_CHANNELS.INCOMING_CALL, handler)
    return () => ipcRenderer.removeListener(IPC_CHANNELS.INCOMING_CALL, handler)
  },

  onPresenceChanged: (callback: (colleagues: ColleaguePresence[]) => void): (() => void) => {
    const handler = (_event: Electron.IpcRendererEvent, colleagues: ColleaguePresence[]): void =>
      callback(colleagues)
    ipcRenderer.on(IPC_CHANNELS.PRESENCE_CHANGED, handler)
    return () => ipcRenderer.removeListener(IPC_CHANNELS.PRESENCE_CHANGED, handler)
  },

  onCallEnded: (callback: (data: { lineId: number }) => void): (() => void) => {
    const handler = (_event: Electron.IpcRendererEvent, data: { lineId: number }): void => callback(data)
    ipcRenderer.on(IPC_CHANNELS.CALL_ENDED, handler)
    return () => ipcRenderer.removeListener(IPC_CHANNELS.CALL_ENDED, handler)
  },

  // --- TeamsLocal ---
  teamsLocalConnect: (): Promise<unknown> =>
    ipcRenderer.invoke(IPC_CHANNELS.TEAMS_LOCAL_CONNECT),
  teamsLocalDisconnect: (): Promise<void> =>
    ipcRenderer.invoke(IPC_CHANNELS.TEAMS_LOCAL_DISCONNECT),
  teamsLocalGetStatus: (): Promise<unknown> =>
    ipcRenderer.invoke(IPC_CHANNELS.TEAMS_LOCAL_GET_STATUS),
  teamsLocalGetAvailability: (): Promise<string> =>
    ipcRenderer.invoke(IPC_CHANNELS.TEAMS_LOCAL_GET_AVAILABILITY),
  teamsLocalSetAvailability: (availability: string): Promise<boolean> =>
    ipcRenderer.invoke(IPC_CHANNELS.TEAMS_LOCAL_SET_AVAILABILITY, availability),
  teamsLocalMakeCall: (phoneNumber: string): Promise<boolean> =>
    ipcRenderer.invoke(IPC_CHANNELS.TEAMS_LOCAL_MAKE_CALL, phoneNumber),
  teamsLocalGetAccounts: (): Promise<unknown[]> =>
    ipcRenderer.invoke(IPC_CHANNELS.TEAMS_LOCAL_GET_ACCOUNTS),
  teamsLocalGetTeamsPresence: (): Promise<{ availability: string; activity: string; source: string; isRunning: boolean }> =>
    ipcRenderer.invoke(IPC_CHANNELS.TEAMS_LOCAL_GET_TEAMS_PRESENCE),
  teamsLocalStartWatch: (): Promise<{ ok: boolean; isRunning: boolean }> =>
    ipcRenderer.invoke(IPC_CHANNELS.TEAMS_LOCAL_START_WATCH),
  teamsLocalStopWatch: (): Promise<{ ok: boolean; isRunning: boolean }> =>
    ipcRenderer.invoke(IPC_CHANNELS.TEAMS_LOCAL_STOP_WATCH),

  // TeamsLocal event listeners
  onTeamsLocalPresenceChanged: (callback: (status: unknown) => void): (() => void) => {
    const handler = (_event: Electron.IpcRendererEvent, status: unknown): void => callback(status)
    ipcRenderer.on(IPC_CHANNELS.TEAMS_LOCAL_PRESENCE_CHANGED, handler)
    return () => ipcRenderer.removeListener(IPC_CHANNELS.TEAMS_LOCAL_PRESENCE_CHANGED, handler)
  },
  onTeamsLocalStateChanged: (callback: (status: unknown) => void): (() => void) => {
    const handler = (_event: Electron.IpcRendererEvent, status: unknown): void => callback(status)
    ipcRenderer.on(IPC_CHANNELS.TEAMS_LOCAL_STATE_CHANGED, handler)
    return () => ipcRenderer.removeListener(IPC_CHANNELS.TEAMS_LOCAL_STATE_CHANGED, handler)
  },
  onTeamsLocalIncomingCall: (callback: (data: unknown) => void): (() => void) => {
    const handler = (_event: Electron.IpcRendererEvent, data: unknown): void => callback(data)
    ipcRenderer.on(IPC_CHANNELS.TEAMS_LOCAL_INCOMING_CALL, handler)
    return () => ipcRenderer.removeListener(IPC_CHANNELS.TEAMS_LOCAL_INCOMING_CALL, handler)
  },
  // --- TeamsGraph (Microsoft Graph API) ---
  teamsGraphLogin: (): Promise<{ ok: boolean; userName?: string; error?: string }> =>
    ipcRenderer.invoke(IPC_CHANNELS.TEAMS_GRAPH_LOGIN),
  teamsGraphLogout: (): Promise<void> =>
    ipcRenderer.invoke(IPC_CHANNELS.TEAMS_GRAPH_LOGOUT),
  teamsGraphGetStatus: (): Promise<{ loggedIn: boolean; userName: string | null; presence: { availability: string; activity: string } | null }> =>
    ipcRenderer.invoke(IPC_CHANNELS.TEAMS_GRAPH_GET_STATUS),
  teamsGraphStartPolling: (): Promise<void> =>
    ipcRenderer.invoke(IPC_CHANNELS.TEAMS_GRAPH_START_POLLING),
  teamsGraphStopPolling: (): Promise<void> =>
    ipcRenderer.invoke(IPC_CHANNELS.TEAMS_GRAPH_STOP_POLLING),

  // TeamsGraph event listeners
  onTeamsGraphPresenceChanged: (callback: (presence: { availability: string; activity: string }) => void): (() => void) => {
    const handler = (_event: Electron.IpcRendererEvent, presence: { availability: string; activity: string }): void => callback(presence)
    ipcRenderer.on(IPC_CHANNELS.TEAMS_GRAPH_PRESENCE_CHANGED, handler)
    return () => ipcRenderer.removeListener(IPC_CHANNELS.TEAMS_GRAPH_PRESENCE_CHANGED, handler)
  },
  onTeamsGraphStateChanged: (callback: (state: { loggedIn: boolean; userName: string | null }) => void): (() => void) => {
    const handler = (_event: Electron.IpcRendererEvent, state: { loggedIn: boolean; userName: string | null }): void => callback(state)
    ipcRenderer.on(IPC_CHANNELS.TEAMS_GRAPH_STATE_CHANGED, handler)
    return () => ipcRenderer.removeListener(IPC_CHANNELS.TEAMS_GRAPH_STATE_CHANGED, handler)
  },
  onTeamsGraphAuthRequired: (callback: () => void): (() => void) => {
    const handler = (): void => callback()
    ipcRenderer.on(IPC_CHANNELS.TEAMS_GRAPH_AUTH_REQUIRED, handler)
    return () => ipcRenderer.removeListener(IPC_CHANNELS.TEAMS_GRAPH_AUTH_REQUIRED, handler)
  },
  onTeamsGraphError: (callback: (err: { message: string }) => void): (() => void) => {
    const handler = (_event: Electron.IpcRendererEvent, err: { message: string }): void => callback(err)
    ipcRenderer.on(IPC_CHANNELS.TEAMS_GRAPH_ERROR, handler)
    return () => ipcRenderer.removeListener(IPC_CHANNELS.TEAMS_GRAPH_ERROR, handler)
  },
} as const

// ─── Expose to Renderer ───────────────────────────────────────────────────────

contextBridge.exposeInMainWorld('swyxApi', swyxApi)

const windowControls = {
  minimize: (): void => ipcRenderer.send('window:minimize'),
  maximize: (): void => ipcRenderer.send('window:maximize'),
  close: (): void => ipcRenderer.send('window:close'),
}

contextBridge.exposeInMainWorld('windowControls', windowControls)

// ─── Type Declaration ─────────────────────────────────────────────────────────

export type SwyxApi = typeof swyxApi
export type WindowControls = typeof windowControls
