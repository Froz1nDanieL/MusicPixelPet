import type { MediaSnapshot, MediaTrack } from "@shared/media";
import type { PetAnimationId } from "@shared/pet";
import type { MusicRule } from "@shared/settings";

function matchesRule(track: MediaTrack, rule: MusicRule): boolean {
  const keyword = rule.keyword.trim().toLowerCase();

  if (!keyword) {
    return false;
  }

  const candidates =
    rule.field === "title"
      ? [track.title]
      : rule.field === "artist"
        ? [track.artist]
        : rule.field === "album"
          ? [track.album]
          : [track.title, track.artist, track.album];

  return candidates.some((candidate) => candidate.toLowerCase().includes(keyword));
}

export function derivePetAnimation(snapshot: MediaSnapshot, rules: MusicRule[]): PetAnimationId {
  if (!snapshot.connected || !snapshot.track) {
    return "idle";
  }

  if (snapshot.status === "paused" || snapshot.status === "stopped") {
    return "paused";
  }

  const matchedRule = rules.find((rule) => matchesRule(snapshot.track as MediaTrack, rule));

  if (matchedRule?.mode === "sleepy") {
    return "sleeping";
  }

  if (matchedRule?.mode === "energetic") {
    return "celebrating";
  }

  return "playing";
}

