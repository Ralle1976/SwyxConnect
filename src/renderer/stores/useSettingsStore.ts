import { create } from 'zustand';
import { persist } from 'zustand/middleware';
import { AppSettings } from '../types/swyx';

type Theme = 'light' | 'dark' | 'system';

interface SettingsStoreState {
  // Alle AppSettings-Felder
  theme: Theme;
  sidebarCollapsed: boolean;
  startMinimized: boolean;
  closeToTray: boolean;
  audioInputDevice: string;
  audioOutputDevice: string;
  audioInputVolume: number;
  audioOutputVolume: number;
  teamsEnabled: boolean;
  numberOfLines: number;
  externalLinePrefix: string;
  externalLinePrefixEnabled: boolean;
  pbxServer: string;
  cdsHost: string;
  cdsPort: number;
  cdsUsername: string;
  // Actions
  setTheme: (theme: Theme) => void;
  toggleSidebar: () => void;
  setSidebarCollapsed: (collapsed: boolean) => void;
  setStartMinimized: (value: boolean) => void;
  setCloseToTray: (value: boolean) => void;
  setAudioInputDevice: (device: string) => void;
  setAudioOutputDevice: (device: string) => void;
  setAudioInputVolume: (volume: number) => void;
  setAudioOutputVolume: (volume: number) => void;
  setTeamsEnabled: (enabled: boolean) => void;
  setNumberOfLines: (count: number) => void;
  setExternalLinePrefix: (prefix: string) => void;
  setExternalLinePrefixEnabled: (enabled: boolean) => void;
  setPbxServer: (server: string) => void;
  setCdsHost: (host: string) => void;
  setCdsPort: (port: number) => void;
  setCdsUsername: (username: string) => void;
  // Sync mit Main Process
  loadFromMain: () => Promise<void>;
  saveToMain: (patch: Partial<AppSettings>) => Promise<void>;
}

