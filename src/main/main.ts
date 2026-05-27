import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { app, BrowserWindow, type Tray } from "electron";
import { createDisconnectedSnapshot } from "../shared/media";
import { getSettings, updateSettings, updateWindowBounds } from "./store";
import { createMainWindow, ensureVisibleBounds } from "./window";
import { createAppTray } from "./tray";
import { registerIpcHandlers } from "./ipc";
import { MediaHelperService } from "./services/helper-service";
import { applySettingsSideEffects } from "./services/settings-service";
import { debounce } from "./services/debounce";
import { openSettingsWindow, positionSettingsWindow } from "./settings-window";

let mainWindow: BrowserWindow | null = null;
let settingsWindow: BrowserWindow | null = null;
let tray: Tray | null = null;
let quitting = false;
const currentDirectory = path.dirname(fileURLToPath(import.meta.url));

configureRuntimePaths();

async function bootstrap(): Promise<void> {
  await app.whenReady();

  app.setAppUserModelId("com.musicpixelpet.app");

  const settings = getSettings();
  const visibleBounds = ensureVisibleBounds(settings.windowBounds);

  if (visibleBounds.x !== settings.windowBounds.x || visibleBounds.y !== settings.windowBounds.y) {
    updateSettings({
      windowBounds: visibleBounds
    });
  }

  mainWindow = createMainWindow({
    ...settings,
    windowBounds: visibleBounds
  });

  const devServerUrl = process.env.VITE_DEV_SERVER_URL;

  if (devServerUrl) {
    await mainWindow.loadURL(devServerUrl);
  } else {
    await mainWindow.loadFile(resolveRendererHtmlPath());
  }

  applySettingsSideEffects(mainWindow, settings);

  const mediaHelper = new MediaHelperService();

  mediaHelper.on("snapshot", (snapshot) => {
    if (!mainWindow || mainWindow.isDestroyed()) {
      return;
    }

    mainWindow.webContents.send("media:snapshot", snapshot);
  });

  registerIpcHandlers({
    window: mainWindow,
    mediaHelper,
    onSettingsChanged: (nextSettings) => {
      mainWindow?.webContents.send("settings:changed", nextSettings);
      settingsWindow?.setAlwaysOnTop(nextSettings.alwaysOnTop, "screen-saver");
      settingsWindow?.webContents.send("settings:changed", nextSettings);
    },
    openSettings: () => {
      if (!mainWindow || mainWindow.isDestroyed()) {
        return;
      }

      settingsWindow = openSettingsWindow(mainWindow, getSettings());
      settingsWindow.once("closed", () => {
        settingsWindow = null;
      });
    },
    closeSettings: () => {
      settingsWindow?.close();
      settingsWindow = null;
    },
    quit: () => {
      quitting = true;
    }
  });

  const persistBounds = debounce(() => {
    if (!mainWindow) {
      return;
    }

    const bounds = mainWindow.getBounds();
    updateWindowBounds({
      x: bounds.x,
      y: bounds.y,
      width: bounds.width,
      height: bounds.height
    });
  }, 200);
  const followMainWindow = debounce(() => {
    if (!mainWindow || !settingsWindow || settingsWindow.isDestroyed()) {
      return;
    }

    positionSettingsWindow(settingsWindow, mainWindow);
  }, 40);

  mainWindow.on("move", persistBounds);
  mainWindow.on("move", followMainWindow);
  mainWindow.on("close", (event) => {
    if (quitting) {
      return;
    }

    event.preventDefault();
    mainWindow?.hide();
  });

  mainWindow.once("ready-to-show", () => {
    mainWindow?.show();
  });

  tray = createAppTray({
    window: mainWindow,
    isAlwaysOnTop: () => getSettings().alwaysOnTop,
    onOpenSettings: () => {
      if (mainWindow && !mainWindow.isDestroyed()) {
        settingsWindow = openSettingsWindow(mainWindow, getSettings());
      }
    },
    onToggleAlwaysOnTop: () => {
      const nextSettings = updateSettings({
        alwaysOnTop: !getSettings().alwaysOnTop
      });

      if (mainWindow) {
        applySettingsSideEffects(mainWindow, nextSettings);
        mainWindow.webContents.send("settings:changed", nextSettings);
        settingsWindow?.setAlwaysOnTop(nextSettings.alwaysOnTop, "screen-saver");
        settingsWindow?.webContents.send("settings:changed", nextSettings);
      }
    },
    onQuit: () => {
      quitting = true;
      app.quit();
    }
  });

  await mediaHelper.start(getSettings());

  if (!mediaHelper.getSnapshot().connected) {
    mainWindow.webContents.send("media:snapshot", createDisconnectedSnapshot(mediaHelper.getSnapshot().errorMessage));
  }

  app.on("activate", () => {
    mainWindow?.show();
  });

  app.on("before-quit", () => {
    quitting = true;
    settingsWindow?.close();
    mediaHelper.stop();
    tray = null;
  });
}

bootstrap();

function configureRuntimePaths(): void {
  const runtimeRoot = app.isPackaged
    ? path.join(app.getPath("appData"), "Music Pixel Pet")
    : path.join(process.cwd(), ".runtime");
  const userDataPath = path.join(runtimeRoot, "user-data");
  const sessionDataPath = path.join(runtimeRoot, "session-data");
  const crashDumpsPath = path.join(runtimeRoot, "crash-dumps");
  const cachePath = path.join(sessionDataPath, "Cache");

  for (const targetPath of [runtimeRoot, userDataPath, sessionDataPath, crashDumpsPath, cachePath]) {
    fs.mkdirSync(targetPath, { recursive: true });
  }

  app.setPath("userData", userDataPath);
  app.setPath("sessionData", sessionDataPath);
  app.setPath("crashDumps", crashDumpsPath);
  app.commandLine.appendSwitch("disk-cache-dir", cachePath);
}

function resolveRendererHtmlPath(): string {
  if (app.isPackaged) {
    return path.join(app.getAppPath(), "dist", "index.html");
  }

  return path.join(currentDirectory, "../../dist", "index.html");
}
