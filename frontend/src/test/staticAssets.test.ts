import { readFileSync } from "node:fs";
import { join } from "node:path";
import { expect, test } from "vitest";

const root = join(__dirname, "..", "..");
const read = (p: string) => readFileSync(join(root, p), "utf8");

test("index.html favicon, açıklama ve OG etiketlerini içerir", () => {
  const html = read("index.html");
  expect(html).toContain('rel="icon" type="image/svg+xml" href="/favicon.svg"');
  expect(html).toContain('name="description"');
  expect(html).toContain('property="og:title"');
  expect(html).toContain('name="theme-color" content="#4338ca"');
});

test("robots.txt uygulama yollarını dizinden çıkarır ve sitemap'i gösterir", () => {
  const robots = read("public/robots.txt");
  expect(robots).toContain("Disallow: /app/");
  expect(robots).toContain("Disallow: /davet");
  expect(robots).toMatch(/Sitemap: https:\/\/.+\/sitemap\.xml/);
});

test("sitemap tüm herkese açık sayfaları listeler", () => {
  const sitemap = read("public/sitemap.xml");
  for (const path of ["/", "/ozellikler", "/fiyatlandirma", "/hakkimizda", "/guvenlik", "/iletisim", "/kvkk-aydinlatma"]) {
    expect(sitemap).toContain(`<loc>https://ekipzen.com${path}</loc>`);
  }
});