export const useSettingsStore = create<SettingsStoreState>()(
  persist(
    (set, get) => ({
      theme: 'system',
      sidebarCollapsed: false,
      startMinimized: false,
      closeToTray: true,
      audioInputDevice: 'default',
      audioOutputDevice: 'default',
      audioInputVolume: 80,
      audioOutputVolume: 80,
      teamsEnabled: false,
      numberOfLines: 2,
      externalLinePrefix: '0',
      externalLinePrefixEnabled: true,
      pbxServer: '',
      cdsHost: '',
      cdsPort: 9094,
      cdsUsername: '',

      setTheme: (theme) => {
        set({ theme });
        get().saveToMain({ theme });
      },
      toggleSidebar: () => {
        const collapsed = !get().sidebarCollapsed;
        set({ sidebarCollapsed: collapsed });
        get().saveToMain({ sidebarCollapsed: collapsed });
      },
      setSidebarCollapsed: (collapsed) => {
        set({ sidebarCollapsed: collapsed });
        get().saveToMain({ sidebarCollapsed: collapsed });
      },
      setStartMinimized: (value) => {
        set({ startMinimized: value });
        get().saveToMain({ startMinimized: value });
      },
      setCloseToTray: (value) => {
        set({ closeToTray: value });
        get().saveToMain({ closeToTray: value });
      },
      setAudioInputDevice: (device) => {
        set({ audioInputDevice: device });
        get().saveToMain({ audioInputDevice: device });
      },
      setAudioOutputDevice: (device) => {
        set({ audioOutputDevice: device });
        get().saveToMain({ audioOutputDevice: device });
      },
      setAudioInputVolume: (volume) => {
        set({ audioInputVolume: volume });
        get().saveToMain({ audioInputVolume: volume });
      },
      setAudioOutputVolume: (volume) => {
        set({ audioOutputVolume: volume });
        get().saveToMain({ audioOutputVolume: volume });
      },
      setTeamsEnabled: (enabled) => {
        set({ teamsEnabled: enabled });
        get().saveToMain({ teamsEnabled: enabled });
      },
      setNumberOfLines: (count) => {
        set({ numberOfLines: count });
        get().saveToMain({ numberOfLines: count });
        // Sofort an Bridge/COM senden — Bridge emittiert lineStateChanged Event
        // welches automatisch die Lines im UI aktualisiert
        if (window.swyxApi?.setNumberOfLines) {
          window.swyxApi.setNumberOfLines(count).catch(() => {});
        }
      },
      setExternalLinePrefix: (prefix) => {
        set({ externalLinePrefix: prefix });
        get().saveToMain({ externalLinePrefix: prefix });
      },
      setExternalLinePrefixEnabled: (enabled) => {
        set({ externalLinePrefixEnabled: enabled });
        get().saveToMain({ externalLinePrefixEnabled: enabled });
      },
      setPbxServer: (server) => {
        set({ pbxServer: server });
        get().saveToMain({ pbxServer: server });
      },
      setCdsHost: (host) => {
        set({ cdsHost: host });
        get().saveToMain({ cdsHost: host });
      },
      setCdsPort: (port) => {
        set({ cdsPort: port });
        get().saveToMain({ cdsPort: port });
      },
      setCdsUsername: (username) => {
        set({ cdsUsername: username });
        get().saveToMain({ cdsUsername: username });
      },

      loadFromMain: async () => {
        if (!window.swyxApi) return;
        try {
          const settings = await window.swyxApi.getSettings();
          if (settings) {
            set({
              theme: settings.theme ?? 'system',
              sidebarCollapsed: settings.sidebarCollapsed ?? false,
              startMinimized: settings.startMinimized ?? false,
              closeToTray: settings.closeToTray ?? true,
              audioInputDevice: settings.audioInputDevice ?? 'default',
              audioOutputDevice: settings.audioOutputDevice ?? 'default',
              audioInputVolume: settings.audioInputVolume ?? 80,
              audioOutputVolume: settings.audioOutputVolume ?? 80,
              teamsEnabled: settings.teamsEnabled ?? false,
              numberOfLines: settings.numberOfLines ?? 2,
              externalLinePrefix: settings.externalLinePrefix ?? '0',
              externalLinePrefixEnabled: settings.externalLinePrefixEnabled ?? true,
              pbxServer: settings.pbxServer ?? '',
              cdsHost: settings.cdsHost ?? '',
              cdsPort: settings.cdsPort ?? 9094,
              cdsUsername: settings.cdsUsername ?? '',
            });
          }
        } catch { /* Bridge noch nicht bereit */ }
      },

      saveToMain: async (patch) => {
        if (!window.swyxApi) return;
        try {
          await window.swyxApi.setSettings(patch);
        } catch { /* ignore */ }
      },
    }),
    {
      name: 'swyx-ui-settings',
      partialize: (state) => ({
        theme: state.theme,
        sidebarCollapsed: state.sidebarCollapsed,
        startMinimized: state.startMinimized,
        closeToTray: state.closeToTray,
        audioInputDevice: state.audioInputDevice,
        audioOutputDevice: state.audioOutputDevice,
        audioInputVolume: state.audioInputVolume,
        audioOutputVolume: state.audioOutputVolume,
        teamsEnabled: state.teamsEnabled,
        numberOfLines: state.numberOfLines,
        externalLinePrefix: state.externalLinePrefix,
        externalLinePrefixEnabled: state.externalLinePrefixEnabled,
        pbxServer: state.pbxServer,
        cdsHost: state.cdsHost,
        cdsPort: state.cdsPort,
        cdsUsername: state.cdsUsername,
      }),
    }
  )
);

export function applyTheme(theme: Theme): void {
  const root = document.documentElement;
  if (theme === 'dark') {
    root.classList.add('dark');
  } else if (theme === 'light') {
    root.classList.remove('dark');
  } else {
    const prefersDark = window.matchMedia('(prefers-color-scheme: dark)').matches;
    root.classList.toggle('dark', prefersDark);
  }
}
