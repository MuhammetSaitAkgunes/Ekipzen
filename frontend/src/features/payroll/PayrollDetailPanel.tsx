import { useState } from "react";
import { ApiError, apiDownload, apiFetch } from "../../api/client";
import { useToast } from "../../layout/ToastProvider";
import { formatPayrollMoney, formatPayrollNumber, payrollStatusClass } from "./format";
import { useApprovePayrollRow, useUpdatePayrollRow, type PayrollPeriodDetailDto, type PayrollRowDto } from "./queries";

const Line = ({ label, value, tone = "" }: { label: string; value?: number; tone?: string }) => (
  <div className={`payroll-line ${tone}`}><span>{label}</span><strong>{formatPayrollMoney(value)}</strong></div>
);

type DetailForm = {
  grossSalary: string;
  statutoryProfile: "unspecified" | "domesticPrivateSectorStandardFourA";
  workedDays: string;
  sgkPremiumDaysConfirmed: boolean;
  overtimeHours: string;
  overtimeTreatmentConfirmed: boolean;
  premiumPay: string;
  roadAllowance: string;
  mealAllowance: string;
  mealDays: string;
  mealTreatment: "unspecified" | "cashExemptWhereNoMealProvided" | "fullyTaxable";
  benefitPay: string;
  benefitPayFullyTaxableConfirmed: boolean;
  applyMinimumWageTaxExemption: boolean;
  employerSgkDiscount: "none" | "otherSectorTwoPoints" | "manufacturingFivePoints";
  employerSgkDiscountReference: string;
  disabilityReliefType: "unspecified" | "none" | "firstDegree" | "secondDegree" | "thirdDegree";
  disabilityReliefReference: string;
  otherIncomeTaxBaseDeductionsConfirmedNone: boolean;
  previousEmployerIncomeTaxBase: string;
  previousEmployerIncomeTaxBaseReference: string;
  cumulativeIncomeTaxBaseConfirmed: boolean;
  specialDeductions: string;
  ibanComplete: boolean;
  notes: string;
};

