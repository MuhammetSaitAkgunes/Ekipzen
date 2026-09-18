import { useCallback, useEffect, useState } from "react";

export type InViewOptions = { once?: boolean; threshold?: number; rootMargin?: string };

/**
 * Öğenin görünür alana girip girmediğini izler.
 *
 * Gözlemci callback ref içinde değil efektte kurulur: React geliştirme modunda
 * (StrictMode) efektleri kurup temizleyip yeniden kurar ama ref'leri yeniden
 * çağırmaz. Gözlemci ref'te kurulsaydı ilk temizlikte kapanır ve bir daha açılmazdı;
 * Reveal içerikleri kalıcı olarak görünmez kalırdı.
 */
export function useInView<T extends Element>({ once = false, threshold = 0.15, rootMargin = "0px 0px -10% 0px" }: InViewOptions = {}) {
  const supported = typeof IntersectionObserver !== "undefined";
  const [inView, setInView] = useState(!supported);
  const [node, setNode] = useState<T | null>(null);
  const ref = useCallback((el: T | null) => setNode(el), []);

  useEffect(() => {
    if (!node || typeof IntersectionObserver === "undefined") return;
    const observer = new IntersectionObserver((entries) => {
      for (const entry of entries) {
        if (entry.isIntersecting) {
          setInView(true);
          if (once) observer.disconnect();
        } else if (!once) {
          setInView(false);
        }
      }
    }, { threshold, rootMargin });
    observer.observe(node);
    return () => observer.disconnect();
  }, [node, once, threshold, rootMargin]);

  return { ref, inView };
}
