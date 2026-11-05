import "@testing-library/jest-dom/vitest";
import { afterEach, beforeEach } from "vitest";
import { installIntersectionObserver, installMatchMedia, resetIntersectionObservers, setReducedMotion } from "./intersectionObserver";

installIntersectionObserver();
installMatchMedia();
// jsdom exposes <dialog> but does not implement its native lifecycle.
HTMLDialogElement.prototype.showModal = function () { this.setAttribute("open", ""); };
HTMLDialogElement.prototype.close = function () {
  this.removeAttribute("open");
  this.dispatchEvent(new Event("close"));
};
beforeEach(() => sessionStorage.clear());

afterEach(() => {
  resetIntersectionObservers();
  setReducedMotion(false);
});
