export type PlaybackStatus = "playing" | "paused" | "stopped" | "unknown";

export interface MediaTrack {
  title: string;
  artist: string;
  album: string;
  artworkDataUrl: string | null;
  sourceAppId: string;
  sourceAppName: string;
}

export interface MediaSnapshot {
  connected: boolean;
  activePlayer: string | null;
  status: PlaybackStatus;
  track: MediaTrack | null;
  volumeLevel: number;
  canPlayPause: boolean;
  canGoNext: boolean;
  canGoPrevious: boolean;
  lastUpdatedAt: string;
  errorMessage: string | null;
}

export interface VolumeCommand {
  delta: number;
}

export function createDisconnectedSnapshot(errorMessage: string | null = null): MediaSnapshot {
  return {
    connected: false,
    activePlayer: null,
    status: "unknown",
    track: null,
    volumeLevel: 0,
    canPlayPause: false,
    canGoNext: false,
    canGoPrevious: false,
    lastUpdatedAt: new Date().toISOString(),
    errorMessage
  };
}
