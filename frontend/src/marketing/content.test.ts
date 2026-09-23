import { readFileSync, readdirSync } from "node:fs";
import { join } from "node:path";
import { expect, test } from "vitest";
import {
  securityPillars, faqs,
  STATUS_META, SHOWCASE_ORDER, ROADMAP_NOTE, ROADMAP_STAGES, commitments, comparisonRows, devProcess, journeySteps, modules, plans, principles,
  roadmap, roleViews, securityCategories, securityLayers, transparency,
} from "./content";

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
    ...readdirSync(join(src, "marketing", "motion")).filter((f) => (f.endsWith(".ts") || f.endsWith(".tsx")) && !f.includes(".test.")).map((f) => join(src, "marketing", "motion", f)),
    join(src, "auth", "AuthLayout.tsx"),
    join(src, "auth", "LoginPage.tsx"),
    join(src, "..", "index.html"),
  ];
  const banned = [
    /Türkiye için/i, /tek tıkla/i, /sürpriz/i, /kurtar/i, /\d+\s*dakikalık/i, /kısa sürede/i, /kolayca/i,
    /en çok tercih edilen/i, /\blider\b/i, /binlerce/i,
  ];
  for (const file of files) {
    const text = readFileSync(file, "utf8");
    for (const pattern of banned) expect(text, `${file} ${pattern}`).not.toMatch(pattern);
  }
});

test("her modülün geçerli bir durumu ve spec'teki dağılımı vardır", () => {
  const byId = Object.fromEntries(modules.map((m) => [m.id, m.status]));
  expect(byId).toEqual({
    "risk-merkezi": "building", personel: "active", izin: "active",
    puantaj: "pilot", bordro: "pilot", "ise-alim": "active",
  });
  for (const m of modules) expect(Object.keys(STATUS_META)).toContain(m.status);
  expect(STATUS_META.active.label).toBe("Kullanımda");
  expect(STATUS_META.pilot.label).toBe("Pilotta");
  expect(STATUS_META.building.label).toBe("Geliştiriliyor");
});

// I2: fiyatlandırma ve rol metinleri pilot/geliştiriliyor modülleri sıradan paket özelliği gibi
// sunmamalı — "Her modülün durumu sitede ... açıkça belirtilir" taahhüdüyle çelişir.
test("pilotta ya da geliştiriliyor durumundaki modüller paket özelliklerinde durum etiketiyle işaretlenir", () => {
  const nonActivePattern = /Risk Merkezi|Bordro|puantaj/i;
  for (const plan of plans) {
    for (const f of plan.features) {
      if (nonActivePattern.test(f.label)) expect(f.status, `${plan.id}: ${f.label}`).toBeDefined();
    }
  }
});

test("pilotta ya da geliştiriliyor durumundaki modüller karşılaştırma tablosunda durum etiketiyle işaretlenir", () => {
  const nonActivePattern = /Risk Merkezi|Bordro|puantaj/i;
  for (const row of comparisonRows) {
    if (nonActivePattern.test(row.label)) expect(row.status, row.label).toBeDefined();
  }
});

test("risk sinyallerinden bahseden rol metinleri Risk Merkezi'nin geliştirilmekte olduğunu açıkça belirtir", () => {
  for (const r of roleViews) {
    const text = [r.text, ...r.points].join(" ");
    if (/risk/i.test(text)) {
      expect(text).toMatch(/geliştiriliyor|Risk Merkezi'nin genel kullanıma/);
    }
  }
});

test("geliştirilen modül kullanımda ya da tahmin yapıyormuş gibi anlatılmaz", () => {
  for (const m of modules.filter((x) => x.status === "building")) {
    const text = [m.summary, ...m.points].join(" ");
    expect(text).not.toMatch(/tahmin|kullanımda/i);
  }
});

test("yol haritası ve taahhütler tarih, çeyrek ya da fiyat içermez", () => {
  const texts = [...roadmap.flatMap((r) => [r.title, r.text]), ...commitments.flatMap((c) => [c.title, c.text]), ROADMAP_NOTE];
  for (const t of texts) {
    expect(t).not.toMatch(/\b20\d{2}\b/);
    expect(t).not.toMatch(/çeyrek/i);
    expect(t).not.toMatch(/₺|\bTL\b/);
  }
});

test("içerik listeleri spec'teki yapıdadır", () => {
  expect(ROADMAP_STAGES.map((s) => s.id)).toEqual(["now", "next", "planned"]);
  expect(roadmap.filter((r) => r.stage === "now")).toHaveLength(2);
  expect(roadmap.filter((r) => r.stage === "next")).toHaveLength(2);
  expect(roadmap.filter((r) => r.stage === "planned")).toHaveLength(3);
  expect(ROADMAP_NOTE).toBe("Yol haritası, planlanan çalışmaları gösterir; yayın tarihi taahhüdü içermez.");
  expect(devProcess).toHaveLength(6);
  expect(principles).toHaveLength(4);
  expect(commitments).toHaveLength(3);
  expect(journeySteps.map((s) => s.title)).toEqual(["İşe alım", "Özlük", "İzin", "Puantaj", "Bordro"]);
  expect(securityLayers).toHaveLength(5);
  expect(securityCategories.map((c) => c.title)).toEqual(["Veri", "Erişim", "İşletim", "Gizlilik (KVKK)"]);
  expect(transparency.inProgress).toEqual(["Çok faktörlü kimlik doğrulama", "Yedekleme ve geri dönüş güvencesi"]);
  expect(SHOWCASE_ORDER).toEqual(["personel", "izin", "puantaj", "bordro", "ise-alim", "risk-merkezi"]);
});
