import "@testing-library/jest-dom/vitest";
import { afterEach, beforeEach } from "vitest";
import { installIntersectionObserver, installMatchMedia, resetIntersectionObservers, setReducedMotion } from "./intersectionObserver";

installIntersectionObserver();
installMatchMedia();
beforeEach(() => sessionStorage.clear());

afterEach(() => {
  resetIntersectionObservers();
  setReducedMotion(false);
});
