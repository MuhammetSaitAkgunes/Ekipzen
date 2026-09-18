import { readFileSync } from "node:fs";
import { join } from "node:path";
import { expect, test } from "vitest";

// WCAG 2.x relative-luminance / contrast-ratio helper (self-contained, no deps).
function relativeLuminance(hex: string): number {
  const h = hex.replace("#", "");
  const r = parseInt(h.substring(0, 2), 16) / 255;
  const g = parseInt(h.substring(2, 4), 16) / 255;
  const b = parseInt(h.substring(4, 6), 16) / 255;
  const channel = (c: number) => (c <= 0.03928 ? c / 12.92 : Math.pow((c + 0.055) / 1.055, 2.4));
  return 0.2126 * channel(r) + 0.7152 * channel(g) + 0.0722 * channel(b);
}

function contrastRatio(hexA: string, hexB: string): number {
  const l1 = relativeLuminance(hexA);
  const l2 = relativeLuminance(hexB);
  const lighter = Math.max(l1, l2);
  const darker = Math.min(l1, l2);
  return (lighter + 0.05) / (darker + 0.05);
}

// Light-theme token values, copied as literals from tokens.css (`:root, .ez-light` block).
// Kept in sync via the "tokens.css contains the literal hex" assertions below.
const TOKENS = {
  "--ez-text": "#0f172a", // --slate-900
  "--ez-text-2": "#334155", // --slate-700
  "--ez-text-3": "#64748b", // --slate-500
  "--ez-surface": "#ffffff", // --slate-0
  "--ez-bg": "#f8fafc", // --slate-50
  "--ez-warning": "#b45309", // --amber-700
  "--ez-warning-soft": "#fffbeb", // --amber-50
  "--ez-success": "#047857", // --emerald-700
  "--ez-success-soft": "#ecfdf5", // --emerald-50
  "--ez-info": "#0369a1", // --sky-700
  "--ez-info-soft": "#f0f9ff", // --sky-50
  "--ez-danger": "#be123c", // --rose-700
  "--ez-danger-soft": "#fff1f2", // --rose-50
  "--ez-on-primary": "#ffffff", // --slate-0
  "--ez-primary": "#4338ca", // --indigo-700
} as const;

test("tokens.css açık tema değerleri bu testteki literal'lerle eşleşir", () => {
  const css = readFileSync(join(__dirname, "tokens.css"), "utf8");
  expect(css).toMatch(/--ez-warning:\s*var\(--amber-700\)/);
  expect(css).toMatch(/--ez-success:\s*var\(--emerald-700\)/);
  expect(css).toMatch(/--ez-info:\s*var\(--sky-700\)/);
  expect(css).toMatch(/--ez-danger:\s*var\(--rose-700\)/);
  expect(css).toContain("--amber-700: #b45309");
  expect(css).toContain("--emerald-700: #047857");
  expect(css).toContain("--sky-700: #0369a1");
  expect(css).toContain("--rose-700: #be123c");
});

test.each([
  ["--ez-text", "--ez-surface"],
  ["--ez-text", "--ez-bg"],
  ["--ez-text-2", "--ez-surface"],
  ["--ez-text-2", "--ez-bg"],
  ["--ez-text-3", "--ez-surface"],
  ["--ez-text-3", "--ez-bg"],
] as const)("%s / %s açık temada AA metin kontrastını (>=4.5) sağlar", (fg, bg) => {
  const ratio = contrastRatio(TOKENS[fg], TOKENS[bg]);
  expect(ratio).toBeGreaterThanOrEqual(4.5);
});

test.each([
  ["--ez-warning", "--ez-surface"],
  ["--ez-warning", "--ez-warning-soft"],
  ["--ez-success", "--ez-surface"],
  ["--ez-success", "--ez-success-soft"],
  ["--ez-info", "--ez-surface"],
  ["--ez-info", "--ez-info-soft"],
  ["--ez-danger", "--ez-surface"],
  ["--ez-danger", "--ez-danger-soft"],
] as const)("%s / %s açık temada AA metin kontrastını (>=4.5) sağlar", (fg, bg) => {
  const ratio = contrastRatio(TOKENS[fg], TOKENS[bg]);
  expect(ratio).toBeGreaterThanOrEqual(4.5);
});

