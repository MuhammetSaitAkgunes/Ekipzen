import { useState } from "react";
import { useEmployeePersonnelHistory } from "./queries";

const titles: Record<string, string> = { "employee-update": "Kart düzeltme", "employee-document-upload": "Belge yükleme", "employee-document-withdraw": "Belge geri çekme",
  "employee-rehire": "Yeni çalışma dönemi", "employee-compensation-create": "Ücret paketi ekleme", "employee-compensation-correct": "Ücret tutarı/kaynak düzeltme", "employee-compensation-date": "Ücret yürürlük tarihi düzeltme" };
export function PersonnelChangeHistory({ employeeId }: { employeeId: number }) {
  const [open, setOpen] = useState(false);
  const query = useEmployeePersonnelHistory(employeeId, open);
  return <details onToggle={event => setOpen(event.currentTarget.open)}><summary>Son 100 personel işlemi ve gerekçesi</summary>
    {open && (query.isPending ? <p>İşlem geçmişi yükleniyor…</p> : query.isError ? <p role="alert">İşlem geçmişi okunamadı.</p>
      : <><p>Bu kayıtlar gerçek kart ve belge işlemleridir; öğrenme yeterliliği veya uzman onayı değildir. Eski kişisel değerler burada kopyalanmaz.</p>
        {query.data?.length === 0 && <p>Bu izleme başladıktan sonra kayıtlı işlem yok.</p>}
        <ul>{query.data?.map(item => <li key={item.id}><strong>{titles[item.operationKey ?? ""] ?? "Personel işlemi"}</strong>
          {item.occurredAtUtc && <> · {new Date(item.occurredAtUtc).toLocaleString("tr-TR")}</>}
          {item.documentId != null && <> · Belge #{item.documentId}</>}
          {item.employmentPeriodId != null && <> · Çalışma dönemi #{item.employmentPeriodId}</>}
          {item.compensationPackageId != null && <> · Ücret paketi #{item.compensationPackageId}</>}
          {item.relatedPackageId != null && <> · Önceki paket #{item.relatedPackageId}</>}
          {item.toVersion != null && <> · Sürüm {item.fromVersion != null ? `${item.fromVersion} → ` : ""}{item.toVersion}</>}
          <p>{item.changedFields?.join(", ")}</p><p>Gerekçe: {item.reason || "Kaydedilmemiş"}</p>
        </li>)}</ul></>)}
  </details>;
}
