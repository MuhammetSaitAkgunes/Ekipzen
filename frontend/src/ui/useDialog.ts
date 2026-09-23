import { useEffect, useRef, type RefObject } from "react";

export function useDialog(ref: RefObject<HTMLElement>, open: boolean, onClose: () => void) {
  const close = useRef(onClose);
  close.current = onClose;
  useEffect(() => {
    if (!open || !ref.current) return;
    const previous = document.activeElement as HTMLElement | null;
    const root = ref.current;
    const focusable = () => Array.from(root.querySelectorAll<HTMLElement>(
      'a[href],button:not([disabled]),input:not([disabled]),select:not([disabled]),textarea:not([disabled]),[tabindex="0"]',
    )).filter((node) => !node.closest('[hidden]'));
    (focusable()[0] ?? root).focus();
    const onKey = (event: KeyboardEvent) => {
      if (event.key === "Escape") { event.preventDefault(); close.current(); }
      if (event.key !== "Tab") return;
      const nodes = focusable(); const first = nodes[0]; const last = nodes[nodes.length - 1];
      if (!first) { event.preventDefault(); root.focus(); return; }
      if (event.shiftKey && (document.activeElement === first || document.activeElement === root)) {
        event.preventDefault(); last.focus();
      } else if (!event.shiftKey && document.activeElement === last) {
        event.preventDefault(); first.focus();
      }
    };
    root.addEventListener("keydown", onKey);
    return () => { root.removeEventListener("keydown", onKey); previous?.focus(); };
  }, [open, ref]);
}
