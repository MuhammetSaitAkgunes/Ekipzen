import { useState, type CSSProperties } from "react";
import { securityLayers } from "./content";

export function SecurityLayers() {
  const [openId, setOpenId] = useState(securityLayers[0].id);
  return (
    <div className="ez-layers-view">
      <div className="ez-sec-layers ez-sec-layers--five" aria-hidden="true">
        {securityLayers.map((layer, i) => (
          <span key={layer.id} className={`ez-sec-layers__layer ez-sec-layers__layer--${i + 1}${openId === layer.id ? " is-active" : ""}`}>{layer.title}</span>
        ))}
      </div>
    <ol className="ez-layers">
      {securityLayers.map((layer, i) => {
        const expanded = openId === layer.id;
        return (
          <li key={layer.id} className={`ez-layers__item${expanded ? " is-open" : ""}`} style={{ "--layer-index": i } as CSSProperties}>
            <h3 className="ez-layers__heading">
              <button type="button" id={`layer-btn-${layer.id}`} aria-expanded={expanded} aria-controls={`layer-${layer.id}`} className="ez-layers__button" onClick={() => setOpenId(layer.id)}>
                <span className="ez-layers__num" aria-hidden="true">{i + 1}</span>
                <span className="ez-layers__title">{layer.title}</span>
                <span className="ez-layers__short">{layer.short}</span>
              </button>
            </h3>
            <div id={`layer-${layer.id}`} role="region" aria-labelledby={`layer-btn-${layer.id}`} className="ez-layers__panel" hidden={!expanded}>
              <p>{layer.text}</p>
            </div>
          </li>
        );
      })}
    </ol>
    </div>
  );
}
