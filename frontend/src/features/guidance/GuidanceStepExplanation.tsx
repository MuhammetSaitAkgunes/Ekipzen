import { useAuth } from "../../auth/AuthContext";
import { Button } from "../../ui";
import { sourceTitles, useGuidanceCatalogVersion, type GuidanceContent } from "./catalogQueries";
import { useGuidanceHelpPreferences } from "./helpQueries";
import { useGuidanceOperations } from "./operationQueries";
import { usePublishedPracticeProgress } from "./publishedPracticeQueries";
import { GuidanceHelpControl } from "./GuidanceHelpControl";

export function GuidanceExplanationBody({ content, compact = false }: { content: GuidanceContent; compact?: boolean }) {
  const { body } = content;
  return <div className="ez-guide-explanation">
    {body.learningGoal && <p className="ez-guide-goal">{body.learningGoal}</p>}
    {!content.isCurrent && <p className="ez-guide-warning" role="status">Açıklama seçtiğiniz sürüme aittir; güncel bir işlem yönlendirmesi olarak kullanılmamalı.</p>}
    <section><span className="ez-guide-number">01</span><h3>Şimdi yap</h3>
      {body.prerequisites && <div className="ez-guide-prerequisite"><strong>Başlamadan önce</strong><p>{body.prerequisites}</p></div>}<p>{body.action}</p></section>
    <section><span className="ez-guide-number">02</span><h3>Neden ve dayanak</h3><p>{body.why}</p>
      <div className="ez-guide-basis"><strong>Dayanak türü: {sourceTitles[body.sourceKind]}</strong><p>{body.provision}</p><p>Uygulama koşulu: {body.applicability}</p></div></section>
    <section><span className="ez-guide-number">03</span><h3>Örnek ve düzeltme</h3>
      {body.example && (compact ? <details><summary>Ayrıntılı örneği aç</summary><p>{body.example}</p></details> : <div className="ez-guide-example"><h4>Ayrıntılı örnek</h4><p>{body.example}</p></div>)}
      {body.correction && <div><h4>Hata düzeltme yolu</h4><p>{body.correction}</p></div>}
      {body.expertEscalation && <div className="ez-guide-warning"><h4>Ne zaman uzmana danışmalı?</h4><p>{body.expertEscalation}</p></div>}
      {body.counterExample && <details><summary>Kaçınılacak örnek</summary><p>{body.counterExample}</p></details>}</section>
    <section><span className="ez-guide-number">04</span><h3>Sonucu kontrol et</h3>
      {body.outcome && <p>{body.outcome}</p>}{body.completionEvidence && <p>{body.completionEvidence}</p>}
      {body.nextStep && <div className="ez-guide-next"><strong>Sonraki adım</strong><p>{body.nextStep}</p></div>}</section>
    <details className="ez-guide-metadata"><summary>Kaynak, terimler ve sürüm bilgisi</summary>
      {body.terms && <><h4>Terimler</h4><p>{body.terms}</p></>}
      {/^(https:\/\/)/i.test(body.sourceReference) ? <a href={body.sourceReference} target="_blank" rel="noopener noreferrer">Bu adımın resmî veya tanımlı kaynağını aç</a> : <p>Kaynak: {body.sourceReference}</p>}
      <p>Sürüm {content.contentVersion} · Uzman incelemesi: {content.reviewedOn || "Bilgi yok"} · Yeniden inceleme: {content.reviewDueOn || "Bilgi yok"}</p>
    </details>
  </div>;
}

export function GuidanceStepSummary({ id }: { id: number }) {
  const { user } = useAuth();
  const query = useGuidanceCatalogVersion(id, user?.role);
  if (query.isPending) return <p role="status">Seçili sürümün ön koşulu ve dayanağı yükleniyor.</p>;
  if (query.isError) return <p role="status">Seçili sürümün dayanağı doğrulanamadı. Ayrıntı için rehberi açın.</p>;
  const { body } = query.data;
  return <div className="ez-guide-collapsed-summary" role="note" aria-label="Kritik ön koşul ve dayanak">
    <strong>Başlamadan önce</strong><p>{body.prerequisites}</p>
    <p><strong>Dayanak:</strong> {body.provision}</p>
    <p><strong>Uygulama koşulu:</strong> {body.applicability}</p>
    {body.expertEscalation && <p><strong>Uzmana danışma:</strong> {body.expertEscalation}</p>}
  </div>;
}

export function GuidanceStepExplanation({ id }: { id: number }) {
  const { user } = useAuth();
  const query = useGuidanceCatalogVersion(id, user?.role);
  const preferences = useGuidanceHelpPreferences(user?.role === "employee" ? null : user?.id);
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
  if (query.isPending) return <p role="status">Bu sürümün açıklaması yükleniyor.</p>;
  if (query.isError) return <p role="alert">Adım açıklaması yüklenemedi. <Button variant="secondary" size="sm" onClick={() => void query.refetch()}>Açıklamayı tekrar yükle</Button></p>;
  return <section aria-label="Seçilen adımın açıklaması">
    {user?.role === "employee" ? <GuidanceExplanationBody content={query.data} />
      : <GuidanceHelpControl process={query.data.processKey} adaptiveReady={adaptiveReady}>{compact => <GuidanceExplanationBody content={query.data} compact={compact} />}</GuidanceHelpControl>}
  </section>;
}
