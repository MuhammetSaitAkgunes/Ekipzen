import { useState } from "react";
import { Link } from "react-router-dom";
import { useAuth } from "../../auth/AuthContext";
import { appPath } from "../../paths";
import { usePublishedPracticeProgress } from "./publishedPracticeQueries";

export function GuidancePracticeJourney() {
  const { user } = useAuth();
  const [open, setOpen] = useState(false);
  const query = usePublishedPracticeProgress(user?.id, open);
  return <details className="ez-panel" onToggle={event => setOpen(event.currentTarget.open)}>
    <summary>Kaynaklı sürümlere ait alıştırma geçmişim</summary>
    {open && (query.isPending ? <p>Alıştırma geçmişi yükleniyor.</p> : query.isError ? <p role="alert">{query.error.message}</p>
      : <><p>Yeni yayında önceki yanıtlar güncel sürümü tamamlamış sayılmaz. Doğru yanıt, gerçek işlem kanıtı veya yeterlilik onayı değildir.</p>
        {query.data.length === 0 ? <p>Henüz kaynaklı sürüm alıştırması yanıtlamadınız.</p> : <ul>{query.data.map(item => <li key={item.contentRevisionId}>
          <Link to={appPath(`/guidance/catalog?version=${item.contentRevisionId}`)}>{item.title} · Sürüm {item.contentVersion}</Link>
          {" · "}{item.isCurrent ? "Güncel sürüm" : "Geçmiş sürüm"}{" · "}{item.attemptCount} deneme · {item.lastCorrect ? "Son yanıt doğru" : "Yeniden çalışılmalı"}
          {" · "}{new Date(item.lastAnsweredAtUtc).toLocaleString("tr-TR")}
        </li>)}</ul>}</>)}
  </details>;
}
