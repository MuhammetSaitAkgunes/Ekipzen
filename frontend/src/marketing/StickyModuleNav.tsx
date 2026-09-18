import type { CSSProperties } from "react";
import type { ModuleStatus } from "./content";
import { STATUS_META } from "./content";

export function StickyModuleNav({ items, activeId }: { items: { id: string; title: string; status: ModuleStatus }[]; activeId: string }) {
  const index = Math.max(0, items.findIndex((i) => i.id === activeId));
  const style = { "--progress": String((index + 1) / items.length) } as CSSProperties;
  return (
    <nav aria-label="Modül menüsü" className="ez-module-nav" style={style}>
      <span className="ez-module-nav__track" aria-hidden="true"><span className="ez-module-nav__fill" /></span>
      <ol>
        {items.map((item) => (
          <li key={item.id}>
            <a href={`#${item.id}`} aria-current={item.id === activeId ? "true" : undefined} className="ez-module-nav__link">
              <span className={`ez-dot ez-dot--${item.status}`} aria-hidden="true" />
              <span>{item.title}</span>
              <span className="ez-sr-only"> ({STATUS_META[item.status].label})</span>
            </a>
          </li>
        ))}
      </ol>
    </nav>
  );
}
