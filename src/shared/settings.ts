export type ControlBarDisplayMode = "hover" | "always";
export type RuleMatchField = "any" | "title" | "artist" | "album";
export type RulePetMode = "default" | "energetic" | "sleepy";

export interface MusicRule {
  id: string;
  keyword: string;
  field: RuleMatchField;
  mode: RulePetMode;
}

export interface WindowBounds {
  x: number;
  y: number;
  width: number;
  height: number;
}

export interface AppSettings {
  skinId: string;
  wheelVolumeEnabled: boolean;
  playerWhitelist: string[];
  musicRules: MusicRule[];
  autoLaunch: boolean;
  alwaysOnTop: boolean;
  controlBarMode: ControlBarDisplayMode;
  windowBounds: WindowBounds;
}

export const PET_WINDOW_SIZE = {
  width: 300,
  height: 360
} as const;

export const DEFAULT_WINDOW_BOUNDS: WindowBounds = {
  x: 80,
  y: 80,
  width: PET_WINDOW_SIZE.width,
  height: PET_WINDOW_SIZE.height
};

export const DEFAULT_SETTINGS: AppSettings = {
  skinId: "default",
  wheelVolumeEnabled: true,
  playerWhitelist: ["cloudmusic", "qqmusic"],
  musicRules: [],
  autoLaunch: false,
  alwaysOnTop: true,
  controlBarMode: "hover",
  windowBounds: DEFAULT_WINDOW_BOUNDS
};
