import path from "node:path";
import { fileURLToPath } from "node:url";
import { BrowserWindow, screen } from "electron";
import type { AppSettings, WindowBounds } from "../shared/settings";

const currentDirectory = path.dirname(fileURLToPath(import.meta.url));

export function createMainWindow(settings: AppSettings): BrowserWindow {
  const bounds = ensureVisibleBounds(settings.windowBounds);

  return new BrowserWindow({
    width: bounds.width,
    height: bounds.height,
    x: bounds.x,
    y: bounds.y,
    minWidth: bounds.width,
    minHeight: bounds.height,
    maxWidth: bounds.width,
    maxHeight: bounds.height,
    frame: false,
    transparent: true,
    resizable: false,
    hasShadow: false,
    alwaysOnTop: settings.alwaysOnTop,
    skipTaskbar: true,
    fullscreenable: false,
    show: false,
    backgroundColor: "#00000000",
    webPreferences: {
      preload: path.join(currentDirectory, "preload.mjs"),
      contextIsolation: true,
      nodeIntegration: false
    }
  });
}

export function ensureVisibleBounds(windowBounds: WindowBounds): WindowBounds {
  const display = screen.getDisplayNearestPoint({
    x: windowBounds.x,
    y: windowBounds.y
  });
  const { workArea } = display;
  const maxX = workArea.x + Math.max(0, workArea.width - windowBounds.width);
  const maxY = workArea.y + Math.max(0, workArea.height - windowBounds.height);

  return {
    ...windowBounds,
    x: clamp(windowBounds.x, workArea.x, maxX),
    y: clamp(windowBounds.y, workArea.y, maxY)
  };
}

function clamp(value: number, min: number, max: number): number {
  return Math.min(Math.max(value, min), max);
}
