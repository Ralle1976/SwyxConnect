import { ipcMain, BrowserWindow } from 'electron';
import { IPC_CHANNELS } from '../../shared/constants';
import {
  BridgeState,
  LineInfo,
  Contact,
  CallHistoryEntry,
  VoicemailEntry,
  ColleaguePresence,
  CallDetails,
  AppSettings,
  PresenceStatus,
} from '../../shared/types';
import { TeamsLocalService } from '../services/TeamsLocalService';
import { BridgeManager } from '../bridge/BridgeManager';
import { SettingsStore } from '../services/SettingsStore';
import { TeamsGraphService } from '../services/TeamsGraphService';

export function registerIpcHandlers(
  bridgeManager: BridgeManager,
  settingsStore: SettingsStore,
  getMainWindow: () => BrowserWindow | null
): void {
  ipcMain.handle(IPC_CHANNELS.DIAL, async (_event, number: string) => {
    return bridgeManager.sendRequest('dial', { number });
  });

  ipcMain.handle(IPC_CHANNELS.ANSWER, async (_event, lineId: number) => {
    return bridgeManager.sendRequest('answer', { lineId });
  });

  ipcMain.handle(IPC_CHANNELS.HANGUP, async (_event, lineId: number) => {
    return bridgeManager.sendRequest('hangup', { lineId });
  });

  ipcMain.handle(IPC_CHANNELS.HOLD, async (_event, lineId: number) => {
    return bridgeManager.sendRequest('hold', { lineId });
  });

  ipcMain.handle(
    IPC_CHANNELS.TRANSFER,
    async (_event, lineId: number, target: string) => {
      return bridgeManager.sendRequest('transfer', { lineId, target });
    }
  );

  ipcMain.handle(IPC_CHANNELS.GET_LINES, async () => {
    return bridgeManager.sendRequest('getLines') as Promise<LineInfo[]>;
  });

  ipcMain.handle(IPC_CHANNELS.GET_CONTACTS, async (_event, query: string) => {
    return bridgeManager.sendRequest('searchContacts', {
      query,
    }) as Promise<Contact[]>;
  });

  ipcMain.handle(IPC_CHANNELS.GET_HISTORY, async () => {
    return bridgeManager.sendRequest(
      'getCallHistory'
    ) as Promise<CallHistoryEntry[]>;
  });

  ipcMain.handle(IPC_CHANNELS.GET_VOICEMAILS, async () => {
    return bridgeManager.sendRequest(
      'getVoicemails'
    ) as Promise<VoicemailEntry[]>;
  });

  ipcMain.handle(IPC_CHANNELS.GET_PRESENCE, async () => {
    return bridgeManager.sendRequest(
      'getColleaguePresence'
    ) as Promise<ColleaguePresence[]>;
  });

  ipcMain.handle(IPC_CHANNELS.SET_PRESENCE, async (_event, status: PresenceStatus) => {
    return bridgeManager.sendRequest('setPresence', { status });
  });

  ipcMain.handle(IPC_CHANNELS.GET_COLLEAGUE_PRESENCE, async () => {
    return bridgeManager.sendRequest('getColleaguePresence');
  });

  ipcMain.handle(IPC_CHANNELS.GET_BRIDGE_STATE, () => {
    return bridgeManager.getState();
  });

  ipcMain.handle(IPC_CHANNELS.GET_SETTINGS, () => {
    return settingsStore.getAll();
  });

  ipcMain.handle(
    IPC_CHANNELS.SET_SETTINGS,
    (_event, patch: Partial<AppSettings>) => {
      settingsStore.patch(patch);
    }
  );

  ipcMain.handle(
    IPC_CHANNELS.SEND_DTMF,
    async (_event, lineId: number, digit: string) => {
      return bridgeManager.sendRequest('sendDtmf', { lineId, digit });
    }
  );

  ipcMain.handle(IPC_CHANNELS.SET_NUMBER_OF_LINES, async (_event, count: number) => {
    return bridgeManager.sendRequest('setNumberOfLines', { count });
  });

  ipcMain.handle(IPC_CHANNELS.GET_CONNECTION_INFO, async () => {
    return bridgeManager.sendRequest('getConnectionInfo');
  });


  ipcMain.handle(IPC_CHANNELS.MUTE, async (_event, lineId: number) => {
    return bridgeManager.sendRequest('mute', { lineId });
  });

  ipcMain.handle(IPC_CHANNELS.UNMUTE, async (_event, lineId: number) => {
    return bridgeManager.sendRequest('unmute', { lineId });
  });

  // ─── Forwarding & Advanced Call ─────────────────────────────────────────────
  ipcMain.handle(IPC_CHANNELS.FORWARD_CALL, async (_event, lineId: number, target: string) => {
    return bridgeManager.sendRequest('forwardCall', { lineId, target });
  });

  ipcMain.handle(IPC_CHANNELS.RESOLVE_NUMBER, async (_event, number: string) => {
    return bridgeManager.sendRequest('resolveNumber', { number });
  });

  ipcMain.handle(IPC_CHANNELS.CONVERT_NUMBER, async (_event, format: number, number: string) => {
    return bridgeManager.sendRequest('convertNumber', { format, number });
  });

  ipcMain.handle(IPC_CHANNELS.REQUEST_CALLBACK_ON_BUSY, async (_event, name: string, number: string) => {
    return bridgeManager.sendRequest('requestCallbackOnBusy', { name, number });
  });

  ipcMain.handle(IPC_CHANNELS.PICKUP_GROUP_CALL, async (_event, refId: number) => {
    return bridgeManager.sendRequest('pickupGroupCall', { refId });
  });

  ipcMain.handle(IPC_CHANNELS.GET_GROUP_NOTIFICATIONS, async () => {
    return bridgeManager.sendRequest('getGroupNotifications');
  });

  ipcMain.handle(IPC_CHANNELS.OPEN_CALL_ROUTING, async () => {
    return bridgeManager.sendRequest('openCallRouting');
  });

  // ─── Conference ────────────────────────────────────────────────────────────────
  ipcMain.handle(IPC_CHANNELS.CREATE_CONFERENCE, async (_event, lineNumber: number) => {
    return bridgeManager.sendRequest('createConference', { lineNumber });
  });

  ipcMain.handle(IPC_CHANNELS.JOIN_LINE_TO_CONFERENCE, async (_event, lineNumber: number) => {
    return bridgeManager.sendRequest('joinLineToConference', { lineNumber });
  });

  ipcMain.handle(IPC_CHANNELS.JOIN_ALL_TO_CONFERENCE, async (_event, lineNumber: number) => {
    return bridgeManager.sendRequest('joinAllToConference', { lineNumber });
  });

  ipcMain.handle(IPC_CHANNELS.GET_CONFERENCE_STATUS, async () => {
    return bridgeManager.sendRequest('getConferenceStatus');
  });

  // ─── Recording ─────────────────────────────────────────────────────────────────
  ipcMain.handle(IPC_CHANNELS.START_RECORDING, async (_event, lineNumber: number) => {
    return bridgeManager.sendRequest('startRecording', { lineNumber });
  });

  ipcMain.handle(IPC_CHANNELS.STOP_RECORDING, async (_event, lineNumber: number) => {
    return bridgeManager.sendRequest('stopRecording', { lineNumber });
  });

  ipcMain.handle(IPC_CHANNELS.PLAY_SOUND, async (_event, file: string, device: number, repeat: number) => {
    return bridgeManager.sendRequest('playSound', { file, device, repeat });
  });

  ipcMain.handle(IPC_CHANNELS.STOP_SOUND, async () => {
    return bridgeManager.sendRequest('stopSound');
  });

  // ─── System Info ───────────────────────────────────────────────────────────────
  ipcMain.handle(IPC_CHANNELS.GET_SYSTEM_INFO, async () => {
    return bridgeManager.sendRequest('getSystemInfo');
  });

  ipcMain.handle(IPC_CHANNELS.GET_AUDIO_DEVICES, async () => {
    return bridgeManager.sendRequest('getAudioDevices');
  });

  ipcMain.handle(IPC_CHANNELS.SET_AUDIO_MODE, async (_event, mode: number) => {
    return bridgeManager.sendRequest('setAudioMode', { mode });
  });

  ipcMain.handle(IPC_CHANNELS.SET_MICRO, async (_event, enabled: boolean) => {
    return bridgeManager.sendRequest('setMicro', { enabled });
  });

  ipcMain.handle(IPC_CHANNELS.SET_SPEAKER, async (_event, enabled: boolean) => {
    return bridgeManager.sendRequest('setSpeaker', { enabled });
  });
  // ─── Window Controls ──────────────────────────────────────────────────────────
  ipcMain.on('window:minimize', () => { getMainWindow()?.minimize(); });
  ipcMain.on('window:maximize', () => {
    const win = getMainWindow();
    if (win) win.isMaximized() ? win.unmaximize() : win.maximize();
  });
  ipcMain.on('window:close', () => { getMainWindow()?.close(); });

  bridgeManager.on('stateChanged', (state: BridgeState) => {
    getMainWindow()?.webContents.send(IPC_CHANNELS.BRIDGE_STATE_CHANGED, state);
  });

  bridgeManager.on('event', (evt) => {
    const win = getMainWindow();
    if (!win) return;

    switch (evt.method) {
      case 'bridgeState': {
        // Map COM-level state string to BridgeState enum
        const comState = (evt.params as { state: string }).state;
        const mappedState: BridgeState =
          comState === 'connected' ? BridgeState.Connected : BridgeState.Disconnected;
        win.webContents.send(IPC_CHANNELS.BRIDGE_STATE_CHANGED, mappedState);
        break;
      }

      case 'lineStateChanged':
        win.webContents.send(
          IPC_CHANNELS.LINE_STATE_CHANGED,
          (evt.params as { lines: LineInfo[] }).lines
        );
        break;

      case 'incomingCall':
        win.webContents.send(
          IPC_CHANNELS.INCOMING_CALL,
          evt.params as CallDetails
        );
        break;

      case 'presenceChanged':
        win.webContents.send(
          IPC_CHANNELS.PRESENCE_CHANGED,
          (evt.params as { colleagues: ColleaguePresence[] }).colleagues
        );
        break;

      case 'callEnded':
        win.webContents.send(IPC_CHANNELS.CALL_ENDED, evt.params);
        break;

      case 'teamsPresenceChanged':
        win.webContents.send(IPC_CHANNELS.TEAMS_LOCAL_PRESENCE_CHANGED, evt.params);
        break;

      default:
        break;
    }
  });
}

