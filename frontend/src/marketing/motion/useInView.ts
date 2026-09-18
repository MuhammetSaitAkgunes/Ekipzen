import { useCallback, useEffect, useRef, useState } from "react";

export type InViewOptions = { once?: boolean; threshold?: number; rootMargin?: string };

export function useInView<T extends Element>({ once = false, threshold = 0.15, rootMargin = "0px 0px -10% 0px" }: InViewOptions = {}) {
  const supported = typeof IntersectionObserver !== "undefined";
  const [inView, setInView] = useState(!supported);
  const observerRef = useRef<IntersectionObserver | null>(null);

  const ref = useCallback((node: T | null) => {
    observerRef.current?.disconnect();
    observerRef.current = null;
    if (!node || typeof IntersectionObserver === "undefined") return;
    const observer = new IntersectionObserver((entries) => {
      for (const entry of entries) {
        if (entry.isIntersecting) {
          setInView(true);
          if (once) { observer.disconnect(); observerRef.current = null; }
        } else if (!once) {
          setInView(false);
        }
      }
    }, { threshold, rootMargin });
    observer.observe(node);
    observerRef.current = observer;
  }, [once, threshold, rootMargin]);

  useEffect(() => () => observerRef.current?.disconnect(), []);
  return { ref, inView };
}
