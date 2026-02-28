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

  // --- Teams Integration ---
  teamsAddAccount: (): Promise<unknown> =>
    ipcRenderer.invoke(IPC_CHANNELS.TEAMS_ADD_ACCOUNT),

  teamsRemoveAccount: (accountId: string): Promise<void> =>
    ipcRenderer.invoke(IPC_CHANNELS.TEAMS_REMOVE_ACCOUNT, accountId),

  teamsGetAccounts: (): Promise<unknown[]> =>
    ipcRenderer.invoke(IPC_CHANNELS.TEAMS_GET_ACCOUNTS),

  teamsSetClientId: (clientId: string): Promise<void> =>
    ipcRenderer.invoke(IPC_CHANNELS.TEAMS_SET_CLIENT_ID, clientId),

  teamsSetEnabled: (enabled: boolean): Promise<void> =>
    ipcRenderer.invoke(IPC_CHANNELS.TEAMS_SET_ENABLED, enabled),

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

  onTeamsDeviceCode: (callback: (data: { userCode: string; verificationUri: string; message: string }) => void): (() => void) => {
    const handler = (_event: Electron.IpcRendererEvent, data: { userCode: string; verificationUri: string; message: string }): void => callback(data)
    ipcRenderer.on(IPC_CHANNELS.TEAMS_DEVICE_CODE, handler)
    return () => ipcRenderer.removeListener(IPC_CHANNELS.TEAMS_DEVICE_CODE, handler)
  },

  onTeamsPresenceChanged: (callback: (data: unknown) => void): (() => void) => {
    const handler = (_event: Electron.IpcRendererEvent, data: unknown): void => callback(data)
    ipcRenderer.on(IPC_CHANNELS.TEAMS_PRESENCE_CHANGED, handler)
    return () => ipcRenderer.removeListener(IPC_CHANNELS.TEAMS_PRESENCE_CHANGED, handler)
  },

  onTeamsAccountAdded: (callback: (account: unknown) => void): (() => void) => {
    const handler = (_event: Electron.IpcRendererEvent, account: unknown): void => callback(account)
    ipcRenderer.on(IPC_CHANNELS.TEAMS_ACCOUNT_ADDED, handler)
    return () => ipcRenderer.removeListener(IPC_CHANNELS.TEAMS_ACCOUNT_ADDED, handler)
  },

  onTeamsAccountRemoved: (callback: (accountId: string) => void): (() => void) => {
    const handler = (_event: Electron.IpcRendererEvent, accountId: string): void => callback(accountId)
    ipcRenderer.on(IPC_CHANNELS.TEAMS_ACCOUNT_REMOVED, handler)
    return () => ipcRenderer.removeListener(IPC_CHANNELS.TEAMS_ACCOUNT_REMOVED, handler)
  },

  onTeamsError: (callback: (error: { message: string }) => void): (() => void) => {
    const handler = (_event: Electron.IpcRendererEvent, error: { message: string }): void => callback(error)
    ipcRenderer.on(IPC_CHANNELS.TEAMS_ERROR, handler)
    return () => ipcRenderer.removeListener(IPC_CHANNELS.TEAMS_ERROR, handler)
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
