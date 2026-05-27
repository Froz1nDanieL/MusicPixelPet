import { contextBridge, ipcRenderer } from "electron";
import type { MusicPetApi } from "../shared/ipc";
import type { AppSettings } from "../shared/settings";
import type { MediaSnapshot } from "../shared/media";

const api: MusicPetApi = {
  settings: {
    get: () => ipcRenderer.invoke("settings:get") as Promise<AppSettings>,
    update: (patch) => ipcRenderer.invoke("settings:update", patch) as Promise<AppSettings>,
    onChange: (listener) => {
      const wrappedListener = (_event: Electron.IpcRendererEvent, settings: AppSettings) => listener(settings);
      ipcRenderer.on("settings:changed", wrappedListener);

      return () => {
        ipcRenderer.off("settings:changed", wrappedListener);
      };
    }
  },
  media: {
    getSnapshot: () => ipcRenderer.invoke("media:getSnapshot") as Promise<MediaSnapshot>,
    playPause: () => ipcRenderer.invoke("media:playPause") as Promise<void>,
    next: () => ipcRenderer.invoke("media:next") as Promise<void>,
    previous: () => ipcRenderer.invoke("media:previous") as Promise<void>,
    adjustVolume: (delta: number) => ipcRenderer.invoke("media:adjustVolume", delta) as Promise<void>,
    onSnapshot: (listener) => {
      const wrappedListener = (_event: Electron.IpcRendererEvent, snapshot: MediaSnapshot) => listener(snapshot);
      ipcRenderer.on("media:snapshot", wrappedListener);

      return () => {
        ipcRenderer.off("media:snapshot", wrappedListener);
      };
    }
  },
  ui: {
    openSettings: () => ipcRenderer.invoke("ui:openSettings") as Promise<void>,
    closeSettings: () => ipcRenderer.invoke("ui:closeSettings") as Promise<void>,
    onMenuAction: (listener) => {
      const wrappedListener = (_event: Electron.IpcRendererEvent, action: "open-settings") => listener(action);
      ipcRenderer.on("ui:menu-action", wrappedListener);

      return () => {
        ipcRenderer.off("ui:menu-action", wrappedListener);
      };
    },
    quit: () => ipcRenderer.invoke("app:quit") as Promise<void>
  }
};

contextBridge.exposeInMainWorld("musicPet", api);
