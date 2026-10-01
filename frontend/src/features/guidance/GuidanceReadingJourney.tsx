import { Link } from "react-router-dom";
import { useAuth } from "../../auth/AuthContext";
import { appPath } from "../../paths";
import { processTitles, useGuidanceContentReadings } from "./catalogQueries";

export function GuidanceReadingJourney() {
  const { user } = useAuth();
  const readings = useGuidanceContentReadings(user?.id);
  return <section className="ez-panel" aria-labelledby="guidance-reading-journey">
    <h2 id="guidance-reading-journey">İncelediğim içerik sürümleri</h2>
    <p>Bu kayıtlar kendi inceleme beyanınızdır. Gerçek iş, doğru karar veya bağımsız yeterlilik kanıtı değildir. Yeni sürüm yayımlandığında eski okuma yeni sürüme taşınmaz.</p>
    {readings.isPending ? <p role="status">İçerik inceleme kayıtları yükleniyor.</p> : readings.isError
      ? <p role="alert">İçerik inceleme kayıtları yüklenemedi. <button className="btn btn-secondary" onClick={() => void readings.refetch()}>Tekrar dene</button></p>
      : readings.data.length === 0 ? <p>Henüz bir içerik sürümünü incelediğinizi kaydetmediniz.</p>
      : <ul>{readings.data.map(item => <li key={item.contentRevisionId}>
        <Link to={appPath(`/guidance/catalog?version=${item.contentRevisionId}`)}>{processTitles[item.processKey]} · {item.title} · Sürüm {item.contentVersion}</Link>
        <p>İlk inceleme: {new Date(item.firstReadAtUtc).toLocaleString("tr-TR")} · {item.isCurrent ? "Güncel sürüm" : "Güncel sürüm değil; yeniden kontrol edin"}</p>
      </li>)}</ul>}
  </section>;
}
