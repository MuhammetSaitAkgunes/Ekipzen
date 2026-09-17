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

test("İzole veri iddiası İK verisi kapsamıyla sınırlıdır", () => {
  const pillar = securityPillars.find((p) => p.title === "İzole veri");
  expect(pillar?.text).toContain("İK verileriniz");
  expect(pillar?.text).toContain("aynı tabloda bulunmaz");
});
