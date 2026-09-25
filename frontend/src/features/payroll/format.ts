import type { PayrollControlDto, PayrollRowDto } from "./queries";

const currency = new Intl.NumberFormat("tr-TR", {
  style: "currency",
  currency: "TRY",
  maximumFractionDigits: 0,
});

const number = new Intl.NumberFormat("tr-TR", {
  minimumFractionDigits: 2,
  maximumFractionDigits: 2,
});

export const formatPayrollMoney = (value?: number): string => currency.format(Math.round(value ?? 0));
export const formatPayrollNumber = (value?: number): string => number.format(value ?? 0);

export const payrollStatusClass = (status?: string | null): string =>
  ({
    "Onaylandı": "approved",
    "Onaya Hazır": "info",
    "Kontrol": "pending",
    "Eksik Veri": "rejected",
    "Ön Hesap": "info",
  })[status ?? ""] ?? "pending";

export const PERIOD_STATUS_TEXT: Record<string, string> = {
  draft: "Taslak",
  control: "Kontrol",
  approved: "Onaylandı",
  closed: "Kapandı",
};

/** Kontrol kartı detay/icon'ları — backend yalnız label/value/level döner (eski payroll.js metinleri). */
export const CONTROL_META: Record<string, { detail: string; icon: string }> = {
  "Eksik Puantaj": { detail: "Fazla mesai ve çalışılan gün kapanışı bekliyor.", icon: "fa-clock" },
  "Eksik IBAN": { detail: "Ödeme listesine girmeden tamamlanmalı.", icon: "fa-building-columns" },
  "SGK Matrah Uyarısı": { detail: "Alt/üst sınır ve ücret dışı ödeme etkisi izleniyor.", icon: "fa-shield-halved" },
  "Vergi Matrahı Uyarısı": { detail: "Kümülatif gelir vergisi dilimi değişen kayıtlar.", icon: "fa-scale-balanced" },
  "Onay Bekleyen": { detail: "Kontrol veya onay aşamasında bekleyen bordrolar.", icon: "fa-file-signature" },
};

export type PayrollStepView = {
  id: string;
  label: string;
  status: "done" | "active" | "pending";
  meta: string;
};

/** Eski steps[] sabitinin dönem durumu + satır sayımlarından türetilmiş hali. */
export const derivePayrollSteps = (
  status: string | undefined,
  rows: PayrollRowDto[],
  controls: PayrollControlDto[],
): PayrollStepView[] => {
  const approved = rows.filter((r) => r.approvalStatus === "Onaylandı").length;
  const waiting = rows.length - approved;
  const totalWarnings = controls.reduce((total, c) => total + (c.label === "Onay Bekleyen" ? 0 : c.value ?? 0), 0);
  const periodDone = status === "approved" || status === "closed";
  const allRowsReady = rows.every((row) =>
    row.approvalStatus === "Onaya Hazır" || row.approvalStatus === "Onaylandı",
  );

  return [
    { id: "prep", label: "Hazırlık", status: "done", meta: "Puantaj ve personel verisi" },
    { id: "calc", label: "Hesaplama", status: "done", meta: "Ön hesap oluşturuldu" },
    {
      id: "check", label: "Kontrol",
      status: periodDone || (status === "control" && allRowsReady && totalWarnings === 0) ? "done" : "active",
      meta: `${totalWarnings} uyarı inceleniyor`,
    },
    {
      id: "approve", label: "Onay",
      status: waiting === 0 && rows.length > 0 ? "done" : status === "control" ? "active" : "pending",
      meta: `${waiting} kayıt bekliyor`,
    },
    {
      id: "slip", label: "Pusula",
      status: periodDone ? "done" : "pending",
      meta: periodDone ? "Pusulalar hazır" : "Onay sonrası yayın",
    },
  ];
};
