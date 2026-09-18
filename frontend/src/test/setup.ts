import "@testing-library/jest-dom/vitest";
import { afterEach } from "vitest";
import { installIntersectionObserver, installMatchMedia, resetIntersectionObservers, setReducedMotion } from "./intersectionObserver";

installIntersectionObserver();
installMatchMedia();

afterEach(() => {
  resetIntersectionObservers();
  setReducedMotion(false);
});
