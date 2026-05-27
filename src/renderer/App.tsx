import { useEffect, useState } from "react";
import type { AppSettings } from "@shared/settings";
import { ControlBar } from "./components/ControlBar";
import { NowPlayingCard } from "./components/NowPlayingCard";
import { PetSurface } from "./components/PetSurface";
import { SettingsPanel } from "./components/SettingsPanel";
import { derivePetAnimation } from "./pet/derivePetAnimation";
import { useAppStore } from "./store/useAppStore";

const isSettingsView = new URLSearchParams(window.location.search).get("view") === "settings";

export default function App() {
  if (isSettingsView) {
    return <SettingsWindowApp />;
  }

  return <PetWindowApp />;
}

function PetWindowApp() {
  const [isHovered, setIsHovered] = useState(false);
  const { settings, media, isReady, initialize, setMedia, setSettings } = useAppStore();

  useEffect(() => {
    let disposeMedia = () => undefined;
    let disposeMenu = () => undefined;
    let disposeSettings = () => undefined;

    const bootstrap = async () => {
      const [loadedSettings, snapshot] = await Promise.all([
        window.musicPet.settings.get(),
        window.musicPet.media.getSnapshot()
      ]);

      initialize(loadedSettings, snapshot);
      disposeMedia = window.musicPet.media.onSnapshot(setMedia);
      disposeMenu = window.musicPet.ui.onMenuAction(() => window.musicPet.ui.openSettings());
      disposeSettings = window.musicPet.settings.onChange(setSettings);
    };

    void bootstrap();

    return () => {
      disposeMedia();
      disposeMenu();
      disposeSettings();
    };
  }, [initialize, setMedia, setSettings]);

  const animation = derivePetAnimation(media, settings.musicRules);
  const controlBarVisible = settings.controlBarMode === "always" || isHovered;

  return (
    <main
      className={`app-shell ${isReady ? "is-ready" : ""}`}
      onMouseEnter={() => setIsHovered(true)}
      onMouseLeave={() => setIsHovered(false)}
    >
      <NowPlayingCard
        media={media}
        volumeEnabled={settings.wheelVolumeEnabled}
        onWheelVolume={(delta) => window.musicPet.media.adjustVolume(delta)}
      />

      <PetSurface
        animationId={animation}
        onDoubleClick={() => window.musicPet.media.playPause()}
      />

      <ControlBar
        visible={controlBarVisible}
        status={media.status}
        canGoPrevious={media.canGoPrevious}
        canPlayPause={media.canPlayPause}
        canGoNext={media.canGoNext}
        onPrevious={() => window.musicPet.media.previous()}
        onPlayPause={() => window.musicPet.media.playPause()}
        onNext={() => window.musicPet.media.next()}
        onOpenSettings={() => window.musicPet.ui.openSettings()}
      />
    </main>
  );
}

function SettingsWindowApp() {
  const [settings, setSettings] = useState<AppSettings | null>(null);

  useEffect(() => {
    let disposeSettings = () => undefined;

    const bootstrap = async () => {
      setSettings(await window.musicPet.settings.get());
      disposeSettings = window.musicPet.settings.onChange(setSettings);
    };

    void bootstrap();

    return () => {
      disposeSettings();
    };
  }, []);

  if (!settings) {
    return <main className="settings-shell" />;
  }

  return (
    <main className="settings-shell">
      <SettingsPanel
        settings={settings}
        onClose={() => window.musicPet.ui.closeSettings()}
        onSave={async (patch) => {
          const nextSettings = await window.musicPet.settings.update(patch);
          setSettings(nextSettings);
        }}
      />
    </main>
  );
}
