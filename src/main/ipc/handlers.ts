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
import { BridgeManager } from '../bridge/BridgeManager';
import { SettingsStore } from '../services/SettingsStore';
import { TeamsPresenceService, TeamsAccount } from '../services/TeamsPresenceService';

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

  ipcMain.handle(IPC_CHANNELS.MUTE, async (_event, lineId: number) => {
    return bridgeManager.sendRequest('mute', { lineId });
  });

  ipcMain.handle(IPC_CHANNELS.UNMUTE, async (_event, lineId: number) => {
    return bridgeManager.sendRequest('unmute', { lineId });
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

      default:
        break;
    }
  });
}

// ─── Teams Integration IPC ─────────────────────────────────────────────────

export function registerTeamsIpcHandlers(
  teamsService: TeamsPresenceService,
  getMainWindow: () => BrowserWindow | null
): void {
  ipcMain.handle(IPC_CHANNELS.TEAMS_ADD_ACCOUNT, async () => {
    const win = getMainWindow();
    return teamsService.addAccount(win);
  });

  ipcMain.handle(IPC_CHANNELS.TEAMS_REMOVE_ACCOUNT, async (_event, accountId: string) => {
    teamsService.removeAccount(accountId);
  });

  ipcMain.handle(IPC_CHANNELS.TEAMS_GET_ACCOUNTS, () => {
    return teamsService.getAccounts();
  });

  ipcMain.handle(IPC_CHANNELS.TEAMS_SET_CLIENT_ID, (_event, clientId: string) => {
    teamsService.setClientId(clientId);
  });

  ipcMain.handle(IPC_CHANNELS.TEAMS_SET_ENABLED, (_event, enabled: boolean) => {
    if (enabled) {
      teamsService.start();
    } else {
      teamsService.stop();
    }
  });

  // Teams Events → Renderer
  teamsService.on('deviceCode', (data) => {
    getMainWindow()?.webContents.send(IPC_CHANNELS.TEAMS_DEVICE_CODE, data);
  });

  teamsService.on('presenceChanged', (data) => {
    getMainWindow()?.webContents.send(IPC_CHANNELS.TEAMS_PRESENCE_CHANGED, data);
  });

  teamsService.on('accountAdded', (account: TeamsAccount) => {
    getMainWindow()?.webContents.send(IPC_CHANNELS.TEAMS_ACCOUNT_ADDED, account);
  });

  teamsService.on('accountRemoved', (accountId: string) => {
    getMainWindow()?.webContents.send(IPC_CHANNELS.TEAMS_ACCOUNT_REMOVED, accountId);
  });

  teamsService.on('error', (error: Error) => {
    getMainWindow()?.webContents.send(IPC_CHANNELS.TEAMS_ERROR, {
      message: error.message,
    });
  });
}
