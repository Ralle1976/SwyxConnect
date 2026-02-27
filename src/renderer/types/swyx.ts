export {
  LineState,
  BridgeState,
  PresenceStatus,
} from '../../shared/types';

export type {
  LineInfo,
  CallDetails,
  Contact,
  CallHistoryEntry,
  VoicemailEntry,
  ColleaguePresence,
  BridgeMessage,
  BridgeRequest,
  BridgeResponse,
  BridgeEvent,
  BridgeError,
  AppSettings,
  TeamsTokens,
  WindowBounds,
} from '../../shared/types';

import type { SwyxApi } from '../../preload/index';

declare global {
  interface Window {
    swyxApi: SwyxApi;
  }
}
