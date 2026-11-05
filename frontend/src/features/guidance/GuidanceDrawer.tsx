import { useId, useRef, useState, type ReactNode } from "react";

export function GuidanceDrawer({ title, children, triggerLabel = "Rehber" }: { title: string; children: ReactNode; triggerLabel?: string }) {
  const [open, setOpen] = useState(false);
  const id = useId();
  const dialog = useRef<HTMLDialogElement>(null);
  const close = () => { dialog.current?.close(); setOpen(false); };

  return <>
    <button type="button" className="btn btn-secondary" aria-haspopup="dialog" onClick={() => {
      dialog.current?.showModal();
      setOpen(true);
    }}><i className="fa-regular fa-circle-question" aria-hidden="true" /> {triggerLabel}</button>
    <dialog ref={dialog} className="ez-guidance-drawer" aria-labelledby={id} onKeyDown={event => event.stopPropagation()} onClose={() => { if (!dialog.current?.open) setOpen(false); }} onCancel={event => { event.preventDefault(); close(); }} onClick={event => {
      const link = (event.target as HTMLElement).closest<HTMLAnchorElement>('a[href^="#"]');
      if (!link) return;
      const target = document.getElementById(link.hash.slice(1));
      if (!target) return;
      let parent = target.parentElement;
      while (parent) {
        if (parent instanceof HTMLDetailsElement) parent.open = true;
        parent = parent.parentElement;
      }
      close();
    }}>
      <div className="ez-drawer-heading">
        <h2 id={id}>{title}</h2>
        <button type="button" className="btn btn-secondary" onClick={close}>Kapat</button>
      </div>
      {open && children}
    </dialog>
  </>;
}
