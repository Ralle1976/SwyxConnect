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
  TeamsIntegrationMode,
  TeamsLocalStatus,
  TeamsLocalAccount,
} from '../../shared/types';

import type { SwyxApi, WindowControls } from '../../preload/index';

declare global {
  interface Window {
    swyxApi: SwyxApi;
    windowControls: WindowControls;
  }
}
