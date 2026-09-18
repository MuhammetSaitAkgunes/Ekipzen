import { ROADMAP_NOTE, ROADMAP_STAGES, roadmap } from "./content";
import { Reveal } from "./motion";

export function RoadmapBoard() {
  return (
    <div className="ez-roadmap">
      <div className="ez-roadmap__board">
        {ROADMAP_STAGES.map((stage, col) => (
          <div key={stage.id} className={`ez-roadmap__col ez-roadmap__col--${stage.id}`}>
            <div className="ez-roadmap__head">
              <h3>{stage.title}</h3>
              <p>{stage.subtitle}</p>
            </div>
            {roadmap.filter((r) => r.stage === stage.id).map((item, i) => (
              <Reveal as="article" key={item.title} delay={col * 120 + i * 80} className="ez-roadmap__card">
                <h4>{item.title}</h4>
                <p>{item.text}</p>
              </Reveal>
            ))}
          </div>
        ))}
      </div>
      <p className="ez-roadmap__note"><i aria-hidden="true" className="fa-solid fa-circle-info" /> {ROADMAP_NOTE}</p>
    </div>
  );
}
