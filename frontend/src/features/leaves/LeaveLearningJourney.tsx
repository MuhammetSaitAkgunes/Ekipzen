import { Link } from "react-router-dom";
import { useAuth } from "../../auth/AuthContext";
import { appPath } from "../../paths";
import { PublishedGuidancePractice } from "../guidance/PublishedGuidancePractice";
import { useLeaveLearningOperations } from "./queries";

export function LeaveLearningJourney() {
  const { user } = useAuth();
  const query = useLeaveLearningOperations(user?.id);
  return <section className="ez-leave-overview" aria-label="Kendi izin öğrenme yolculuğum">
    <h3>İzin türü, talep ve iptal yolculuğum</h3>
    <p>Tür ve dayanağı kontrol edin → sunucu etkisini inceleyin → gerçek talebi gönderin → sonucu ve gerekiyorsa iptal/iade kaydını doğrulayın. Okuma ve doğru alıştırma yanıtı gerçek işlem değildir.</p>
    <PublishedGuidancePractice process="leave" step="request-lifecycle" />
    <details><summary>Kendi talep ve iptal işlem kanıtlarım</summary>
      {query.isPending ? <p>İzin işlem kanıtları yükleniyor.</p> : query.isError ? <p role="alert">{query.error.message} <button type="button" onClick={() => void query.refetch()}>İzin kanıtlarını yeniden yükle</button></p>
        : !query.data.length ? <p>Henüz seçtiğiniz rehber sürümüyle kaydedilmiş talep veya iptal yok.</p>
          : <ul>{query.data.map(({ evidence, isCurrent }) => <li key={evidence.evidenceKey}>
            <strong>{evidence.operationKey === "leave-request-create" ? "Talep oluşturuldu" : "Talep iptal edildi"}</strong> · Talep #{evidence.resourceId}
            <p>{evidence.preparationChanges?.changedFields.join(" · ")}</p>
            <Link to={appPath(`/guidance/catalog?version=${evidence.content.contentRevisionId}`)}>{evidence.content.title} · Sürüm {evidence.content.contentVersion}</Link>
            {" · "}{isCurrent ? "Güncel ders sürümü" : "Geçmiş ders sürümü"}
            <p>{new Date(evidence.occurredAtUtc).toLocaleString("tr-TR")}. Tarihsel işlem kaydıdır; talebin bugünkü durumu listede ayrıca okunur.</p>
          </li>)}</ul>}
    </details>
    <p>Bu geçmiş yalnız sizin seçilmiş dersle yaptığınız işlemlerden gelir; izin hakkının hukuken doğrulandığı, dış bildirimin yapıldığı veya bağımsız yeterlilik kazanıldığı anlamına gelmez.</p>
  </section>;
}
