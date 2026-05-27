import type { PetAnimationId } from "@shared/pet";

interface PetSurfaceProps {
  animationId: PetAnimationId;
  onDoubleClick: () => void;
}

export function PetSurface(props: PetSurfaceProps) {
  return (
    <div className="pet-stage">
      <div
        className="pet-hitbox"
        onDoubleClick={props.onDoubleClick}
      >
        <div className={`pet-sprite pet-${props.animationId}`} aria-label="music pixel pet" />
      </div>
    </div>
  );
}
