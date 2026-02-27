import { app, BrowserWindow, shell } from 'electron'
import { join } from 'path'
import { electronApp, optimizer, is } from '@electron-toolkit/utils'
import { BridgeManager } from './bridge/BridgeManager'
import { TrayIcon } from './tray'
import { registerIpcHandlers } from './ipc/handlers'
import { SettingsStore } from './services/SettingsStore'
import { NotificationService } from './services/NotificationService'
import { BridgeState, CallDetails, LineState } from '../shared/types'

let mainWindow: BrowserWindow | null = null
let isQuitting = false
const bridgeManager = new BridgeManager()
const settingsStore = new SettingsStore()
let trayIcon: TrayIcon | null = null

function getMainWindow(): BrowserWindow | null {
  return mainWindow
}

const notificationService = new NotificationService(getMainWindow)

const gotLock = app.requestSingleInstanceLock()
if (!gotLock) {
  app.quit()
}

app.on('second-instance', () => {
  if (mainWindow) {
    if (mainWindow.isMinimized()) mainWindow.restore()
    mainWindow.show()
    mainWindow.focus()
  }
})

function createMainWindow(): BrowserWindow {
  const savedBounds = settingsStore.get('windowBounds')

  const win = new BrowserWindow({
    width: savedBounds?.width ?? 1100,
    height: savedBounds?.height ?? 750,
    x: savedBounds?.x,
    y: savedBounds?.y,
    minWidth: 900,
    minHeight: 600,
    show: false,
    frame: true,
    titleBarStyle: process.platform === 'darwin' ? 'hiddenInset' : 'default',
    icon: join(__dirname, '../../resources/icon.ico'),
    webPreferences: {
      preload: join(__dirname, '../preload/index.js'),
      sandbox: false,
      contextIsolation: true,
      nodeIntegration: false,
    },
  })

  win.on('ready-to-show', () => {
    if (!settingsStore.get('startMinimized')) {
      win.show()
    }
  })

  win.on('close', (event) => {
    if (settingsStore.get('closeToTray') !== false && !isQuitting) {
      event.preventDefault()
      win.hide()
    } else {
      settingsStore.set('windowBounds', win.getBounds())
      settingsStore.flush()
    }
  })

  win.on('resized', () => {
    settingsStore.set('windowBounds', win.getBounds())
  })

  win.on('moved', () => {
    settingsStore.set('windowBounds', win.getBounds())
  })

  win.webContents.setWindowOpenHandler(({ url }) => {
    shell.openExternal(url)
    return { action: 'deny' }
  })

  if (is.dev && process.env['ELECTRON_RENDERER_URL']) {
    win.loadURL(process.env['ELECTRON_RENDERER_URL'])
  } else {
    win.loadFile(join(__dirname, '../renderer/index.html'))
  }

  return win
}

app.on('before-quit', () => {
  isQuitting = true
  bridgeManager.stop()
  settingsStore.flush()
})

// Register IPC handlers early (before whenReady) so they are available
// as soon as the renderer makes its first invoke calls.
registerIpcHandlers(bridgeManager, settingsStore, getMainWindow)

app.whenReady().then(() => {
  electronApp.setAppUserModelId('com.ralle197.swyit')

  app.on('browser-window-created', (_, window) => {
    optimizer.watchWindowShortcuts(window)
  })

  mainWindow = createMainWindow()

  trayIcon = new TrayIcon(mainWindow)
  trayIcon.init()

  bridgeManager.on('stateChanged', (state: BridgeState) => {
    trayIcon?.setStatus(state)
  })

  bridgeManager.on('event', (evt) => {
    if (evt.method === 'incomingCall' && evt.params) {
      notificationService.showIncomingCall(evt.params as CallDetails)
    }

    if (evt.method === 'lineStateChanged' && evt.params) {
      const params = evt.params as Record<string, unknown>
      const state = params['state'] as string
      if (state === LineState.Terminated) {
        notificationService.showMissedCall(
          (params['callerName'] as string) ?? 'Unbekannt',
          (params['callerNumber'] as string) ?? ''
        )
      }
    }
  })

  bridgeManager.on('error', (err) => {
    console.error('[Bridge Error]', err.message)
  })

  bridgeManager.start()

  app.on('activate', () => {
    if (BrowserWindow.getAllWindows().length === 0) {
      mainWindow = createMainWindow()
    } else {
      mainWindow?.show()
    }
  })
})

app.on('window-all-closed', () => {
  if (process.platform !== 'darwin') {
    trayIcon?.destroy()
    app.quit()
  }
})
