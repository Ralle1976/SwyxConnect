// ─── Bridge timing constants ────────────────────────────────────────────────
export const BRIDGE_HEARTBEAT_INTERVAL = 5000 as const;
export const BRIDGE_HEARTBEAT_TIMEOUT = 10000 as const;
export const BRIDGE_MAX_RESTARTS = 3 as const;
export const BRIDGE_RESTART_WINDOW = 60000 as const;
export const BRIDGE_REQUEST_TIMEOUT = 10000 as const;

// ─── IPC channel names ───────────────────────────────────────────────────────
export const IPC_CHANNELS = {
  // Invoke channels (renderer → main)
  DIAL: 'swyx:dial',
  ANSWER: 'swyx:answer',
  HANGUP: 'swyx:hangup',
  HOLD: 'swyx:hold',
  TRANSFER: 'swyx:transfer',
  GET_LINES: 'swyx:getLines',
  GET_CONTACTS: 'swyx:getContacts',
  GET_HISTORY: 'swyx:getHistory',
  GET_VOICEMAILS: 'swyx:getVoicemails',
  GET_PRESENCE: 'swyx:getPresence',
  SET_PRESENCE: 'swyx:setPresence',
  GET_COLLEAGUE_PRESENCE: 'swyx:getColleaguePresence',
  GET_BRIDGE_STATE: 'swyx:getBridgeState',
  GET_SETTINGS: 'swyx:getSettings',
  SET_SETTINGS: 'swyx:setSettings',
  SEND_DTMF: 'swyx:sendDtmf',
  MUTE: 'swyx:mute',
  UNMUTE: 'swyx:unmute',
  SET_NUMBER_OF_LINES: 'swyx:setNumberOfLines',
  GET_CONNECTION_INFO: 'swyx:getConnectionInfo',
  // Event channels (main → renderer)
  LINE_STATE_CHANGED: 'swyx:lineStateChanged',
  BRIDGE_STATE_CHANGED: 'swyx:bridgeStateChanged',
  INCOMING_CALL: 'swyx:incomingCall',
  PRESENCE_CHANGED: 'swyx:presenceChanged',
  CALL_ENDED: 'swyx:callEnded',
  HEARTBEAT: 'swyx:heartbeat',
  // TeamsLocal channels
  TEAMS_LOCAL_CONNECT: 'swyx:teamsLocal:connect',
  TEAMS_LOCAL_DISCONNECT: 'swyx:teamsLocal:disconnect',
  TEAMS_LOCAL_GET_STATUS: 'swyx:teamsLocal:getStatus',
  TEAMS_LOCAL_GET_AVAILABILITY: 'swyx:teamsLocal:getAvailability',
  TEAMS_LOCAL_SET_AVAILABILITY: 'swyx:teamsLocal:setAvailability',
  TEAMS_LOCAL_MAKE_CALL: 'swyx:teamsLocal:makeCall',
  TEAMS_LOCAL_GET_ACCOUNTS: 'swyx:teamsLocal:getAccounts',
  // TeamsLocal events (main → renderer)
  TEAMS_LOCAL_PRESENCE_CHANGED: 'swyx:teamsLocal:presenceChanged',
  TEAMS_LOCAL_INCOMING_CALL: 'swyx:teamsLocal:incomingCall',
  TEAMS_LOCAL_STATE_CHANGED: 'swyx:teamsLocal:stateChanged',
  TEAMS_LOCAL_ERROR: 'swyx:teamsLocal:error',
  // TeamsGraph channels (Graph API)
  TEAMS_GRAPH_LOGIN: 'swyx:teamsGraph:login',
  TEAMS_GRAPH_LOGOUT: 'swyx:teamsGraph:logout',
  TEAMS_GRAPH_GET_STATUS: 'swyx:teamsGraph:getStatus',
  TEAMS_GRAPH_START_POLLING: 'swyx:teamsGraph:startPolling',
  TEAMS_GRAPH_STOP_POLLING: 'swyx:teamsGraph:stopPolling',
  // TeamsGraph events (main → renderer)
  TEAMS_GRAPH_PRESENCE_CHANGED: 'swyx:teamsGraph:presenceChanged',
  TEAMS_GRAPH_STATE_CHANGED: 'swyx:teamsGraph:stateChanged',
  TEAMS_GRAPH_AUTH_REQUIRED: 'swyx:teamsGraph:authRequired',
  TEAMS_GRAPH_ERROR: 'swyx:teamsGraph:error',
} as const;

export type IpcChannelKey = keyof typeof IPC_CHANNELS;
export type IpcChannelValue = (typeof IPC_CHANNELS)[IpcChannelKey];
