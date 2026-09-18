import { STATUS_META, modules, type ModuleStatus } from "./content";
import { StatusBadge } from "./StatusBadge";

const ORDER: ModuleStatus[] = ["active", "pilot", "building"];

export function ModuleMaturity() {
  return (
    <div className="ez-maturity">
      {ORDER.map((status) => (
        <section key={status} className={`ez-maturity__col ez-maturity__col--${status}`} aria-labelledby={`olgunluk-${status}`}>
          <h3 id={`olgunluk-${status}`}>{STATUS_META[status].label}</h3>
          <p>{STATUS_META[status].description}</p>
          <ul>
            {modules.filter((m) => m.status === status).map((m) => (
              <li key={m.id}><i aria-hidden="true" className={`fa-solid ${m.icon}`} /><span>{m.title}</span><StatusBadge status={m.status} /></li>
            ))}
          </ul>
        </section>
      ))}
    </div>
  );
}
