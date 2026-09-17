import { readFileSync, readdirSync, statSync } from "node:fs";
import { join } from "node:path";
import { expect, test } from "vitest";

const srcRoot = join(__dirname, "..");

function collectTsxFiles(dir: string): string[] {
  const entries = readdirSync(dir);
  const files: string[] = [];
  for (const entry of entries) {
    const full = join(dir, entry);
    const stat = statSync(full);
    if (stat.isDirectory()) {
      files.push(...collectTsxFiles(full));
    } else if (entry.endsWith(".tsx") && !entry.endsWith(".test.tsx")) {
      files.push(full);
    }
  }
  return files;
}

test("eski petrol grafik renkleri .tsx dosyalarında kalmaz", () => {
  const legacy = /#0f766e|15,\s*118,\s*110|#b98a2f/i;
  for (const file of collectTsxFiles(srcRoot)) {
    expect(readFileSync(file, "utf8"), file).not.toMatch(legacy);
  }
});