export function registerTeamsLocalIpcHandlers(
  teamsLocalService: TeamsLocalService,
  bridgeManager: BridgeManager,
  getMainWindow: () => BrowserWindow | null
): void {
  ipcMain.handle(IPC_CHANNELS.TEAMS_LOCAL_CONNECT, async () => {
    return teamsLocalService.connect();
  });
  ipcMain.handle(IPC_CHANNELS.TEAMS_LOCAL_DISCONNECT, async () => {
    return teamsLocalService.disconnect();
  });
  ipcMain.handle(IPC_CHANNELS.TEAMS_LOCAL_GET_STATUS, async () => {
    return teamsLocalService.getStatus();
  });
  ipcMain.handle(IPC_CHANNELS.TEAMS_LOCAL_GET_AVAILABILITY, async () => {
    return teamsLocalService.getAvailability();
  });
  ipcMain.handle(IPC_CHANNELS.TEAMS_LOCAL_SET_AVAILABILITY, async (_event, availability: string) => {
    return teamsLocalService.setAvailability(availability);
  });
  ipcMain.handle(IPC_CHANNELS.TEAMS_LOCAL_MAKE_CALL, async (_event, phoneNumber: string) => {
    return teamsLocalService.makeCall(phoneNumber);
  });
  ipcMain.handle(IPC_CHANNELS.TEAMS_LOCAL_GET_ACCOUNTS, async () => {
    return teamsLocalService.getAccounts();
  });

  // Teams Presence Watcher (Bridge-seitig)
  ipcMain.handle(IPC_CHANNELS.TEAMS_LOCAL_GET_TEAMS_PRESENCE, async () => {
    return bridgeManager.sendRequest('teams.local.getTeamsPresence');
  });
  ipcMain.handle(IPC_CHANNELS.TEAMS_LOCAL_START_WATCH, async () => {
    return bridgeManager.sendRequest('teams.local.startTeamsWatch');
  });
  ipcMain.handle(IPC_CHANNELS.TEAMS_LOCAL_STOP_WATCH, async () => {
    return bridgeManager.sendRequest('teams.local.stopTeamsWatch');
  });

  // Forward TeamsLocal events to renderer
  teamsLocalService.on('presenceChanged', (status) => {
    getMainWindow()?.webContents.send(IPC_CHANNELS.TEAMS_LOCAL_PRESENCE_CHANGED, status);
  });
  teamsLocalService.on('stateChanged', (status) => {
    getMainWindow()?.webContents.send(IPC_CHANNELS.TEAMS_LOCAL_STATE_CHANGED, status);
  });
}