test("--ez-on-primary / --ez-primary açık temada AA metin kontrastını (>=4.5) sağlar", () => {
  const ratio = contrastRatio(TOKENS["--ez-on-primary"], TOKENS["--ez-primary"]);
  expect(ratio).toBeGreaterThanOrEqual(4.5);
});

test(".ez-muted-note metin için yeterli kontrastlı --ez-text-3 kullanır", () => {
  const css = readFileSync(join(__dirname, "..", "..", "src", "styles", "marketing.css"), "utf8");
  const rule = css.match(/\.ez-muted-note\s*\{[^}]*\}/)?.[0] ?? "";
  expect(rule).toContain("var(--ez-text-3)");
  expect(rule).not.toContain("var(--ez-text-4)");
});

// I3: pasif .ez-journey__step artık opacity yerine renk değiştirir (düşük opaklık, AA
// kontrastını hesaba katmadan metni soluklaştırıyordu).
test(".ez-journey__step pasif/etkin durumları opacity yerine yeterli kontrastlı renkler kullanır", () => {
  const css = readFileSync(join(__dirname, "..", "..", "src", "styles", "marketing-motion.css"), "utf8");
  const baseRule = css.match(/\.ez-journey__step\s*\{[^}]*\}/)?.[0] ?? "";
  expect(baseRule).not.toContain("opacity");
  const h3Rule = css.match(/\.ez-journey__step h3\s*\{[^}]*\}/)?.[0] ?? "";
  const pRule = css.match(/\.ez-journey__step p\s*\{[^}]*\}/)?.[0] ?? "";
  expect(h3Rule).toContain("var(--ez-text-2)");
  expect(pRule).toContain("var(--ez-text-3)");
  const activeH3Rule = css.match(/\.ez-journey__step\.is-active h3\s*\{[^}]*\}/)?.[0] ?? "";
  const activePRule = css.match(/\.ez-journey__step\.is-active p\s*\{[^}]*\}/)?.[0] ?? "";
  expect(activeH3Rule).toContain("var(--ez-text)");
  expect(activePRule).toContain("var(--ez-text-2)");
});

// Pasif adım metni soluklaştırılmaz (opaklık, --ez-text-3'ü AA altına düşürüyordu);
// etkinleşme yalnız konum hareketiyle gösterilir.
test(".ez-process__step pasif durumda metni opaklıkla soluklaştırmaz", () => {
  const css = readFileSync(join(__dirname, "..", "..", "src", "styles", "marketing.css"), "utf8");
  const rule = css.match(/\.ez-process__step\s*\{[^}]*\}/)?.[0] ?? "";
  expect(rule).not.toContain("opacity");
});

test(".ez-layers__short ve .ez-journey__fields dt/dd yeterli kontrastlı token kullanır", () => {
  const marketingCss = readFileSync(join(__dirname, "..", "..", "src", "styles", "marketing.css"), "utf8");
  const motionCss = readFileSync(join(__dirname, "..", "..", "src", "styles", "marketing-motion.css"), "utf8");
  const shortRule = marketingCss.match(/\.ez-layers__short\s*\{[^}]*\}/)?.[0] ?? "";
  expect(shortRule).toContain("var(--ez-text-2)");
  const dtRule = motionCss.match(/\.ez-journey__fields dt\s*\{[^}]*\}/)?.[0] ?? "";
  expect(dtRule).toContain("var(--ez-text-2)");
  const ddRule = motionCss.match(/\.ez-journey__fields dd\s*\{[^}]*\}/)?.[0] ?? "";
  // --ez-surface-2 zemininde --ez-text-3 ≈ 4.34:1 kalıyordu; --ez-text-2 AA sağlar.
  expect(ddRule).toContain("var(--ez-text-2)");
});
