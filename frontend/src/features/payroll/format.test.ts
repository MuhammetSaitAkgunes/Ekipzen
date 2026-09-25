import { describe, expect, test } from "vitest";
import {
  CONTROL_META, PERIOD_STATUS_TEXT, derivePayrollSteps, formatPayrollMoney,
  formatPayrollNumber, payrollStatusClass,
} from "./format";

test("para ve sayı formatı eski payroll.js ile aynı", () => {
  // Birebir Intl çıktısı ortama göre değişebilir; davranışı sabitle:
  expect(formatPayrollMoney(118000.4)).toBe(
    new Intl.NumberFormat("tr-TR", { style: "currency", currency: "TRY", maximumFractionDigits: 0 }).format(118000),
  );
  expect(formatPayrollMoney(undefined)).toBe(
    new Intl.NumberFormat("tr-TR", { style: "currency", currency: "TRY", maximumFractionDigits: 0 }).format(0),
  );
  expect(formatPayrollNumber(1.5)).toBe(
    new Intl.NumberFormat("tr-TR", { minimumFractionDigits: 2, maximumFractionDigits: 2 }).format(1.5),
  );
});

test("durum pill sınıfları eski haritayla birebir", () => {
  expect(payrollStatusClass("Onaylandı")).toBe("approved");
  expect(payrollStatusClass("Onaya Hazır")).toBe("info");
  expect(payrollStatusClass("Kontrol")).toBe("pending");
  expect(payrollStatusClass("Eksik Veri")).toBe("rejected");
  expect(payrollStatusClass("Ön Hesap")).toBe("info");
  expect(payrollStatusClass("bilinmeyen")).toBe("pending");
  expect(PERIOD_STATUS_TEXT.draft).toBe("Taslak");
  expect(CONTROL_META["Eksik IBAN"].icon).toBe("fa-building-columns");
});

describe("derivePayrollSteps", () => {
  const rows = (approved: number, total: number) =>
    Array.from({ length: total }, (_, i) => ({ approvalStatus: i < approved ? "Onaylandı" : "Kontrol" })) as never[];
  const controls = [{ label: "Eksik IBAN", value: 2, level: "high" }] as never[];

  test("taslak dönemde kontrol adımı aktiftir", () => {
    const steps = derivePayrollSteps("draft", rows(0, 3), controls);
    expect(steps.map((s) => s.status)).toEqual(["done", "done", "active", "pending", "pending"]);
    expect(steps[2].meta).toBe("2 uyarı inceleniyor");
    expect(steps[3].meta).toBe("3 kayıt bekliyor");
  });

  test("kontrol döneminde eksik satır varsa kontrol adımı aktif kalır", () => {
    const steps = derivePayrollSteps("control", rows(1, 3), controls);
    expect(steps[2].status).toBe("active");
    expect(steps[3].status).toBe("active");
  });

  test("kontrol döneminde bütün satırlar hazırsa kontrol adımı tamamlanır", () => {
    const ready = [{ approvalStatus: "Onaya Hazır" }, { approvalStatus: "Onaylandı" }] as never[];
    const steps = derivePayrollSteps("control", ready, []);
    expect(steps[2].status).toBe("done");
    expect(steps[3].status).toBe("active");
  });

  test("onaylanan dönemde pusula adımı tamamdır", () => {
    const steps = derivePayrollSteps("approved", rows(3, 3), controls);
    expect(steps[3].status).toBe("done");
    expect(steps[4]).toMatchObject({ status: "done", meta: "Pusulalar hazır" });
  });
});
