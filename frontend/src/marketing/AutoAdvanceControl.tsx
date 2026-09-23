import { playbackCopy } from "./content";

export function AutoAdvanceControl({ enabled, reduced, onToggle }: { enabled: boolean; reduced: boolean; onToggle: () => void }) {
  return (
    <div className="ez-playback">
      {reduced ? <p className="ez-muted-note">{playbackCopy.reduced}</p> : (
        <button type="button" className="ez-playback__button" onClick={onToggle}>
          <i aria-hidden="true" className={enabled ? "fa-solid fa-pause" : "fa-solid fa-play"} />
          {enabled ? playbackCopy.pause : playbackCopy.resume}
        </button>
      )}
    </div>
  );
}
