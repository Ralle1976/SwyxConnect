export {
  LineState,
  BridgeState,
  PresenceStatus,
  AuthMode,
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
  AuthCredentials,
  AuthSessionInfo,
  AuthState,
} from '../../shared/types';

import type { SwyxApi, WindowControls } from '../../preload/index';

declare global {
  interface Window {
    swyxApi: SwyxApi;
    windowControls: WindowControls;
  }
}