export function registerTeamsGraphIpcHandlers(
  teamsGraphService: TeamsGraphService,
  getMainWindow: () => BrowserWindow | null
): void {
  ipcMain.handle(IPC_CHANNELS.TEAMS_GRAPH_LOGIN, async () => {
    return teamsGraphService.login();
  });

  ipcMain.handle(IPC_CHANNELS.TEAMS_GRAPH_LOGOUT, async () => {
    return teamsGraphService.logout();
  });

  ipcMain.handle(IPC_CHANNELS.TEAMS_GRAPH_GET_STATUS, async () => {
    return {
      loggedIn: teamsGraphService.isLoggedIn,
      userName: teamsGraphService.currentUser,
      presence: await teamsGraphService.getPresence(),
    };
  });

  ipcMain.handle(IPC_CHANNELS.TEAMS_GRAPH_START_POLLING, async () => {
    await teamsGraphService.startPolling();
  });

  ipcMain.handle(IPC_CHANNELS.TEAMS_GRAPH_STOP_POLLING, () => {
    teamsGraphService.stopPolling();
  });

  // Forward TeamsGraph events to renderer
  teamsGraphService.on('presenceChanged', (presence) => {
    getMainWindow()?.webContents.send(IPC_CHANNELS.TEAMS_GRAPH_PRESENCE_CHANGED, presence);
  });
  teamsGraphService.on('stateChanged', (state) => {
    getMainWindow()?.webContents.send(IPC_CHANNELS.TEAMS_GRAPH_STATE_CHANGED, state);
  });
  teamsGraphService.on('authRequired', () => {
    getMainWindow()?.webContents.send(IPC_CHANNELS.TEAMS_GRAPH_AUTH_REQUIRED);
  });
  teamsGraphService.on('error', (err) => {
    getMainWindow()?.webContents.send(IPC_CHANNELS.TEAMS_GRAPH_ERROR, err);
  });
}
