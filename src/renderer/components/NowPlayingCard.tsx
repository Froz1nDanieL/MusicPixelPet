import type { CSSProperties, WheelEvent } from "react";
import type { MediaSnapshot } from "@shared/media";

const waveformBars = [0.34, 0.52, 0.76, 0.46, 0.68, 0.88, 0.58, 0.4];

interface NowPlayingCardProps {
  media: MediaSnapshot;
  volumeEnabled: boolean;
  onWheelVolume: (delta: number) => void;
}

export function NowPlayingCard({ media, volumeEnabled, onWheelVolume }: NowPlayingCardProps) {
  const handleWheel = (event: WheelEvent<HTMLElement>) => {
    if (!volumeEnabled) {
      return;
    }

    event.preventDefault();
    const delta = event.deltaY > 0 ? -1 : 1;
    onWheelVolume(delta);
  };

  if (!media.track) {
    return (
      <section className="status-card no-drag" onWheel={handleWheel}>
        <div className="status-topline">
          <span className="status-tag">未连接</span>
          <span className="status-state">空闲</span>
        </div>
        <strong className="status-title">当前没有可用的网易云音乐或 QQ 音乐会话</strong>
        <span className="status-subtitle">{media.errorMessage ?? "启动支持的播放器后，桌宠会自动接管状态显示。"}</span>
      </section>
    );
  }

  const playbackLabel = media.status === "playing" ? "播放中" : "已暂停";
  const waveformClassName = `status-waveform ${media.status === "playing" ? "is-playing" : "is-paused"}`;

  return (
    <section className="status-card no-drag" onWheel={handleWheel}>
      <div className="status-topline">
        <span className="status-tag">{media.activePlayer ?? "播放器"}</span>
        <span className="status-state">{playbackLabel}</span>
      </div>
      <strong className="status-title">{media.track.title || "未知歌曲"}</strong>
      <span className="status-subtitle">
        {media.track.artist || "未知歌手"}
        {media.track.album ? ` · ${media.track.album}` : ""}
      </span>
      <div className="status-bottomline">
        <div className={waveformClassName} aria-hidden="true">
          {waveformBars.map((height, index) => (
            <span
              key={index}
              className="status-wave-bar"
              style={{ "--wave-scale": height, "--wave-delay": `${index * 90}ms` } as CSSProperties}
            />
          ))}
        </div>
        <div
          className="status-volume"
          aria-label="音量"
          title="滚动调节音量"
          style={{ "--volume-level": Math.min(1, Math.max(0, media.volumeLevel ?? 0)) } as CSSProperties}
        >
          <span className="status-volume-fill" />
        </div>
      </div>
    </section>
  );
}