export function PayrollDetailPanel({ periodId, periodName, row, onClose }: {
  periodId: number;
  periodName: string;
  row: PayrollRowDto;
  onClose: () => void;
}) {
  const { showToast } = useToast();
  const updateRow = useUpdatePayrollRow();
  const approveRow = useApprovePayrollRow();
  const [error, setError] = useState<string | null>(null);
  const [form, setForm] = useState<DetailForm>({
    grossSalary: String(row.grossSalary ?? 0),
    statutoryProfile: (row.statutoryProfile as DetailForm["statutoryProfile"]) ?? "unspecified",
    workedDays: String(row.workedDays ?? 30),
    sgkPremiumDaysConfirmed: row.sgkPremiumDaysConfirmed ?? false,
    overtimeHours: String(row.overtimeHours ?? 0),
    overtimeTreatmentConfirmed: row.overtimeTreatmentConfirmed ?? false,
    premiumPay: String(row.premiumPay ?? 0),
    roadAllowance: String(row.roadAllowance ?? 0),
    mealAllowance: String(row.mealAllowance ?? 0),
    mealDays: String(row.mealDays ?? 0),
    mealTreatment: (row.mealTreatment as DetailForm["mealTreatment"]) ?? "unspecified",
    benefitPay: String(row.benefitPay ?? 0),
    benefitPayFullyTaxableConfirmed: row.benefitPayFullyTaxableConfirmed ?? false,
    applyMinimumWageTaxExemption: row.applyMinimumWageTaxExemption ?? true,
    employerSgkDiscount: (row.employerSgkDiscount as DetailForm["employerSgkDiscount"]) ?? "none",
    employerSgkDiscountReference: row.employerSgkDiscountReference ?? "",
    disabilityReliefType: (row.disabilityReliefType as DetailForm["disabilityReliefType"]) ?? "unspecified",
    disabilityReliefReference: row.disabilityReliefReference ?? "",
    otherIncomeTaxBaseDeductionsConfirmedNone: row.otherIncomeTaxBaseDeductionsConfirmedNone ?? false,
    previousEmployerIncomeTaxBase: String(row.previousEmployerIncomeTaxBase ?? 0),
    previousEmployerIncomeTaxBaseReference: row.previousEmployerIncomeTaxBaseReference ?? "",
    cumulativeIncomeTaxBaseConfirmed: row.cumulativeIncomeTaxBaseConfirmed ?? false,
    specialDeductions: String(row.specialDeductions ?? 0),
    ibanComplete: row.ibanComplete ?? true,
    notes: row.notes ?? "",
  });
  const [savedForm] = useState(form);
  const [reviewRow, setReviewRow] = useState<PayrollRowDto | null>(null);
  const [reviewConfirmed, setReviewConfirmed] = useState(false);
  const [reviewLoading, setReviewLoading] = useState(false);
  const hasUnsavedChanges = JSON.stringify(form) !== JSON.stringify(savedForm);

  const rowId = row.id!;
  const isApproved = row.approvalStatus === "Onaylandı";
  const parse = (value: string) => {
    const parsed = Number(String(value).replace(",", "."));
    return Number.isFinite(parsed) ? parsed : 0;
  };
  const setText = (key: keyof DetailForm) => (event: React.ChangeEvent<HTMLInputElement>) =>
    setForm((current) => ({ ...current, [key]: event.target.value }));

  const save = async () => {
    setError(null);
    try {
      await updateRow.mutateAsync({
        periodId,
        rowId,
        model: {
          grossSalary: parse(form.grossSalary),
          statutoryProfile: form.statutoryProfile,
          workedDays: Math.round(parse(form.workedDays)),
          sgkPremiumDaysConfirmed: form.sgkPremiumDaysConfirmed,
          overtimeHours: Math.round(parse(form.overtimeHours)),
          overtimeTreatmentConfirmed: form.overtimeTreatmentConfirmed,
          premiumPay: parse(form.premiumPay),
          roadAllowance: parse(form.roadAllowance),
          mealAllowance: parse(form.mealAllowance),
          mealDays: Math.round(parse(form.mealDays)),
          mealTreatment: form.mealTreatment,
          benefitPay: parse(form.benefitPay),
          benefitPayFullyTaxableConfirmed: form.benefitPayFullyTaxableConfirmed,
          applyMinimumWageTaxExemption: form.applyMinimumWageTaxExemption,
          employerSgkDiscount: form.employerSgkDiscount,
          employerSgkDiscountReference: form.employerSgkDiscountReference.trim() || null,
          disabilityReliefType: form.disabilityReliefType,
          disabilityReliefReference: form.disabilityReliefReference.trim() || null,
          otherIncomeTaxBaseDeductionsConfirmedNone: form.otherIncomeTaxBaseDeductionsConfirmedNone,
          previousEmployerIncomeTaxBase: parse(form.previousEmployerIncomeTaxBase),
          previousEmployerIncomeTaxBaseReference: form.previousEmployerIncomeTaxBaseReference.trim() || null,
          cumulativeIncomeTaxBaseConfirmed: form.cumulativeIncomeTaxBaseConfirmed,
          specialDeductions: parse(form.specialDeductions),
          ibanComplete: form.ibanComplete,
          notes: form.notes.trim() || null,
        },
      });
      showToast(`${row.name} bordro girdileri kaydedildi.`, "success");
      onClose();
    } catch (caught) {
      setError(caught instanceof ApiError ? caught.message : "Girdiler kaydedilemedi.");
    }
  };

  const openApprovalReview = async () => {
    setError(null);
    if (hasUnsavedChanges) {
      setError("Kaydedilmemiş değişiklikler var. Önce Kaydet ile sunucuya yazın, sonra satırı yeniden açıp onaylayın.");
      showToast("Önce bordro değişikliklerini kaydedin ve satırı yeniden açın.", "warning");
      return;
    }
    setReviewLoading(true);
    try {
      const detail = await apiFetch<PayrollPeriodDetailDto>("/payroll/periods/" + periodId);
      const current = detail.rows?.find((item) => item.id === rowId);
      if (!current) throw new Error("Satır artık bu dönemde bulunamadı.");
      if (current.approvalStatus === "Onaylandı") throw new Error("Satır başka bir işlemde onaylandı. Dönem listesini yenileyin.");
      setReviewRow(current);
      setReviewConfirmed(false);
    } catch (caught) {
      const message = caught instanceof Error ? caught.message : "Satır yeniden okunamadı.";
      setError(message);
      showToast(message, "error");
    } finally {
      setReviewLoading(false);
    }
  };

  const approve = async () => {
    if (!reviewRow || !reviewConfirmed || hasUnsavedChanges) return;
    setError(null);
    try {
      await approveRow.mutateAsync({ periodId, rowId });
      showToast(`${row.name} bordrosu kanuni kapılardan geçerek onaylandı.`, "success");
      onClose();
    } catch (caught) {
      setError(caught instanceof ApiError ? caught.message : "Satır onaylanamadı.");
    }
  };

  const downloadSlip = async () => {
    try {
      const { blob, fileName } = await apiDownload(`/payroll/periods/${periodId}/rows/${rowId}/payslip`);
      const link = document.createElement("a");
      link.href = URL.createObjectURL(blob);
      link.download = fileName ?? `bordro-${periodName}.pdf`;
      document.body.appendChild(link);
      link.click();
      link.remove();
      URL.revokeObjectURL(link.href);
      showToast("Bordro pusulası indirildi.", "success");
    } catch (caught) {
      showToast(caught instanceof ApiError ? caught.message : "Pusula indirilemedi.", "error");
    }
  };

  const inputFields: [keyof DetailForm, string][] = [
    ["grossSalary", "Aylık brüt ücret"],
    ["workedDays", "SGK prim günü"],
    ["overtimeHours", "Normal fazla çalışma saati"],
    ["premiumPay", "Prime tabi prim"],
    ["roadAllowance", "Nakit yol yardımı (tam tabi)"],
    ["mealAllowance", "Nakit yemek yardımı"],
    ["mealDays", "Yemek fiili gün sayısı"],
    ["benefitPay", "Diğer ödeme (tam tabi)"],
    ["previousEmployerIncomeTaxBase", "İlk dönem önceki işveren GV matrahı"],
    ["specialDeductions", "Netten özel kesinti"],
  ];

  return (
    <div className="payroll-detail-panel">
      <div className="payroll-detail-head">
        <div>
          <span className={`status-pill ${payrollStatusClass(row.approvalStatus)}`}>{row.approvalStatus}</span>
          <h3>{row.name}</h3>
          <p>{row.title} · {row.department} · {periodName}</p>
        </div>
        <button className="btn-icon-sm" onClick={onClose} title="Kapat" aria-label="Bordro detayını kapat"><i aria-hidden="true" className="fa-solid fa-xmark" /></button>
      </div>

      {error && <p className="form-error" role="alert">{error}</p>}

      <div className="payroll-detail-summary">
        <div><span>Brüt Kazanç</span><strong>{formatPayrollMoney(row.grossEarnings)}</strong></div>
        <div><span>Toplam Kesinti</span><strong>{formatPayrollMoney(row.totalDeductions)}</strong></div>
        <div><span>Net Ücret</span><strong>{formatPayrollMoney(row.netPay)}</strong></div>
        <div><span>İşveren Maliyeti</span><strong>{formatPayrollMoney(row.employerCost)}</strong></div>
      </div>

      {!isApproved && (
        <section className="card payroll-detail-section payroll-input-section">
          <div className="card-header-clean">
            <div><h4>Kanuni girdiler ve sınıflandırmalar</h4><p className="text-muted">Eksik doğrulama satır onayını sunucuda engeller.</p></div>
          </div>
          <div className="form-grid">
            <div className="input-group col-6">
              <label className="input-label" htmlFor="pd-statutoryProfile">Kanuni çalışan profili</label>
              <select id="pd-statutoryProfile" className="input-control" value={form.statutoryProfile} onChange={(e) => setForm((current) => ({ ...current, statutoryProfile: e.target.value as DetailForm["statutoryProfile"] }))}>
                <option value="unspecified">Doğrulanmadı</option>
                <option value="domesticPrivateSectorStandardFourA">Yurt içi özel sektör standart 4/a</option>
              </select>
            </div>
            <div className="input-group col-3">
              <label className="input-label" htmlFor="pd-mealTreatment">Yemek vergi/SGK sınıfı</label>
              <select id="pd-mealTreatment" className="input-control" value={form.mealTreatment} onChange={(e) => setForm((current) => ({ ...current, mealTreatment: e.target.value as DetailForm["mealTreatment"] }))}>
                <option value="unspecified">Yemek yok / seçilmedi</option>
                <option value="cashExemptWhereNoMealProvided">İşyerinde yemek yok, nakit istisna</option>
                <option value="fullyTaxable">Tam prime ve vergiye tabi</option>
              </select>
            </div>
            <div className="input-group col-3">
              <label className="input-label" htmlFor="pd-disabilityReliefType">Engellilik indirimi</label>
              <select id="pd-disabilityReliefType" className="input-control" value={form.disabilityReliefType} onChange={(e) => setForm((current) => ({ ...current, disabilityReliefType: e.target.value as DetailForm["disabilityReliefType"] }))}>
                <option value="unspecified">Seçilmedi</option>
                <option value="none">Yok</option>
                <option value="firstDegree">1. derece</option>
                <option value="secondDegree">2. derece</option>
                <option value="thirdDegree">3. derece</option>
              </select>
            </div>
            <div className="input-group col-3">
              <label className="input-label" htmlFor="pd-employerSgkDiscount">SGK işveren indirimi</label>
              <select id="pd-employerSgkDiscount" className="input-control" value={form.employerSgkDiscount} onChange={(e) => setForm((current) => ({ ...current, employerSgkDiscount: e.target.value as DetailForm["employerSgkDiscount"] }))}>
                <option value="none">İndirimsiz</option>
                <option value="otherSectorTwoPoints">Diğer sektör 2 puan</option>
                <option value="manufacturingFivePoints">İmalat 5 puan</option>
              </select>
            </div>
            {inputFields.map(([key, label]) => (
              <div key={key} className="input-group col-3">
                <label className="input-label" htmlFor={`pd-${key}`}>{label}</label>
                <input id={`pd-${key}`} type="number" className="input-control" value={String(form[key])} onChange={setText(key)} />
              </div>
            ))}
            <div className="input-group col-6">
              <label className="input-label" htmlFor="pd-employerSgkDiscountReference">İndirim uygunluk referansı</label>
              <input id="pd-employerSgkDiscountReference" type="text" className="input-control" value={form.employerSgkDiscountReference} onChange={setText("employerSgkDiscountReference")} placeholder="Teşvik/uygunluk kayıt numarası" />
            </div>
            <div className="input-group col-6">
              <label className="input-label" htmlFor="pd-disabilityReliefReference">Engellilik indirimi belge/referansı</label>
              <input id="pd-disabilityReliefReference" type="text" className="input-control" value={form.disabilityReliefReference} onChange={setText("disabilityReliefReference")} placeholder="Vergi indirimi yazısı / uygunluk kayıt numarası" />
            </div>
            <div className="input-group col-6">
              <label className="input-label" htmlFor="pd-previousEmployerIncomeTaxBaseReference">Önceki işveren matrahı talep/kabul referansı</label>
              <input id="pd-previousEmployerIncomeTaxBaseReference" type="text" className="input-control" value={form.previousEmployerIncomeTaxBaseReference} onChange={setText("previousEmployerIncomeTaxBaseReference")} placeholder="Çalışan talebi ve işveren kabul kayıt numarası" />
            </div>
            <div className="input-group col-6">
              <label className="input-label" htmlFor="pd-notes">Denetim notu</label>
              <input id="pd-notes" type="text" className="input-control" value={form.notes} onChange={setText("notes")} />
            </div>
          </div>
          <div className="payroll-rule-checks">
            <label className="leg-item"><input type="checkbox" checked={form.ibanComplete} onChange={(e) => setForm((current) => ({ ...current, ibanComplete: e.target.checked }))} /> IBAN tamam</label>
            <label className="leg-item"><input type="checkbox" checked={form.sgkPremiumDaysConfirmed} onChange={(e) => setForm((current) => ({ ...current, sgkPremiumDaysConfirmed: e.target.checked }))} /> SGK prim gününü doğruladım</label>
            <label className="leg-item"><input type="checkbox" checked={form.overtimeTreatmentConfirmed} onChange={(e) => setForm((current) => ({ ...current, overtimeTreatmentConfirmed: e.target.checked }))} /> Fazla çalışmanın standart %50 zamlı türünü, çalışan onayını ve yıllık 270 saat sınırını doğruladım</label>
            <label className="leg-item"><input type="checkbox" checked={form.benefitPayFullyTaxableConfirmed} onChange={(e) => setForm((current) => ({ ...current, benefitPayFullyTaxableConfirmed: e.target.checked }))} /> Diğer ödemenin tamamı prime ve vergiye tabi</label>
            <label className="leg-item"><input type="checkbox" checked={form.applyMinimumWageTaxExemption} onChange={(e) => setForm((current) => ({ ...current, applyMinimumWageTaxExemption: e.target.checked }))} /> Asgari ücret gelir ve damga vergisi istisnası bu işverende (tek/en yüksek ücretli işveren) uygulanır</label>
            <label className="leg-item"><input type="checkbox" checked={form.otherIncomeTaxBaseDeductionsConfirmedNone} onChange={(e) => setForm((current) => ({ ...current, otherIncomeTaxBaseDeductionsConfirmedNone: e.target.checked }))} /> Engellilik dışında kişisel sigorta/sendika vb. GV matrah indirimi bulunmuyor</label>
            <label className="leg-item"><input type="checkbox" checked={form.cumulativeIncomeTaxBaseConfirmed} onChange={(e) => setForm((current) => ({ ...current, cumulativeIncomeTaxBaseConfirmed: e.target.checked }))} /> Kümülatif GV matrahını ve varsa önceki işveren açılışını doğruladım</label>
            <span className={`status-pill ${row.timesheetComplete ? "approved" : "pending"}`}>{row.timesheetComplete ? "Puantaj sistem tarafından tamamlandı" : "Puantaj sistem kontrolünde eksik"}</span>
          </div>
        </section>
      )}

      <div className="payroll-detail-grid">
        <section className="card payroll-detail-section">
          <div className="card-header-clean"><h4>Kazançlar</h4></div>
          <Line label="Brüt ücret" value={row.baseGross} />
          <Line label="Fazla çalışma" value={row.overtimePay} />
          <Line label="Prime tabi prim" value={row.premiumPay} />
          <Line label="Nakit yol (tam tabi)" value={row.roadAllowance} />
          <Line label="Nakit yemek" value={row.mealAllowance} />
          <Line label="Diğer ödeme (tam tabi)" value={row.benefitPay} />
          <div className="payroll-note compact"><i aria-hidden="true" className="fa-solid fa-circle-info" /><span>{row.notes || "Not yok."}</span></div>
        </section>

        <section className="card payroll-detail-section">
          <div className="card-header-clean"><h4>Kesintiler</h4></div>
          <Line label="SGK işçi payı" value={row.sgkEmployee} tone="deduction" />
          <Line label="İşsizlik işçi payı" value={row.unemploymentEmployee} tone="deduction" />
          <Line label="Gelir vergisi" value={row.incomeTax} tone="deduction" />
          <Line label="Damga vergisi" value={row.stampTax} tone="deduction" />
          <Line label="Netten özel kesinti" value={row.specialDeductions} tone="deduction" />
        </section>

        <section className="card payroll-detail-section">
          <div className="card-header-clean"><h4>Matrahlar ve kanuni istisnalar</h4></div>
          <Line label="Saatlik ücret" value={row.hourlyRate} />
          <Line label="SGK matrahı" value={row.sgkBase} />
          <Line label="Gelir vergisi matrahı" value={row.incomeTaxBase} />
          <Line label="Engellilik matrah indirimi" value={row.disabilityIncomeTaxBaseDeduction} />
          <Line label="Önceki kümülatif matrah" value={row.previousTaxBase} />
          <Line label="Önceki işveren açılış matrahı" value={row.previousEmployerIncomeTaxBase} />
          <Line label="Dönem sonu kümülatif" value={(row.previousTaxBase ?? 0) + (row.incomeTaxBase ?? 0)} />
          <Line label="Yemek SGK istisnası" value={row.sgkExemptMealAllowance} />
          <Line label="Yemek GV istisnası" value={row.incomeTaxExemptMealAllowance} />
          <Line label="Asgari ücret GV istisnası" value={row.minimumWageIncomeTaxExemption} />
          <Line label="Asgari ücret damga istisnası" value={row.minimumWageStampTaxExemption} />
          <div className="payroll-line"><span>Uygulanan SGK işveren oranı</span><strong>%{formatPayrollNumber(row.appliedEmployerSgkRate)}</strong></div>
          <div className="payroll-warning-stack">
            {(row.warnings ?? []).length > 0
              ? row.warnings?.map((warning) => <span key={warning} className="status-pill pending">{warning}</span>)
              : <span className="status-pill approved">Uyarı yok</span>}
          </div>
        </section>

        <section className="card payroll-slip-preview">
          <div className="card-header-clean"><h4>Bordro Pusulası Önizleme</h4><span className="status-pill info">Denetimli</span></div>
          <div className="slip-paper">
            <div className="slip-brand"><strong>Ekipzen</strong><span>{periodName} Bordro Pusulası</span></div>
            <div className="slip-row"><span>Personel</span><strong>{row.name}</strong></div>
            <div className="slip-row"><span>SGK prim günü</span><strong>{row.workedDays}</strong></div>
            <div className="slip-row"><span>Fazla çalışma</span><strong>{row.overtimeHours ?? 0} saat · {formatPayrollMoney(row.overtimePay)}</strong></div>
            <div className="slip-row"><span>Brüt kazanç</span><strong>{formatPayrollMoney(row.grossEarnings)}</strong></div>
            <div className="slip-row"><span>Yasal kesintiler</span><strong>{formatPayrollMoney((row.totalDeductions ?? 0) - (row.specialDeductions ?? 0))}</strong></div>
            <div className="slip-row"><span>Netten özel kesinti</span><strong>{formatPayrollMoney(row.specialDeductions)}</strong></div>
            <div className="slip-row total"><span>Net ödenecek</span><strong>{formatPayrollMoney(row.netPay)}</strong></div>
          </div>
        </section>
      </div>

      {hasUnsavedChanges && !isApproved && <p className="ez-payroll-unsaved-note" role="status">Bu formda kaydedilmemiş değişiklikler var. Önce Kaydet ile sunucuya yazın; ardından satırı yeniden açıp onaylayın.</p>}
      <div className="payroll-detail-footer">
        <button className="btn btn-secondary" onClick={onClose}>Kapat</button>
        {isApproved ? (
          <button className="btn btn-primary" onClick={downloadSlip}><i aria-hidden="true" className="fa-solid fa-file-pdf" /> Pusula İndir</button>
        ) : (
          <>
            <button className="btn btn-secondary" onClick={save} disabled={updateRow.isPending}><i aria-hidden="true" className="fa-solid fa-floppy-disk" /> Kaydet</button>
            <button className="btn btn-primary" onClick={openApprovalReview} disabled={approveRow.isPending || reviewLoading}><i aria-hidden="true" className="fa-solid fa-shield-halved" /> Satırı onayla</button>
          </>
        )}
      </div>
      {reviewRow && (
        <div className="modal-overlay ez-payroll-row-overlay" style={{ display: "flex" }}>
          <div className="modal-card scale-in" role="dialog" aria-modal="true" aria-label="Bordro satırı onay ön kontrolü">
            <div className="modal-head">
              <div><h3>Satır onayı ön kontrolü</h3><p>Sunucudan yeniden okunan bordro kaydı</p></div>
              <button className="btn-icon-sm" onClick={() => setReviewRow(null)} aria-label="Ön kontrolü kapat"><i aria-hidden="true" className="fa-solid fa-xmark" /></button>
            </div>
            <div className="modal-body-scroll">
              {error && <p className="form-error" role="alert">{error}</p>}
              <div className="ez-payroll-review">
                <h4>{reviewRow.name} · {periodName}</h4>
                <p><strong>Kaydedilmiş durum:</strong> {reviewRow.approvalStatus} · <strong>Brüt kazanç:</strong> {formatPayrollMoney(reviewRow.grossEarnings)} · <strong>Net ücret:</strong> {formatPayrollMoney(reviewRow.netPay)}</p>
                <p><strong>Ne yapıyoruz?</strong> Satırın hesap sonucu onaylı kayıt olarak saklanır. Onaydan sonra bu satırın girdileri bu ekranda değiştirilemez.</p>
                <p><strong>Neden kontrol ediyoruz?</strong> Gerçek ücret ve yan ödeme belgelerini, puantajı, IBAN bilgisini, matrahları ve sınıflandırmaları kaynak kayıtlarla karşılaştırın.</p>
                <p><strong>Puantaj:</strong> {reviewRow.timesheetComplete ? "Sistem kontrolü tamam" : "Sistem kontrolünde eksik"} · <strong>IBAN:</strong> {reviewRow.ibanComplete ? "Tamam" : "Eksik"}</p>
                {(reviewRow.warnings ?? []).length > 0 && <div><strong>Hesap uyarıları:</strong><ul>{reviewRow.warnings?.map((warning) => <li key={warning}>{warning}</li>)}</ul></div>}
                <p>Bu ön kontrol, sunucunun nihai onay kurallarının yerine geçmez. Eksik veya hatalı kayıt varsa geri dönüp düzeltin; sunucu onay sırasında yeniden denetler.</p>
                <label><input type="checkbox" checked={reviewConfirmed} onChange={(e) => setReviewConfirmed(e.target.checked)} /> Kaydedilmiş satırı ve kaynak bilgilerini kontrol ettim.</label>
              </div>
            </div>
            <div className="modal-footer">
              <button className="btn btn-ghost" onClick={() => setReviewRow(null)}>Düzeltmeye dön</button>
              <button className="btn btn-primary" onClick={approve} disabled={!reviewConfirmed || approveRow.isPending}>Onayı tamamla</button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
