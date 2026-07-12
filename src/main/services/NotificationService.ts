import { Notification, BrowserWindow } from 'electron';
import { CallDetails } from '../../shared/types';

export class NotificationService {
  private readonly getMainWindow: () => BrowserWindow | null;

  constructor(getMainWindow: () => BrowserWindow | null) {
    this.getMainWindow = getMainWindow;
  }

  showIncomingCall(call: CallDetails): void {
    if (!Notification.isSupported()) return;

    const callerDisplay = call.callerName
      ? `${call.callerName} (${call.callerNumber})`
      : call.callerNumber;

    const notification = new Notification({
      title: 'Eingehender Anruf',
      body: callerDisplay,
      urgency: 'critical',
      timeoutType: 'never',
      actions: [
        { type: 'button', text: 'Annehmen' },
        { type: 'button', text: 'Ablehnen' },
      ],
    });

    notification.on('click', () => {
      const win = this.getMainWindow();
      if (!win) return;
      if (win.isMinimized()) win.restore();
      win.show();
      win.focus();
    });

    notification.on('action', (_event, index) => {
      const win = this.getMainWindow();
      if (!win) return;
      win.webContents.send(
        index === 0 ? 'swyx:answerFromNotification' : 'swyx:rejectFromNotification',
        { lineId: call.lineId }
      );
    });

    notification.show();
  }

  showMissedCall(callerName: string, callerNumber: string): void {
    if (!Notification.isSupported()) return;

    const display = callerName ? `${callerName} (${callerNumber})` : callerNumber;

    const notification = new Notification({
      title: 'Verpasster Anruf',
      body: display,
    });

    notification.on('click', () => {
      const win = this.getMainWindow();
      if (!win) return;
      if (win.isMinimized()) win.restore();
      win.show();
      win.focus();
      win.webContents.send('swyx:openHistory');
    });

    notification.show();
  }
}
