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
    <dialog ref={dialog} className="ez-guidance-drawer" aria-labelledby={id} onKeyDown={event => event.stopPropagation()} onClose={() => setOpen(false)} onCancel={event => { event.preventDefault(); close(); }}>
      <div className="ez-drawer-heading">
        <h2 id={id}>{title}</h2>
        <button type="button" className="btn btn-secondary" onClick={close}>Kapat</button>
      </div>
      {open && children}
    </dialog>
  </>;
}
