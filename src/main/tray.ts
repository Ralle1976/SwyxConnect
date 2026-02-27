import { Tray, Menu, BrowserWindow, nativeImage, app } from 'electron';
import * as path from 'path';
import { PresenceStatus } from '../shared/types';

const PRESENCE_LABELS: Record<PresenceStatus, string> = {
  [PresenceStatus.Available]: 'Verfügbar',
  [PresenceStatus.Away]: 'Abwesend',
  [PresenceStatus.Busy]: 'Beschäftigt',
  [PresenceStatus.DND]: 'Nicht stören',
  [PresenceStatus.Offline]: 'Offline',
};

export class TrayIcon {
  private tray: Tray | null = null;
  private mainWindow: BrowserWindow;
  private currentPresence: PresenceStatus = PresenceStatus.Offline;
  private currentStatus = 'Getrennt';

  constructor(mainWindow: BrowserWindow) {
    this.mainWindow = mainWindow;
  }

  init(): void {
    try {
      const iconPath = this.getIconPath();
      this.tray = new Tray(iconPath);
    } catch {
      // Fallback: empty 1x1 image so the rest of the app still boots
      this.tray = new Tray(nativeImage.createEmpty());
    }
    this.tray.setToolTip('Swyx Softphone');
    this.updateContextMenu();

    this.tray.on('double-click', () => {
      this.showWindow();
    });

    this.tray.on('click', () => {
      if (process.platform !== 'darwin') {
        this.showWindow();
      }
    });
  }

  setPresence(presence: PresenceStatus): void {
    this.currentPresence = presence;
    this.updateTooltip();
    this.updateContextMenu();
  }

  setStatus(status: string): void {
    this.currentStatus = status;
    this.updateTooltip();
    this.updateContextMenu();
  }

  destroy(): void {
    this.tray?.destroy();
    this.tray = null;
  }

  private showWindow(): void {
    if (!this.mainWindow) return;
    if (this.mainWindow.isMinimized()) this.mainWindow.restore();
    this.mainWindow.show();
    this.mainWindow.focus();
  }

  private updateTooltip(): void {
    const label = PRESENCE_LABELS[this.currentPresence];
    this.tray?.setToolTip(`Swyx Softphone — ${label} — ${this.currentStatus}`);
  }

  private updateContextMenu(): void {
    if (!this.tray) return;

    const label = PRESENCE_LABELS[this.currentPresence];
    const menu = Menu.buildFromTemplate([
      {
        label: 'Swyx Softphone',
        enabled: false,
      },
      { type: 'separator' },
      {
        label: `Status: ${label}`,
        enabled: false,
      },
      {
        label: `Bridge: ${this.currentStatus}`,
        enabled: false,
      },
      { type: 'separator' },
      {
        label: 'Fenster anzeigen',
        click: () => this.showWindow(),
      },
      {
        label: 'Fenster verstecken',
        click: () => this.mainWindow?.hide(),
      },
      { type: 'separator' },
      {
        label: 'Beenden',
        click: () => {
          app.quit();
        },
      },
    ]);

    this.tray.setContextMenu(menu);
  }

  private getIconPath(): string {
    const iconName =
      process.platform === 'darwin'
        ? 'iconTemplate.png'
        : process.platform === 'win32'
          ? 'icon.ico'
          : 'icon.png';

    const resourcesPath = app.isPackaged
      ? process.resourcesPath
      : path.join(app.getAppPath(), 'resources');

    return path.join(resourcesPath, 'icons', iconName);
  }
}
