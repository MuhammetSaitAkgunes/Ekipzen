import { useAuth } from "../../auth/AuthContext";
import { sourceTitles, useGuidanceCatalogVersion } from "./catalogQueries";

export function GuidanceStepExplanation({ id }: { id: number }) {
  const { user } = useAuth();
  const query = useGuidanceCatalogVersion(id, user?.role);
  if (query.isPending) return <p>Bu sürümün açıklaması yükleniyor.</p>;
  if (query.isError) return <p role="alert">Adım açıklaması yüklenemedi. <button type="button" onClick={() => void query.refetch()}>Açıklamayı tekrar yükle</button></p>;
  const content = query.data;
  const fields = [["prerequisites", "Başlamadan önce"], ["action", "Ne yapacağız?"], ["why", "Neden yapacağız?"],
    ["example", "Ayrıntılı örnek"], ["correction", "Hata düzeltme yolu"], ["expertEscalation", "Ne zaman uzmana danışmalı?"]] as const;
  return <section aria-label="Seçilen adımın açıklaması">
    {fields.map(([key, title]) => <div key={key}><h4>{title}</h4><p>{content.body[key]}</p></div>)}
    <p>Dayanak türü: {sourceTitles[content.body.sourceKind]} · {content.body.provision}</p>
    <p>Uygulama koşulu: {content.body.applicability}</p>
    {/^https:\/\//i.test(content.body.sourceReference)
      ? <a href={content.body.sourceReference} target="_blank" rel="noopener noreferrer">Bu adımın resmî veya tanımlı kaynağını aç</a>
      : <p>Kaynak: {content.body.sourceReference}</p>}
    <p>Uzman incelemesi: {content.reviewedOn} · Yeniden inceleme: {content.reviewDueOn}.</p>
    {!content.isCurrent && <p role="status">Açıklama seçtiğiniz sürüme aittir; güncel bir işlem yönlendirmesi olarak kullanılmamalı.</p>}
  </section>;
}
