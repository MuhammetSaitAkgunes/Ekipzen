import { Children, isValidElement, useId, useState, type ReactNode } from "react";
import { guidanceStepTitles } from "./stepDefinitions";
import { navigateStepTab } from "./stepTabNavigation";

export function GuidanceOperationDeck({ children }: { children: ReactNode }) {
  const [selected, setSelected] = useState(0); const id = useId();
  const items = Children.toArray(children).filter(isValidElement<{ process: string; step: string }>);
  const active = Math.min(selected, items.length - 1);
  return <div><div className="ez-step-tabs" role="tablist" aria-label="İşlem rehberi adımları">{items.map((item, index) => <button type="button" key={item.key} id={`${id}-${index}`} role="tab" tabIndex={index === active ? 0 : -1} onKeyDown={event => navigateStepTab(event, index, items.length, setSelected)} aria-selected={index === active} aria-controls={`${id}-panel`} onClick={() => setSelected(index)}>{guidanceStepTitles[item.props.process]?.[item.props.step] ?? item.props.step}</button>)}</div>
    <div role="tabpanel" id={`${id}-panel`} aria-labelledby={`${id}-${active}`}>{items[active]}</div></div>;
}
