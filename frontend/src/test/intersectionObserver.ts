type Callback = (entries: IntersectionObserverEntry[], observer: IntersectionObserver) => void;

class TestIntersectionObserver {
  static instances = new Set<TestIntersectionObserver>();
  readonly root = null;
  readonly rootMargin = "";
  readonly thresholds = [0];
  private readonly elements = new Set<Element>();
  private readonly callback: Callback;
  constructor(callback: Callback) {
    this.callback = callback;
    TestIntersectionObserver.instances.add(this);
  }
  observe(element: Element) { this.elements.add(element); }
  unobserve(element: Element) { this.elements.delete(element); }
  disconnect() { this.elements.clear(); TestIntersectionObserver.instances.delete(this); }
  takeRecords(): IntersectionObserverEntry[] { return []; }
  trigger(isIntersecting: boolean, predicate: (el: Element) => boolean, passed = false) {
    const entries = [...this.elements].filter(predicate).map((target) => ({
      isIntersecting, target, intersectionRatio: isIntersecting ? 1 : 0,
      time: 0,
      boundingClientRect: passed ? new DOMRect(0, -200, 100, 100) : target.getBoundingClientRect(),
      intersectionRect: target.getBoundingClientRect(),
      rootBounds: passed ? new DOMRect(0, 0, 1280, 800) : null,
    }) as IntersectionObserverEntry);
    if (entries.length) this.callback(entries, this as unknown as IntersectionObserver);
  }
}

export function installIntersectionObserver() {
  (globalThis as unknown as { IntersectionObserver: unknown }).IntersectionObserver = TestIntersectionObserver;
}

/** Gözlenen öğeleri görünür/görünmez yapar (sırayla, belge sırasına göre). */
export function triggerIntersections(isIntersecting: boolean, predicate: (el: Element) => boolean = () => true) {
  for (const observer of [...TestIntersectionObserver.instances]) observer.trigger(isIntersecting, predicate);
}

/** Gözlenen öğeleri kesişmeden görünür alanın üstüne geçmiş gibi bildirir (hızlı kaydırma). */
export function triggerScrolledPast(predicate: (el: Element) => boolean = () => true) {
  for (const observer of [...TestIntersectionObserver.instances]) observer.trigger(false, predicate, true);
}

export function resetIntersectionObservers() {
  TestIntersectionObserver.instances.clear();
}

let reducedMotion = false;
const listeners = new Set<() => void>();

export function installMatchMedia() {
  window.matchMedia = ((query: string) => ({
    get matches() { return query.includes("prefers-reduced-motion") ? reducedMotion : false; },
    media: query,
    onchange: null,
    addEventListener: (_: string, cb: () => void) => listeners.add(cb),
    removeEventListener: (_: string, cb: () => void) => listeners.delete(cb),
    addListener: (cb: () => void) => listeners.add(cb),
    removeListener: (cb: () => void) => listeners.delete(cb),
    dispatchEvent: () => false,
  })) as unknown as typeof window.matchMedia;
}

export function setReducedMotion(value: boolean) {
  reducedMotion = value;
  for (const cb of [...listeners]) cb();
}
