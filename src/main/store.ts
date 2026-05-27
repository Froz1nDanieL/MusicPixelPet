import Store from "electron-store";
import { DEFAULT_SETTINGS, PET_WINDOW_SIZE, type AppSettings, type WindowBounds } from "../shared/settings";

interface StoreShape {
  settings: AppSettings;
}

let store: Store<StoreShape> | null = null;

function getStore(): Store<StoreShape> {
  if (!store) {
    store = new Store<StoreShape>({
      name: "music-pixel-pet",
      defaults: {
        settings: DEFAULT_SETTINGS
      }
    });
  }

  return store;
}

function mergeSettings(current: AppSettings, patch: Partial<AppSettings>): AppSettings {
  return {
    ...current,
    ...patch,
    windowBounds: normalizeWindowBounds(patch.windowBounds ? { ...current.windowBounds, ...patch.windowBounds } : current.windowBounds),
    playerWhitelist: patch.playerWhitelist ?? current.playerWhitelist,
    musicRules: patch.musicRules ?? current.musicRules
  };
}

export function getSettings(): AppSettings {
  const settings = getStore().get("settings");
  const normalizedSettings = {
    ...settings,
    windowBounds: normalizeWindowBounds(settings.windowBounds)
  };

  getStore().set("settings", normalizedSettings);
  return normalizedSettings;
}

export function updateSettings(patch: Partial<AppSettings>): AppSettings {
  const nextSettings = mergeSettings(getSettings(), patch);
  getStore().set("settings", nextSettings);
  return nextSettings;
}

export function updateWindowBounds(windowBounds: WindowBounds): AppSettings {
  return updateSettings({ windowBounds });
}

function normalizeWindowBounds(windowBounds: WindowBounds): WindowBounds {
  return {
    ...windowBounds,
    width: PET_WINDOW_SIZE.width,
    height: PET_WINDOW_SIZE.height
  };
}
