import { act, fireEvent, render, screen } from "@testing-library/react";
import { expect, test } from "vitest";
import { setReducedMotion, triggerIntersections } from "../../test/intersectionObserver";
import { Reveal, onSpotlightMove, useInView, usePrefersReducedMotion } from ".";

test("Reveal görünür olana kadar is-visible taşımaz, görünür olunca taşır", () => {
  render(<Reveal data-testid="r">içerik</Reveal>);
  const el = screen.getByTestId("r");
  expect(el).toHaveClass("ez-reveal");
  expect(el).not.toHaveClass("is-visible");
  act(() => triggerIntersections(true));
  expect(el).toHaveClass("is-visible");
});

test("Reveal hareket azaltılmışsa anında görünür ve gecikmeyi değişkene yazar", () => {
  setReducedMotion(true);
  render(<Reveal data-testid="r" delay={120}>içerik</Reveal>);
  const el = screen.getByTestId("r");
  expect(el).toHaveClass("is-visible");
  expect(el.style.getPropertyValue("--ez-reveal-delay")).toBe("120ms");
});

test("Reveal as ile farklı etiket render eder", () => {
  render(<ul><Reveal as="li" data-testid="r">öğe</Reveal></ul>);
  expect(screen.getByTestId("r").tagName).toBe("LI");
});

function InViewProbe({ once }: { once?: boolean }) {
  const { ref, inView } = useInView<HTMLDivElement>({ once });
  return <div ref={ref} data-testid="p">{inView ? "evet" : "hayır"}</div>;
}

test("useInView görünürlüğü izler; once ile görünür kalır", () => {
  const { unmount } = render(<InViewProbe />);
  act(() => triggerIntersections(true));
  expect(screen.getByTestId("p")).toHaveTextContent("evet");
  act(() => triggerIntersections(false));
  expect(screen.getByTestId("p")).toHaveTextContent("hayır");
  unmount();
  render(<InViewProbe once />);
  act(() => triggerIntersections(true));
  act(() => triggerIntersections(false));
  expect(screen.getByTestId("p")).toHaveTextContent("evet");
});

function MotionProbe() {
  return <span data-testid="m">{usePrefersReducedMotion() ? "azaltılmış" : "normal"}</span>;
}

test("usePrefersReducedMotion tercihi ve değişimini izler", () => {
  render(<MotionProbe />);
  expect(screen.getByTestId("m")).toHaveTextContent("normal");
  act(() => setReducedMotion(true));
  expect(screen.getByTestId("m")).toHaveTextContent("azaltılmış");
});

test("onSpotlightMove imleç konumunu CSS değişkenlerine yazar", () => {
  render(<div data-testid="s" onPointerMove={onSpotlightMove} />);
  const el = screen.getByTestId("s");
  fireEvent.pointerMove(el, { clientX: 30, clientY: 12 });
  expect(el.style.getPropertyValue("--mx")).toBe("30px");
  expect(el.style.getPropertyValue("--my")).toBe("12px");
});
