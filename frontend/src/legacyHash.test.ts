import { expect, test } from "vitest";
import { legacyHashTarget, mapLegacyPath } from "./legacyHash";

test.each([
  ["/login", "/giris"],
  ["/accept-invite", "/davet"],
  ["/dashboard", "/app/dashboard"],
  ["/risk/attrition", "/app/risk/attrition"],
  ["/payroll/settings", "/app/payroll/settings"],
  ["/ozellikler", "/ozellikler"],
  ["/riskli", "/riskli"],
])("mapLegacyPath(%s) → %s", (from, to) => {
  expect(mapLegacyPath(from)).toBe(to);
});

test("hash davet linki sorgusuyla birlikte yeni yola çevrilir", () => {
  expect(legacyHashTarget("#/accept-invite?email=a%40b.c&token=x%2By")).toBe("/davet?email=a%40b.c&token=x%2By");
});

test("hash olmayan ya da çapa olan değerler dokunulmadan kalır", () => {
  expect(legacyHashTarget("")).toBeNull();
  expect(legacyHashTarget("#moduller")).toBeNull();
});
