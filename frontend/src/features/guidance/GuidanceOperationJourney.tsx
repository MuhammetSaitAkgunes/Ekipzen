import { Link } from "react-router-dom";
import { useAuth } from "../../auth/AuthContext";
import { appPath } from "../../paths";
import { useGuidanceOperations } from "./operationQueries";

const operationTitles: Record<string, string> = { "department-delete": "Departman silme", "company-logo-upload": "Şirket logosu yükleme", "company-notifications-update": "Bildirim tercihi değişimi", "employee-photo-upload": "Personel fotoğrafı yükleme",
  "company-profile-update": "Şirket profili düzeltme", "department-create": "Departman ekleme", "department-update": "Departman düzeltme",
  "candidate-stage-decision": "Sonraki aday aşama kararı", "candidate-hire-create": "Adaydan personel oluşturma", "candidate-provisioning-retry": "Hesap hazırlığını yeniden isteme",
  "position-correct": "Pozisyon düzeltme", "candidate-correct": "Aday kaydı düzeltme",
  "candidate-note-create": "Mülakat notu ekleme", "candidate-note-revise": "Mülakat notu yeni sürümü",
  "candidate-evaluation-create": "Değerlendirme ekleme", "candidate-evaluation-revise": "Değerlendirme yeni sürümü",
  "position-create": "Pozisyon açma", "candidate-create": "Aday oluşturma",
  "employee-import": "Toplu personel aktarımı",
  "employee-rehire": "Yeni çalışma dönemi", "employee-compensation-create": "Ücret paketi ekleme", "employee-compensation-correct": "Ücret tutarı/kaynak düzeltme", "employee-compensation-date": "Ücret yürürlük tarihi düzeltme",
  "employee-update": "Personel kartı düzeltme", "employee-document-upload": "Özlük belgesi yükleme", "employee-document-withdraw": "Özlük belgesi geri çekme",
  "payroll-period-create": "Bordro dönemi oluşturma", "payroll-row-approve": "Bordro satırı onayı", "payroll-period-submit": "Bordro dönemi onayı",
  "offboarding-complete": "Ayrılış dosyası tamamlama", "offboarding-reverse": "Ayrılışı kontrollü geri alma",
  "tenant-setup": "Şirket başlangıç kayıtları", "attendance-create": "Puantaj oluşturma", "attendance-update": "Puantaj düzeltme",
  "compliance-create": "Uyum takip kaydı oluşturma", "compliance-update": "Uyum kayıt bilgilerini düzeltme", "compliance-status": "Uyum durumu değiştirme", "compliance-owner": "Uyum sorumlusu atama",
  "employee-create": "Çalışan kartı oluşturma", "candidate-first-review": "Aday ilk aşama kararı", "leave-decision": "İzin kararı",
};
const outcomes: Record<string, string> = { deleted: "Departman silindi", "hire-recorded": "Personel oluşturuldu; hesap hazırlığı istendi", "retry-requested": "Mevcut hesap hazırlığı yeniden istendi; teslim sonucu ayrıca izlenir", New: "Yeni", corrected: "Gerekçeli düzeltme kaydedildi", revised: "Yeni geçerli sürüm kaydedildi", imported: "Personel kayıtları oluşturuldu", rehired: "Yeni dönem açıldı; erişim eşitlemesi istendi", uploaded: "Dosya yüklendi", withdrawn: "Gerekçeyle geri çekildi", created: "Kayıt oluşturuldu", "draft-created": "Taslak dönem oluşturuldu", approved: "Onaylandı", completed: "Dosya tamamlandı", reversed: "Kontrollü geri alındı", initialized: "Eksik başlangıç kayıtları eklendi", updated: "Kayıt düzeltildi", "owner-assigned": "Sorumlu değişti", Missing: "Eksik", InReview: "İncelemede", DueSoon: "Süresi Yaklaşıyor", Completed: "Tamamlandı", Interview: "Mülakat", Offer: "Teklif", Rejected: "Ret", Approved: "Onay" };
export function GuidanceOperationJourney() {
  const { user } = useAuth(); const query = useGuidanceOperations(user?.id);
  return <section className="ez-panel" aria-labelledby="guidance-operation-journey">
    <h2 id="guidance-operation-journey">Rehber sürümüne bağlı gerçek işlemlerim</h2>
    <p>Desteklenen gerçek işlemlerde açıkça seçtiğiniz rehber sürümü, kayıtla birlikte korunur. Her düzeltme ayrı görünür; değişmeyen veriyi kaydetmek veya yalnız önizleme almak kanıt üretmez. Daha eski işlemlere sonradan sürüm atanmaz. Bu kayıtlar tüm sürecin tamamlandığını veya bağımsız yeterliliği kanıtlamaz.</p>
    {query.isPending ? <p>Gerçek işlem kanıtları yükleniyor.</p> : query.isError ? <p role="alert">{query.error.message} <button type="button" onClick={() => void query.refetch()}>İşlem kanıtlarını yeniden yükle</button></p>
      : !query.data.length ? <p>Henüz seçilmiş rehber sürümüyle kaydedilen gerçek işlem yok.</p>
        : <ul>{query.data.map(({ evidence: item, isCurrent }) => <li key={item.evidenceKey}>
          <strong>{operationTitles[item.operationKey] ?? item.operationKey}</strong> · {item.operationKey === "tenant-setup" ? `Şablon yılı ${item.resourceId}` : item.operationKey === "employee-import" ? `Aktarım makbuzu #${item.resourceId}` : `Kayıt #${item.resourceId}`} · {outcomes[item.outcome] ?? item.outcome}
          {item.setupChanges && <p>Şirket profili: {item.setupChanges.addedCompanyProfile ? "eklendi" : "korundu"} · İzin türü: {item.setupChanges.addedLeaveTypeCount} eklendi · Tam gün tatil: {item.setupChanges.addedHolidayCount} eklendi · Bordro taslağı: {item.setupChanges.addedPayrollSettings ? "eklendi" : "korundu"}. Uzman onayı veya hukuki doğrulama değildir.</p>}
          <p>{new Date(item.occurredAtUtc).toLocaleString("tr-TR")} · {item.content.title} · Sürüm {item.content.contentVersion}</p>
            {item.recruitmentChanges && <p>İşe alım kaydı: {item.recruitmentChanges.candidateId ? `Aday #${item.recruitmentChanges.candidateId}` : `Pozisyon #${item.recruitmentChanges.positionId}`} · {({ "stage-decision": "Aşama kararı", note: "Not", evaluation: "Değerlendirme", candidate: "Aday düzeltmesi", position: "Pozisyon düzeltmesi" } as Record<string, string>)[item.recruitmentChanges.recordKind]} #{item.recruitmentChanges.recordId} · Sürüm {item.recruitmentChanges.fromVersion ?? "ilk kayıt"} → {item.recruitmentChanges.toVersion}. {item.recruitmentChanges.reason && `Gerekçe: ${item.recruitmentChanges.reason}`} {item.recruitmentChanges.recordKind === "stage-decision" ? "Karar kaydedildi; iletişim, işe alma ve hukuki uygunluk ayrıca kontrol edilir." : "Bu kanıt aşama, genel puan veya işe alma onayı değildir."}</p>}
          {item.preparationChanges && <p>Gerçek kayıt türü: {({ "company-profile": "Şirket profili", notifications: "Bildirim tercihleri", department: "Departman", position: "Pozisyon", candidate: "Aday" } as Record<string, string>)[item.preparationChanges.resourceKind] ?? item.preparationChanges.resourceKind} #{item.preparationChanges.resourceId} · Alanlar: {item.preparationChanges.changedFields.join(", ")}. Bu tarihsel işlem kanıtı, bütün hazırlığın veya işe alımın tamamlandığı anlamına gelmez.</p>}
          {item.importChanges && <section aria-label="Aktarım kanıtının gerçek sonucu">
            <p>{item.importChanges.createdCount} personel oluşturuldu · {item.importChanges.skippedCount} satır atlandı. Gönderim: {item.importChanges.importRequestId}. Aynı gönderim tek kanıttır; kart hazırlığı ve dış bildirimler ayrıca kontrol edilir.</p>
            <details><summary>Oluşan kartlar ve atlanan satırlar</summary>
              <ul>{item.importChanges.createdEmployeeIds.map(id => <li key={id}><Link to={appPath("/personnel/" + id)}>Çalışan #{id} kartını aç</Link></li>)}</ul>
              <ul>{item.importChanges.skippedRows.map(row => <li key={row.rowNumber}>Excel satırı {row.rowNumber} · {row.outcome === "duplicate" ? "Mevcut kayıt; atlandı" : "Hatalı; atlandı"} · {row.explanation}
                {row.issues.length > 0 && <p>{row.issues.join("; ")}</p>}</li>)}</ul>
            </details>
          </section>}
          {item.personnelChanges && <>
            <p>{item.personnelChanges.employmentPeriodId != null ? `Çalışma dönemi #${item.personnelChanges.employmentPeriodId}` : ""}{item.personnelChanges.compensationPackageId != null ? ` · Ücret paketi #${item.personnelChanges.compensationPackageId}` : ""}{item.personnelChanges.relatedPackageId != null ? ` · Önceki paket #${item.personnelChanges.relatedPackageId}` : ""}{item.personnelChanges.toVersion != null ? ` · Sürüm ${item.personnelChanges.fromVersion != null ? `${item.personnelChanges.fromVersion} → ` : ""}${item.personnelChanges.toVersion}` : ""}</p>
            <p>Çalışan #{item.personnelChanges.employeeId}{item.personnelChanges.documentId ? ` · Belge #${item.personnelChanges.documentId}` : ""} · {item.personnelChanges.changedFields?.join(", ")}{item.personnelChanges.reason ? ` · Gerekçe: ${item.personnelChanges.reason}` : ""}</p></>}
          {!isCurrent && <p>İşlemde kullanılan sürüm bugün güncel değil; geçmiş kanıt yeni sürüme aktarılmadı.</p>}
          <Link to={appPath(`/guidance/catalog?version=${item.content.contentRevisionId}`)}>İşlemde kullanılan açıklama ve dayanağı aç</Link>
        </li>)}</ul>}
  </section>;
}
