import { useEffect, useState } from "react";

/** Sayfa belirli bir mesafeden fazla kaydırıldı mı? (pasif dinleyici) */
export function useScrolled(offset = 8) {
  const [scrolled, setScrolled] = useState(false);
  useEffect(() => {
    const update = () => setScrolled(window.scrollY > offset);
    update();
    window.addEventListener("scroll", update, { passive: true });
    return () => window.removeEventListener("scroll", update);
  }, [offset]);
  return scrolled;
}
