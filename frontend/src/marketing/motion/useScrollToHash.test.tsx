import { render, waitFor } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { afterEach, expect, test, vi } from "vitest";
import { setReducedMotion } from "../../test/intersectionObserver";
import { useScrollToHash } from "./useScrollToHash";

const originalScrollIntoView = Element.prototype.scrollIntoView;

afterEach(() => {
  Element.prototype.scrollIntoView = originalScrollIntoView;
});

function Probe() {
  useScrollToHash();
  return <section id="hedef" />;
}

test("hash hedefine smooth kaydırır", async () => {
  const scrollIntoView = vi.fn();
  Element.prototype.scrollIntoView = scrollIntoView;
  render(
    <MemoryRouter initialEntries={["/x#hedef"]}>
      <Probe />
    </MemoryRouter>,
  );
  await waitFor(() => expect(scrollIntoView).toHaveBeenCalledWith({ behavior: "smooth", block: "start" }));
});

test("hareket azaltılmışsa anında kaydırır", async () => {
  setReducedMotion(true);
  const scrollIntoView = vi.fn();
  Element.prototype.scrollIntoView = scrollIntoView;
  render(
    <MemoryRouter initialEntries={["/x#hedef"]}>
      <Probe />
    </MemoryRouter>,
  );
  await waitFor(() => expect(scrollIntoView).toHaveBeenCalledWith({ behavior: "auto", block: "start" }));
});

test("hash yoksa kaydırmaz", async () => {
  const scrollIntoView = vi.fn();
  Element.prototype.scrollIntoView = scrollIntoView;
  render(
    <MemoryRouter initialEntries={["/x"]}>
      <Probe />
    </MemoryRouter>,
  );
  await new Promise((r) => setTimeout(r, 50));
  expect(scrollIntoView).not.toHaveBeenCalled();
});
