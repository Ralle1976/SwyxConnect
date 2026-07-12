import { create } from 'zustand';
import { AuthState, AuthCredentials, AuthSessionInfo, AuthMode } from '../types/swyx';

interface AuthStoreState extends AuthState {
  login: (credentials: AuthCredentials) => Promise<{ ok: boolean; error?: string }>;
  logout: () => Promise<void>;
  refreshSessionStatus: () => Promise<void>;
  clearError: () => void;
}

export const useAuthStore = create<AuthStoreState>((set, get) => ({
  status: 'idle',
  session: null,
  error: undefined,

  login: async (credentials: AuthCredentials) => {
    set({ status: 'authenticating', error: undefined });
    try {
      const result = await window.swyxApi.login(credentials);
      if (result.ok) {
        set({
          status: 'authenticated',
          session: {
            isAuthenticated: true,
            server: credentials.server,
            username: credentials.username,
          },
        });
      } else {
        set({
          status: 'failed',
          error: result.error || 'Anmeldung fehlgeschlagen',
        });
      }
      return result;
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : 'Unbekannter Fehler';
      set({
        status: 'failed',
        error: errorMessage,
      });
      return { ok: false, error: errorMessage };
    }
  },

  logout: async () => {
    set({ status: 'logging_out' });
    try {
      await window.swyxApi.logout();
      set({
        status: 'idle',
        session: null,
        error: undefined,
      });
    } catch (err) {
      // Log error but still clear local state
      console.error('Logout error:', err);
      set({
        status: 'idle',
        session: null,
        error: undefined,
      });
    }
  },

  refreshSessionStatus: async () => {
    try {
      const session = await window.swyxApi.getSessionStatus();
      set({
        status: session.isAuthenticated ? 'authenticated' : 'idle',
        session: session.isAuthenticated ? session : null,
      });
    } catch (err) {
      console.error('Failed to refresh session status:', err);
    }
  },

  clearError: () => {
    set({ error: undefined });
  },
}));
