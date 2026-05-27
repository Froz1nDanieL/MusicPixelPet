import type { MediaSnapshot } from "./media";
import type { AppSettings } from "./settings";

export type MenuAction = "open-settings";

export interface MusicPetApi {
  settings: {
    get: () => Promise<AppSettings>;
    update: (patch: Partial<AppSettings>) => Promise<AppSettings>;
    onChange: (listener: (settings: AppSettings) => void) => () => void;
  };
  media: {
    getSnapshot: () => Promise<MediaSnapshot>;
    playPause: () => Promise<void>;
    next: () => Promise<void>;
    previous: () => Promise<void>;
    adjustVolume: (delta: number) => Promise<void>;
    onSnapshot: (listener: (snapshot: MediaSnapshot) => void) => () => void;
  };
  ui: {
    openSettings: () => Promise<void>;
    closeSettings: () => Promise<void>;
    onMenuAction: (listener: (action: MenuAction) => void) => () => void;
    quit: () => Promise<void>;
  };
}

declare global {
  interface Window {
    musicPet: MusicPetApi;
  }
}

export {};
