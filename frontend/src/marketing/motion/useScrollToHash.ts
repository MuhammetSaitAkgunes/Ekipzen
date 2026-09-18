import { useEffect } from "react";
import { useLocation } from "react-router-dom";
import { usePrefersReducedMotion } from "./usePrefersReducedMotion";

export function useScrollToHash() {
  const { pathname, hash } = useLocation();
  const reduced = usePrefersReducedMotion();
  useEffect(() => {
    if (!hash) return;
    const id = decodeURIComponent(hash.slice(1));
    const frame = requestAnimationFrame(() => {
      document.getElementById(id)?.scrollIntoView({ behavior: reduced ? "auto" : "smooth", block: "start" });
    });
    return () => cancelAnimationFrame(frame);
  }, [pathname, hash, reduced]);
}
