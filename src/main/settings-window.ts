import path from "node:path";
import { fileURLToPath } from "node:url";
import { app, BrowserWindow, screen } from "electron";
import type { AppSettings } from "../shared/settings";

const currentDirectory = path.dirname(fileURLToPath(import.meta.url));
const SETTINGS_WINDOW_WIDTH = 340;
const SETTINGS_WINDOW_HEIGHT = 520;
const SETTINGS_WINDOW_GAP = 10;

export function openSettingsWindow(owner: BrowserWindow, settings: AppSettings): BrowserWindow {
  const existingWindow = BrowserWindow.getAllWindows().find((window) => window.getTitle() === "Music Pixel Pet Settings");

  if (existingWindow && !existingWindow.isDestroyed()) {
    positionSettingsWindow(existingWindow, owner);
    existingWindow.show();
    existingWindow.focus();
    return existingWindow;
  }

  const bounds = getSettingsWindowBounds(owner);
  const settingsWindow = new BrowserWindow({
    ...bounds,
    width: SETTINGS_WINDOW_WIDTH,
    height: SETTINGS_WINDOW_HEIGHT,
    minWidth: SETTINGS_WINDOW_WIDTH,
    minHeight: 420,
    maxWidth: SETTINGS_WINDOW_WIDTH,
    frame: false,
    transparent: false,
    resizable: false,
    show: false,
    title: "Music Pixel Pet Settings",
    parent: owner,
    alwaysOnTop: settings.alwaysOnTop,
    skipTaskbar: true,
    backgroundColor: "#ffffff",
    webPreferences: {
      preload: path.join(currentDirectory, "preload.mjs"),
      contextIsolation: true,
      nodeIntegration: false
    }
  });

  void loadSettingsRenderer(settingsWindow);
  settingsWindow.once("ready-to-show", () => settingsWindow.show());

  return settingsWindow;
}

export function positionSettingsWindow(settingsWindow: BrowserWindow, owner: BrowserWindow): void {
  settingsWindow.setBounds(getSettingsWindowBounds(owner));
}

async function loadSettingsRenderer(settingsWindow: BrowserWindow): Promise<void> {
  const devServerUrl = process.env.VITE_DEV_SERVER_URL;

  if (devServerUrl) {
    await settingsWindow.loadURL(`${devServerUrl}?view=settings`);
    return;
  }

  await settingsWindow.loadFile(resolveRendererHtmlPath(), {
    query: {
      view: "settings"
    }
  });
}

function getSettingsWindowBounds(owner: BrowserWindow): Electron.Rectangle {
  const ownerBounds = owner.getBounds();
  const display = screen.getDisplayMatching(ownerBounds);
  const { workArea } = display;
  const preferredX = ownerBounds.x + ownerBounds.width + SETTINGS_WINDOW_GAP;
  const fallbackX = ownerBounds.x - SETTINGS_WINDOW_WIDTH - SETTINGS_WINDOW_GAP;
  const canFitRight = preferredX + SETTINGS_WINDOW_WIDTH <= workArea.x + workArea.width;
  const x = canFitRight ? preferredX : Math.max(workArea.x, fallbackX);
  const centeredY = ownerBounds.y + Math.round((ownerBounds.height - SETTINGS_WINDOW_HEIGHT) / 2);
  const maxY = workArea.y + Math.max(0, workArea.height - SETTINGS_WINDOW_HEIGHT);

  return {
    x: clamp(x, workArea.x, workArea.x + Math.max(0, workArea.width - SETTINGS_WINDOW_WIDTH)),
    y: clamp(centeredY, workArea.y, maxY),
    width: SETTINGS_WINDOW_WIDTH,
    height: SETTINGS_WINDOW_HEIGHT
  };
}

function resolveRendererHtmlPath(): string {
  if (app.isPackaged) {
    return path.join(app.getAppPath(), "dist", "index.html");
  }

  return path.join(currentDirectory, "../../dist", "index.html");
}

function clamp(value: number, min: number, max: number): number {
  return Math.min(Math.max(value, min), max);
}
