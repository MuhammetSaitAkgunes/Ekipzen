import { Link } from "react-router-dom";
import { appPath } from "../../paths";
import { PageError, PageLoading } from "../shared/PageState";
import { usePersonnelEntryJourney, type PersonnelEntryJourney } from "./journeyQueries";
import { GuidanceProcessMap } from "./GuidanceProcessMap";

const stepLabels: Record<string, string> = {
  "tab-kimlik": "Kimlik ve temel bilgiler",
  "tab-iletisim": "İletişim",
  "tab-is": "İş ve kurumsal bilgiler",
  "tab-mali": "Mali bilgiler",
  "tab-ozluk": "Özlük bilgileri",
  "tab-evrak": "Belgeler",
};

function JourneyEvidence({ journey }: { journey: PersonnelEntryJourney }) {
  const step = stepLabels[journey.bookmarkStepKey] ?? stepLabels["tab-kimlik"];
  return <section className="ez-panel ez-journey-card" aria-labelledby="journey-personnel-entry">
    <span className="ez-overline">İŞE GİRİŞ · İLK REHBER</span>
    <h2 id="journey-personnel-entry">Çalışan kartı oluşturma</h2>
    <dl>
      <div><dt>Rehber konumu</dt><dd>{journey.hasSavedPosition ? step : "Henüz konum kaydedilmedi"}</dd></div>
      <div><dt>Doğrulanmış kart oluşturma</dt><dd>{journey.verifiedEmployeeRecords} kayıt</dd></div>
      {journey.lastVerifiedAtUtc && <div><dt>Son gerçek işlem</dt><dd>{new Date(journey.lastVerifiedAtUtc).toLocaleDateString("tr-TR")}</dd></div>}
    </dl>
    <p>Bu kanıt yalnız çalışan kartı oluşturmayı gösterir. Evrak, ücret paketi ve uygulama dışındaki adımların tamamlandığı ya da tüm işe giriş sürecinde bağımsız yeterlilik kazanıldığı anlamına gelmez.</p>
    <p>Rehber konumu form taslağı değildir; kaydedilmemiş alanları yeniden girmeniz gerekebilir.</p>
    <Link className="btn btn-primary" to={appPath("/personnel/new")}>Gerçek bir işe girişte rehberi aç</Link>
  </section>;
}

export function GuidanceJourneyPage() {
  const query = usePersonnelEntryJourney();
  return <div className="ez-journey-page">
    <div className="page-header">
      <div><span className="ez-overline">İK REHBERİ</span><h1>Öğrenme yolculuğum</h1>
        <p>Gerçek işlemlerden gelen kanıtları ve rehberde kaldığınız bölümü görün.</p></div>
    </div>
    {query.isPending ? <PageLoading /> : query.isError
      ? <PageError error={query.error} />
      : <JourneyEvidence journey={query.data} />}
    <GuidanceProcessMap />
  </div>;
}
