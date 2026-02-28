import { app } from 'electron';
import * as fs from 'fs';
import * as path from 'path';
import { AppSettings } from '../../shared/types';

const DEFAULTS: AppSettings = {
  theme: 'system',
  audioInputDevice: 'default',
  audioOutputDevice: 'default',
  audioInputVolume: 80,
  audioOutputVolume: 80,
  teamsEnabled: false,
  teamsTokens: null,
  windowBounds: null,
  sidebarCollapsed: false,
  pluginsDirectory: '',
  startMinimized: false,
  closeToTray: true,
  numberOfLines: 2,
  externalLinePrefix: '0',
  externalLinePrefixEnabled: true,
  pbxServer: '',
  cdsHost: '',
  cdsPort: 9094,
  cdsUsername: '',
};
export class SettingsStore {
  private data: AppSettings;
  private readonly filePath: string;
  private saveTimer: ReturnType<typeof setTimeout> | null = null;

  constructor() {
    this.filePath = path.join(app.getPath('userData'), 'settings.json');
    this.data = this.load();
  }

  get<K extends keyof AppSettings>(key: K): AppSettings[K] {
    return this.data[key];
  }

  set<K extends keyof AppSettings>(key: K, value: AppSettings[K]): void {
    this.data[key] = value;
    this.scheduleSave();
  }

  patch(partial: Partial<AppSettings>): void {
    this.data = { ...this.data, ...partial };
    this.scheduleSave();
  }

  getAll(): AppSettings {
    return { ...this.data };
  }

  private load(): AppSettings {
    try {
      if (fs.existsSync(this.filePath)) {
        const raw = fs.readFileSync(this.filePath, 'utf8');
        const parsed = JSON.parse(raw) as Partial<AppSettings>;
        return { ...DEFAULTS, ...parsed };
      }
    } catch {
      // fallback to defaults
    }
    return { ...DEFAULTS };
  }

  private scheduleSave(): void {
    if (this.saveTimer) clearTimeout(this.saveTimer);
    this.saveTimer = setTimeout(() => {
      this.flush();
    }, 500);
  }

  flush(): void {
    try {
      const dir = path.dirname(this.filePath);
      if (!fs.existsSync(dir)) fs.mkdirSync(dir, { recursive: true });
      fs.writeFileSync(this.filePath, JSON.stringify(this.data, null, 2), 'utf8');
    } catch {
      // ignore write errors — settings are non-critical
    }
  }
}
