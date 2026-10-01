import { Link } from "react-router-dom";
import { useAuth } from "../../auth/AuthContext";
import { appPath } from "../../paths";
import { useGuidanceOperations } from "./operationQueries";

const operationTitles: Record<string, string> = {
  "employee-create": "Çalışan kartı oluşturma", "candidate-first-review": "Aday ilk aşama kararı", "leave-decision": "İzin kararı",
};
const outcomes: Record<string, string> = { created: "Kart oluşturuldu", Interview: "Mülakat", Offer: "Teklif", Rejected: "Ret", Approved: "Onay" };
export function GuidanceOperationJourney() {
  const { user } = useAuth(); const query = useGuidanceOperations(user?.id);
  return <section className="ez-panel" aria-labelledby="guidance-operation-journey">
    <h2 id="guidance-operation-journey">Rehber sürümüne bağlı gerçek işlemlerim</h2>
    <p>Çalışan kartı oluşturma, aday ilk aşama kararı ve izin kararında seçtiğiniz rehber sürümü, gerçek kayıtla birlikte korunur. Daha eski işlemlere sonradan sürüm atanmaz. Bu kayıtlar tüm sürecin tamamlandığını veya bağımsız yeterliliği kanıtlamaz.</p>
    {query.isPending ? <p>Gerçek işlem kanıtları yükleniyor.</p> : query.isError ? <p role="alert">{query.error.message} <button type="button" onClick={() => void query.refetch()}>İşlem kanıtlarını yeniden yükle</button></p>
      : !query.data.length ? <p>Henüz seçilmiş rehber sürümüyle kaydedilen gerçek işlem yok.</p>
        : <ul>{query.data.map(({ evidence: item, isCurrent }) => <li key={`${item.operationKey}:${item.resourceId}`}>
          <strong>{operationTitles[item.operationKey] ?? item.operationKey}</strong> · Kayıt #{item.resourceId} · {outcomes[item.outcome] ?? item.outcome}
          <p>{new Date(item.occurredAtUtc).toLocaleString("tr-TR")} · {item.content.title} · Sürüm {item.content.contentVersion}</p>
          {!isCurrent && <p>İşlemde kullanılan sürüm bugün güncel değil; geçmiş kanıt yeni sürüme aktarılmadı.</p>}
          <Link to={appPath(`/guidance/catalog?version=${item.content.contentRevisionId}`)}>İşlemde kullanılan açıklama ve dayanağı aç</Link>
        </li>)}</ul>}
  </section>;
}
