import { useAuth } from "../../auth/AuthContext";
import { sourceTitles, useGuidanceCatalogVersion } from "./catalogQueries";
import { useGuidanceHelpPreferences } from "./helpQueries";
import { useGuidanceOperations } from "./operationQueries";
import { usePublishedPracticeProgress } from "./publishedPracticeQueries";
import { GuidanceHelpControl } from "./GuidanceHelpControl";

export function GuidanceStepExplanation({ id }: { id: number }) {
  const { user } = useAuth();
  const query = useGuidanceCatalogVersion(id, user?.role);
  const preferences = useGuidanceHelpPreferences(user?.id);
  const adaptive = !!user?.id && query.data?.isCurrent === true && preferences.isSuccess
    && preferences.data.some(item => item.processKey === query.data?.processKey && item.mode === "adaptive");
  const operations = useGuidanceOperations(user?.id, adaptive);
  const practice = usePublishedPracticeProgress(user?.id, adaptive);
  const adaptiveReady = adaptive && !query.isFetching && !preferences.isFetching
    && operations.isSuccess && !operations.isFetching && practice.isSuccess && !practice.isFetching
    && practice.data.some(item => item.isCurrent && item.lastCorrect === true && item.contentRevisionId === id
      && item.processKey === query.data?.processKey && item.stepKey === query.data?.body.stepKey)
    && operations.data.some(item => item.isCurrent && item.evidence.content.contentRevisionId === id
      && item.evidence.content.processKey === query.data?.processKey && item.evidence.content.stepKey === query.data?.body.stepKey);
  if (query.isPending) return <p>Bu sürümün açıklaması yükleniyor.</p>;
  if (query.isError) return <p role="alert">Adım açıklaması yüklenemedi. <button type="button" onClick={() => void query.refetch()}>Açıklamayı tekrar yükle</button></p>;
  const content = query.data;
  const fields = [["prerequisites", "Başlamadan önce"], ["action", "Ne yapacağız?"], ["why", "Neden yapacağız?"],
    ["example", "Ayrıntılı örnek"], ["correction", "Hata düzeltme yolu"], ["expertEscalation", "Ne zaman uzmana danışmalı?"], ["counterExample", "Kaçınılacak örnek"], ["terms", "Terimler"], ["outcome", "İşlem sonrası ne değişir?"], ["completionEvidence", "Sonucu nasıl doğrularız?"], ["nextStep", "Sonraki adım"]] as const;
  return <section aria-label="Seçilen adımın açıklaması">
    <GuidanceHelpControl process={content.processKey} adaptiveReady={adaptiveReady}>{compact => <>
    {fields.map(([key, title]) => key === "example" && compact
      ? <details key={key}><summary>Ayrıntılı örneği aç</summary><p>{content.body[key]}</p></details>
      : <div key={key}><h4>{title}</h4><p>{content.body[key]}</p></div>)}
    <p>Dayanak türü: {sourceTitles[content.body.sourceKind]} · {content.body.provision}</p>
    <p>Uygulama koşulu: {content.body.applicability}</p>
    {/^https:\/\//i.test(content.body.sourceReference)
      ? <a href={content.body.sourceReference} target="_blank" rel="noopener noreferrer">Bu adımın resmî veya tanımlı kaynağını aç</a>
      : <p>Kaynak: {content.body.sourceReference}</p>}
    <p>Uzman incelemesi: {content.reviewedOn} · Yeniden inceleme: {content.reviewDueOn}.</p>
    {!content.isCurrent && <p role="status">Açıklama seçtiğiniz sürüme aittir; güncel bir işlem yönlendirmesi olarak kullanılmamalı.</p>}
    </>}</GuidanceHelpControl>
  </section>;
}
