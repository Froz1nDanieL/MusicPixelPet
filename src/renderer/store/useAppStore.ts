import { create } from "zustand";
import { createDisconnectedSnapshot, type MediaSnapshot } from "@shared/media";
import { DEFAULT_SETTINGS, type AppSettings } from "@shared/settings";

interface AppStore {
  settings: AppSettings;
  media: MediaSnapshot;
  isReady: boolean;
  isSettingsOpen: boolean;
  initialize: (settings: AppSettings, media: MediaSnapshot) => void;
  setMedia: (media: MediaSnapshot) => void;
  setSettings: (settings: AppSettings) => void;
  saveSettings: (patch: Partial<AppSettings>) => Promise<void>;
  openSettings: () => void;
  closeSettings: () => void;
}

export const useAppStore = create<AppStore>((set, get) => ({
  settings: DEFAULT_SETTINGS,
  media: createDisconnectedSnapshot(),
  isReady: false,
  isSettingsOpen: false,
  initialize: (settings, media) => {
    set({
      settings,
      media,
      isReady: true
    });
  },
  setMedia: (media) => {
    set({ media });
  },
  setSettings: (settings) => {
    set({ settings });
  },
  saveSettings: async (patch) => {
    const nextSettings = await window.musicPet.settings.update(patch);
    get().setSettings(nextSettings);
  },
  openSettings: () => {
    set({ isSettingsOpen: true });
  },
  closeSettings: () => {
    set({ isSettingsOpen: false });
  }
}));
