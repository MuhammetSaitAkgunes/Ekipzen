import { readFileSync, readdirSync } from "node:fs";
import { join } from "node:path";
import { expect, test } from "vitest";
import { securityPillars, faqs } from "./content";

// Platform katalogu Tenants/Directory/RefreshTokens gibi paylaşılan tablolar barındırır;
// "paylaşılan tablo yoktur" gibi mutlak iddialar bu nedenle yanlıştır. CSP 'unsafe-inline'
// stil izni verdiği için "sıkı" (strict) nitelemesi de abartılı — bkz. final-fix-findings I5.
test("güvenlik metinleri abartılı/yanlış iddialar içermez", () => {
  const texts = [...securityPillars.map((p) => p.text), ...faqs.map((f) => f.a)];
  for (const text of texts) {
    expect(text).not.toMatch(/paylaşılan tablo yoktur/i);
    expect(text).not.toMatch(/sıkı içerik güvenliği/i);
  }
});

test("Ayrı veritabanı iddiası İK verisi kapsamıyla sınırlıdır", () => {
  const pillar = securityPillars.find((p) => p.title === "Ayrı veritabanı");
  expect(pillar?.text).toContain("İK verileriniz");
  expect(pillar?.text).toContain("aynı tabloda bulunmaz");
});

// Üslup koruması: tanıtım ve giriş metinleri ölçülü ve kurumsal kalır; pazar/coğrafya
// sloganı, abartılı kolaylık ifadeleri ve doğrulanmamış süre vaatleri kullanılmaz.
test("tanıtım metinleri üslup kurallarına uyar", () => {
  const src = join(__dirname, "..");
  const files = [
    ...readdirSync(join(src, "marketing")).filter((f) => /\.tsx?$/.test(f) && !f.includes(".test.")).map((f) => join(src, "marketing", f)),
    ...readdirSync(join(src, "marketing", "pages")).filter((f) => f.endsWith(".tsx") && !f.includes(".test.")).map((f) => join(src, "marketing", "pages", f)),
    join(src, "auth", "AuthLayout.tsx"),
    join(src, "auth", "LoginPage.tsx"),
    join(src, "..", "index.html"),
  ];
  const banned = [/Türkiye için/i, /tek tıkla/i, /sürpriz/i, /kurtar/i, /\d+\s*dakikalık/i, /kısa sürede/i, /kolayca/i];
  for (const file of files) {
    const text = readFileSync(file, "utf8");
    for (const pattern of banned) expect(text, `${file} ${pattern}`).not.toMatch(pattern);
  }
});
