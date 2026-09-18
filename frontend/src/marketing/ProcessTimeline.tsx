import { devProcess } from "./content";
import { useInView } from "./motion";

export function ProcessTimeline() {
  const { ref, inView } = useInView<HTMLOListElement>({ once: true, threshold: 0.3 });
  return (
    <ol ref={ref} className={`ez-process${inView ? " is-active" : ""}`}>
      {devProcess.map((step, i) => (
        <li key={step.title} className="ez-process__step" style={{ transitionDelay: `${i * 140}ms` }}>
          <span className="ez-process__marker" aria-hidden="true">{i + 1}</span>
          <h3>{step.title}</h3>
          <p>{step.text}</p>
        </li>
      ))}
    </ol>
  );
}
