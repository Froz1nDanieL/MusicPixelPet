import { app, type BrowserWindow } from "electron";
import type { AppSettings } from "../../shared/settings";

export function applySettingsSideEffects(window: BrowserWindow, settings: AppSettings): void {
  window.setAlwaysOnTop(settings.alwaysOnTop, "screen-saver");
  app.setLoginItemSettings({
    openAtLogin: settings.autoLaunch
  });
}
