import { Link } from "react-router-dom";
import { useAuth } from "../../auth/AuthContext";
import { appPath } from "../../paths";
import { useGuidanceOperations } from "./operationQueries";

const operationTitles: Record<string, string> = {
  "tenant-setup": "Şirket başlangıç kayıtları", "attendance-create": "Puantaj oluşturma", "attendance-update": "Puantaj düzeltme",
  "compliance-create": "Uyum takip kaydı oluşturma", "compliance-update": "Uyum kayıt bilgilerini düzeltme", "compliance-status": "Uyum durumu değiştirme", "compliance-owner": "Uyum sorumlusu atama",
  "employee-create": "Çalışan kartı oluşturma", "candidate-first-review": "Aday ilk aşama kararı", "leave-decision": "İzin kararı",
};
const outcomes: Record<string, string> = { created: "Kayıt oluşturuldu", initialized: "Eksik başlangıç kayıtları eklendi", updated: "Kayıt düzeltildi", "owner-assigned": "Sorumlu değişti", Missing: "Eksik", InReview: "İncelemede", DueSoon: "Süresi Yaklaşıyor", Completed: "Tamamlandı", Interview: "Mülakat", Offer: "Teklif", Rejected: "Ret", Approved: "Onay" };
export function GuidanceOperationJourney() {
  const { user } = useAuth(); const query = useGuidanceOperations(user?.id);
  return <section className="ez-panel" aria-labelledby="guidance-operation-journey">
    <h2 id="guidance-operation-journey">Rehber sürümüne bağlı gerçek işlemlerim</h2>
    <p>Desteklenen gerçek işlemlerde açıkça seçtiğiniz rehber sürümü, kayıtla birlikte korunur. Her düzeltme ayrı görünür; değişmeyen veriyi kaydetmek veya yalnız önizleme almak kanıt üretmez. Daha eski işlemlere sonradan sürüm atanmaz. Bu kayıtlar tüm sürecin tamamlandığını veya bağımsız yeterliliği kanıtlamaz.</p>
    {query.isPending ? <p>Gerçek işlem kanıtları yükleniyor.</p> : query.isError ? <p role="alert">{query.error.message} <button type="button" onClick={() => void query.refetch()}>İşlem kanıtlarını yeniden yükle</button></p>
      : !query.data.length ? <p>Henüz seçilmiş rehber sürümüyle kaydedilen gerçek işlem yok.</p>
        : <ul>{query.data.map(({ evidence: item, isCurrent }) => <li key={item.evidenceKey}>
          <strong>{operationTitles[item.operationKey] ?? item.operationKey}</strong> · {item.operationKey === "tenant-setup" ? `Şablon yılı ${item.resourceId}` : `Kayıt #${item.resourceId}`} · {outcomes[item.outcome] ?? item.outcome}
          {item.setupChanges && <p>Şirket profili: {item.setupChanges.addedCompanyProfile ? "eklendi" : "korundu"} · İzin türü: {item.setupChanges.addedLeaveTypeCount} eklendi · Tam gün tatil: {item.setupChanges.addedHolidayCount} eklendi · Bordro taslağı: {item.setupChanges.addedPayrollSettings ? "eklendi" : "korundu"}. Uzman onayı veya hukuki doğrulama değildir.</p>}
          <p>{new Date(item.occurredAtUtc).toLocaleString("tr-TR")} · {item.content.title} · Sürüm {item.content.contentVersion}</p>
          {!isCurrent && <p>İşlemde kullanılan sürüm bugün güncel değil; geçmiş kanıt yeni sürüme aktarılmadı.</p>}
          <Link to={appPath(`/guidance/catalog?version=${item.content.contentRevisionId}`)}>İşlemde kullanılan açıklama ve dayanağı aç</Link>
        </li>)}</ul>}
  </section>;
}
