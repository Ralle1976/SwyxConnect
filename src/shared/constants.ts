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
  // Event channels (main → renderer)
  LINE_STATE_CHANGED: 'swyx:lineStateChanged',
  BRIDGE_STATE_CHANGED: 'swyx:bridgeStateChanged',
  INCOMING_CALL: 'swyx:incomingCall',
  PRESENCE_CHANGED: 'swyx:presenceChanged',
  CALL_ENDED: 'swyx:callEnded',
  HEARTBEAT: 'swyx:heartbeat',
} as const;

export type IpcChannelKey = keyof typeof IPC_CHANNELS;
export type IpcChannelValue = (typeof IPC_CHANNELS)[IpcChannelKey];
