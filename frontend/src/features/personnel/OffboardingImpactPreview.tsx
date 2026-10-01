
export type EmployeeOffboardingImpactPreview = {
  action: "complete" | "reverse"; caseId: number; caseVersion: number; employeeName: string;
  terminationDate: string; employmentPeriodId: number | null; compensationPackageId: number | null;
  nextCompensationEndDate: string | null; totalSettlement: number; settlementReference: string;
  teamTransfers: Array<{ employeeId: number; name: string; previousManagerName: string | null; nextManagerName: string | null }>;
  pendingLeaveRequestCount: number; accessOperations: Array<{ employeeId: number; name: string; operation: string }>;
  blockingReasons: string[]; canApply: boolean; token: string;
};
export function OffboardingImpactDetails({ preview }: { preview: EmployeeOffboardingImpactPreview }) {
  return <section aria-label="Ayrılış etki önizlemesi">
    <p><strong>Sunucudaki dosya:</strong> #{preview.caseId} / sürüm {preview.caseVersion} · {preview.employeeName}</p>
    <p><strong>İstihdam dönemi:</strong> #{preview.employmentPeriodId ?? "Bulunamadı"} · {preview.action === "complete" ? preview.terminationDate + " tarihinde kapanacak." : "Yeniden açık hale gelecek; çalışan aktif olacak."}</p>
    <p><strong>Ücret paketi:</strong> {preview.compensationPackageId === null ? "Kapatılacak/açılacak paket yok." : "#" + preview.compensationPackageId + " · İşlem sonrası bitiş: " + (preview.nextCompensationEndDate ?? "Açık")}</p>
    <p><strong>Kayıtlı parasal hesap:</strong> {new Intl.NumberFormat("tr-TR", { style: "currency", currency: "TRY" }).format(preview.totalSettlement)} · {preview.settlementReference}. Önizleme bir uzman doğrulaması veya ödeme değildir.</p>
    <h5>Ekip bağları</h5>
    {preview.teamTransfers.length === 0 ? <p>Ekip bağı değişikliği yok.</p> : <ul>{preview.teamTransfers.map(t => <li key={t.employeeId}>{t.name}: {t.previousManagerName ?? "Yönetici yok"} → {t.nextManagerName ?? "Yönetici yok"}</li>)}</ul>}
    <p>Bu ekipte {preview.pendingLeaveRequestCount} izin talebi karar bekliyor. Taleplere bu işlemle onay/ret verilmeyecek; yönetici kapsamını ayrıca kontrol edin.</p>
    <h5>Kuyruğa alınacak erişim işlemleri</h5>
    <ul>{preview.accessOperations.map((a, i) => <li key={`${a.employeeId}-${i}`}>{a.name}: {a.operation}</li>)}</ul>
    <p>Erişim işlemleri kuyruğa yazılır; tamamlanma durumunu işlemden sonra kontrol edin.</p>
    {preview.blockingReasons.length > 0 && <div role="alert"><strong>İşlem engelleri</strong><p>Engeller çözülmeden bu değişiklikler uygulanmaz.</p><ul>{preview.blockingReasons.map(reason => <li key={reason}>{reason}</li>)}</ul></div>}
  </section>;
}
