import { readFileSync, readdirSync } from "node:fs";
import { join } from "node:path";
import { expect, test } from "vitest";

const dir = __dirname;
const read = (name: string) => readFileSync(join(dir, name), "utf8");

test("tokens.css zorunlu amaç token'larını ve kimlik renklerini tanımlar", () => {
  const css = read("tokens.css");
  for (const name of ["--ez-bg", "--ez-surface", "--ez-text", "--ez-primary", "--ez-on-primary", "--ez-border", "--ez-focus-ring", "--ez-nav-bg", "--ez-font-sans"]) {
    expect(css).toContain(`${name}:`);
  }
  expect(css.toLowerCase()).toContain("--indigo-700: #4338ca");
  expect(css.toLowerCase()).toContain("--slate-900: #0f172a");
  expect(css).toContain('"Plus Jakarta Sans"');
  expect(css).toContain(':root[data-theme="dark"]');
});

test("eski petrol/evergreen renkleri stil dosyalarında kalmaz", () => {
  const legacy = /#0f766e|#0b5a54|#2fbfae|#0c2b29|#b98a2f|rgb\(47 191 174|rgb\(15 118 110|IBM Plex/i;
  for (const file of readdirSync(dir).filter((f) => f.endsWith(".css"))) {
    expect(read(file), file).not.toMatch(legacy);
  }
});
