import { useState } from "react";
import { Link } from "react-router-dom";
import { useAuth } from "../../auth/AuthContext";
import { appPath } from "../../paths";
import { processTitles, useGuidanceCatalog, useGuidanceContentReadings } from "./catalogQueries";
import { useGuidanceOperations } from "./operationQueries";
import { usePublishedPracticeProgress } from "./publishedPracticeQueries";
import { guidanceStepAudience, guidanceStepTitles } from "./stepDefinitions";
import { guidanceProcessPath } from "./processNavigation";

export function GuidanceCoverageJourney() {
  const [open, setOpen] = useState(false);
  return <details className="ez-panel ez-journey-card ez-guidance-coverage" onToggle={event => setOpen(event.currentTarget.open)}>
    <summary>Tüm süreçlerde güncel öğrenme durumum</summary>
    {open && <Coverage />}
  </details>;
}
function Coverage() {
  const { user } = useAuth();
  const catalog = useGuidanceCatalog(user?.role);
  const readings = useGuidanceContentReadings(user?.id);
  const practice = usePublishedPracticeProgress(user?.id, !!user?.id);
  const operations = useGuidanceOperations(user?.id);
  const queries = [catalog, readings, practice, operations];
  if (queries.some(query => query.isPending)) return <p>Güncel öğrenme durumunuz yükleniyor.</p>;
  if (queries.some(query => query.isError)) return <p role="alert">Öğrenme durumu doğrulanamadı. <button type="button" onClick={() => queries.forEach(query => void query.refetch())}>Durumu yeniden yükle</button></p>;
  return <>
    <p>İnceleme beyanı, alıştırma ve gerçek işlem ayrı izlenir. Yeni içerik sürümü eski kanıtları devralmaz. Bu görünüm bağımsız yeterlilik veya bütün İK işlerinin tamamlanması anlamına gelmez.</p>
    {Object.entries(guidanceStepTitles).map(([process, steps]) => {
      const path = guidanceProcessPath(process, user?.role);
      if (!path) return null;
      return <section key={process}><h3>{processTitles[process]}</h3><Link to={appPath(path)}>Bu sürecin ekranını aç</Link>
        <ul>{Object.entries(steps).filter(([step]) => guidanceStepAudience(process, step).some(role => role === user?.role)).map(([step, title]) => {
          const content = catalog.data!.find(item => item.state === "published" && item.isCurrent && item.processKey === process && item.body.stepKey === step);
          if (!content) return <li key={step}><strong>{title}</strong><p>Bu adımın güncel onaylı içeriği henüz yayımlanmamış.</p></li>;
          const read = readings.data!.some(item => item.isCurrent && item.contentRevisionId === content.id);
          const answer = practice.data!.find(item => item.isCurrent && item.contentRevisionId === content.id && item.processKey === process && item.stepKey === step);
          const count = operations.data!.filter(item => item.isCurrent && item.evidence.content.contentRevisionId === content.id && item.evidence.content.processKey === process && item.evidence.content.stepKey === step).length;
          return <li key={step}><strong>{title} · Sürüm {content.contentVersion}</strong>
            <p>{read ? "İncelediğinize dair beyan var" : "İnceleme beyanı yok"} · {answer ? answer.lastCorrect ? "Son alıştırma yanıtı doğru" : "Alıştırmayı yeniden çalışın" : "Alıştırma yanıtı yok"} · {count ? count + " gerçek işlem kanıtı" : "Gerçek işlem kanıtı yok"}</p>
            <Link to={appPath("/guidance/catalog?version=" + content.id)}>Bu sürümün açıklamasını ve alıştırmasını aç</Link>
          </li>;
        })}</ul>
      </section>;
    })}
  </>;
}
