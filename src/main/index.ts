import { app, BrowserWindow, shell, dialog } from 'electron'
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

  // ---------- FENSTER SICHER ANZEIGEN ----------

  let windowShown = false

  const showWindow = (): void => {
    if (windowShown || win.isDestroyed()) return
    windowShown = true
    win.show()
    win.focus()
    console.log('[Main] Fenster angezeigt.')
  }

  // Normaler Pfad: Fenster zeigen wenn Renderer bereit ist
  win.on('ready-to-show', () => {
    console.log('[Main] ready-to-show gefeuert.')
    if (!settingsStore.get('startMinimized')) {
      showWindow()
    }
  })

  // Fallback: Fenster nach 5 Sekunden ERZWINGEN, falls ready-to-show nie kommt
  const forceShowTimer = setTimeout(() => {
    if (!windowShown && !win.isDestroyed()) {
      console.warn('[Main] WARNUNG: ready-to-show hat nach 5s nicht gefeuert. Fenster wird erzwungen.')
      showWindow()
    }
  }, 5000)

  // Timer aufräumen wenn Fenster geschlossen wird
  win.on('closed', () => {
    clearTimeout(forceShowTimer)
  })

  // ---------- RENDERER FEHLER-HANDLING ----------

  win.webContents.on('did-fail-load', (_event, errorCode, errorDescription, validatedURL) => {
    console.error(`[Main] Renderer konnte nicht laden: ${errorCode} ${errorDescription} URL: ${validatedURL}`)
    // Trotzdem anzeigen damit der User den Fehler sieht
    showWindow()
  })

  win.webContents.on('render-process-gone', (_event, details) => {
    console.error(`[Main] Renderer-Prozess abgestürzt: ${details.reason}`)
    dialog.showErrorBox(
      'SwyxConnect - Renderer Fehler',
      `Der Renderer ist abgestürzt (${details.reason}). Die App wird neu geladen.`
    )
    if (!win.isDestroyed()) {
      win.reload()
    }
  })

  win.webContents.on('unresponsive', () => {
    console.warn('[Main] Renderer reagiert nicht mehr.')
  })

  win.webContents.on('responsive', () => {
    console.log('[Main] Renderer reagiert wieder.')
  })

  // DevTools öffnen bei Fehler (nur im Dev-Modus)
  win.webContents.on('console-message', (_event, level, message) => {
    // level: 0=verbose, 1=info, 2=warning, 3=error
    if (level >= 3) {
      console.error(`[Renderer Error] ${message}`)
    }
  })

  // ---------- FENSTER-EVENTS ----------

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

  // ---------- RENDERER LADEN ----------

  const rendererURL = is.dev ? process.env['ELECTRON_RENDERER_URL'] : undefined

  if (rendererURL) {
    console.log(`[Main] Lade Renderer von URL: ${rendererURL}`)
    win.loadURL(rendererURL).catch((err) => {
      console.error(`[Main] loadURL fehlgeschlagen: ${err.message}`)
      showWindow()
    })
  } else {
    const htmlPath = join(__dirname, '../renderer/index.html')
    console.log(`[Main] Lade Renderer von Datei: ${htmlPath}`)
    win.loadFile(htmlPath).catch((err) => {
      console.error(`[Main] loadFile fehlgeschlagen: ${err.message}`)
      showWindow()
    })
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
  electronApp.setAppUserModelId('com.ralle197.swyxconnect')

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

  console.log('[Main] Bridge wird gestartet...')
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
