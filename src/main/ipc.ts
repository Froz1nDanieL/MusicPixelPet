import { app, ipcMain, type BrowserWindow } from "electron";
import type { AppSettings } from "../shared/settings";
import { getSettings, updateSettings } from "./store";
import { applySettingsSideEffects } from "./services/settings-service";
import { MediaHelperService } from "./services/helper-service";

interface IpcOptions {
  window: BrowserWindow;
  mediaHelper: MediaHelperService;
  onSettingsChanged: (settings: AppSettings) => void;
  openSettings: () => void;
  closeSettings: () => void;
  quit: () => void;
}

export function registerIpcHandlers(options: IpcOptions): void {
  ipcMain.handle("settings:get", () => getSettings());

  ipcMain.handle("settings:update", async (_, patch: Partial<AppSettings>) => {
    const nextSettings = updateSettings(patch);
    applySettingsSideEffects(options.window, nextSettings);
    options.mediaHelper.configure(nextSettings.playerWhitelist);
    options.onSettingsChanged(nextSettings);
    return nextSettings;
  });

  ipcMain.handle("media:getSnapshot", () => {
    return options.mediaHelper.getSnapshot();
  });

  ipcMain.handle("media:playPause", () => {
    options.mediaHelper.playPause();
  });

  ipcMain.handle("media:next", () => {
    options.mediaHelper.next();
  });

  ipcMain.handle("media:previous", () => {
    options.mediaHelper.previous();
  });

  ipcMain.handle("media:adjustVolume", (_, delta: number) => {
    const settings = getSettings();

    if (!settings.wheelVolumeEnabled) {
      return;
    }

    options.mediaHelper.adjustVolume(delta);
  });

  ipcMain.handle("app:quit", () => {
    options.quit();
    app.quit();
  });

  ipcMain.handle("ui:openSettings", () => {
    options.openSettings();
  });

  ipcMain.handle("ui:closeSettings", () => {
    options.closeSettings();
  });
}
