interface ControlBarProps {
  visible: boolean;
  status: "playing" | "paused" | "stopped" | "unknown";
  canGoPrevious: boolean;
  canPlayPause: boolean;
  canGoNext: boolean;
  onPrevious: () => void;
  onPlayPause: () => void;
  onNext: () => void;
  onOpenSettings: () => void;
}

function PreviousIcon() {
  return (
    <svg className="control-icon" viewBox="0 0 24 24" aria-hidden="true">
      <path d="M8 7v10" />
      <path d="M17 7l-6 5 6 5V7Z" />
    </svg>
  );
}

function NextIcon() {
  return (
    <svg className="control-icon" viewBox="0 0 24 24" aria-hidden="true">
      <path d="M16 7v10" />
      <path d="M7 7l6 5-6 5V7Z" />
    </svg>
  );
}

function PlayIcon() {
  return (
    <svg className="control-icon" viewBox="0 0 24 24" aria-hidden="true">
      <path d="M9 7l8 5-8 5V7Z" />
    </svg>
  );
}

function PauseIcon() {
  return (
    <svg className="control-icon" viewBox="0 0 24 24" aria-hidden="true">
      <path d="M9 7v10" />
      <path d="M15 7v10" />
    </svg>
  );
}

function SettingsIcon() {
  return (
    <svg className="control-icon" viewBox="0 0 24 24" aria-hidden="true">
      <path d="M8 5.5h8l4 6.5l-4 6.5H8L4 12l4-6.5Z" />
      <circle cx="12" cy="12" r="2.2" />
    </svg>
  );
}

export function ControlBar(props: ControlBarProps) {
  const isPaused = props.status === "paused";
  const playPauseLabel = isPaused ? "Play" : "Pause";

  return (
    <div className={`control-bar no-drag ${props.visible ? "is-visible" : ""}`}>
      <button className="control-button control-button-icon no-drag" onClick={props.onPrevious} aria-label="Previous" title="Previous">
        <PreviousIcon />
      </button>
      <button
        className={`control-button control-button-icon no-drag ${isPaused ? "is-active" : ""}`}
        onClick={props.onPlayPause}
        aria-label={playPauseLabel}
        title={playPauseLabel}
      >
        {isPaused ? <PlayIcon /> : <PauseIcon />}
      </button>
      <button className="control-button control-button-icon no-drag" onClick={props.onNext} aria-label="Next" title="Next">
        <NextIcon />
      </button>
      <button className="control-button control-button-icon no-drag" onClick={props.onOpenSettings} aria-label="Settings" title="Settings">
        <SettingsIcon />
      </button>
    </div>
  );
}
