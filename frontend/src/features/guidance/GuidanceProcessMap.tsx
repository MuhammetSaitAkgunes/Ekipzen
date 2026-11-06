import { Link } from "react-router-dom";
import { appPath } from "../../paths";

type Process = {
  key: string;
  title: string;
  purpose: string;
  connection: string;
  path: string;
  coverage: "entry" | "decision" | "tasks" | "screen";
};

const coverageLabels: Record<Process["coverage"], string> = {
  entry: "İlk adım rehberi var",
  decision: "Karar öncesi kontrol var",
  tasks: "Günlük iş açıklamaları var",
  screen: "Ayrıntılı rehber hazırlanıyor",
};

const processIcons: Record<string, string> = {
  company: "building", recruitment: "briefcase", entry: "user-plus", personnel: "users",
  leave: "calendar-check", attendance: "clock", payroll: "wallet", compliance: "shield-halved",
  offboarding: "arrow-right-from-bracket", daily: "list-check",
};

const processes: Process[] = [
  {
    key: "company", title: "Şirket hazırlığı", purpose: "Şirket bilgilerini ve departman yapısını hazırlayın.",
    connection: "Başlangıç kayıtlarını, şirket profilini ve gerçek departmanı kontrol edip çalışan kartına geçin.", path: "/settings", coverage: "entry",
  },
  {
    key: "recruitment", title: "İşe alım", purpose: "Pozisyonu ve adayları değerlendirme aşamalarında izleyin.",
    connection: "Aday aşamasını gerekçesiyle doğrulayın; teklif veya red etiketi tek başına dış iletişim değildir.", path: "/recruitment", coverage: "decision",
  },
  {
    key: "entry", title: "İşe giriş", purpose: "Gerçek çalışan kartını açıp temel iş bilgilerini doğrulayın.",
    connection: "Karttan sonra belge ve ücret paketi adımlarını ayrıca kontrol edin.", path: "/personnel/new", coverage: "entry",
  },
  {
    key: "personnel", title: "Personel yönetimi", purpose: "Mevcut kartları, değişiklikleri ve belgeleri izleyin.",
    connection: "Doğru kişiyi bulup kartını belgeyle karşılaştırın; bilgi ve durum düzeltmelerini ayrı akışlarda yapın.", path: "/personnel", coverage: "decision",
  },
  {
    key: "leave", title: "İzin ve onay", purpose: "Bekleyen talepleri gerçek tür, tarih ve açıklamayla inceleyin.",
    connection: "Karardan önce eksikleri netleştirin; sonuç ilgili izin kaydına işlenir.", path: "/inbox?tab=approvals", coverage: "decision",
  },
  {
    key: "attendance", title: "Mesai ve puantaj", purpose: "Çalışma kayıtlarıyla dönem özetini karşılaştırın.",
    connection: "Eksik veya hatalı girdileri bordro hazırlığından önce inceleyin.", path: "/attendance", coverage: "decision",
  },
  {
    key: "payroll", title: "Bordro", purpose: "Dönemi, girdileri, hesaplamayı ve onayı ayrı aşamalar olarak izleyin.",
    connection: "Dönem açılışında son dönemi, onayda gerçek satırları kontrol edin; uygulama dış ödeme yapmaz.", path: "/payroll", coverage: "decision",
  },
  {
    key: "compliance", title: "Uyum ve belgeler", purpose: "Eksik veya süresi yaklaşan belge kayıtlarını inceleyin.",
    connection: "Belge durumunu gerçek dosya ve sorumlu kişiyle doğrulayın.", path: "/risk/compliance", coverage: "decision",
  },
  {
    key: "offboarding", title: "İşten ayrılış", purpose: "İlgili çalışan dosyasından ayrılış hazırlığını takip edin.",
    connection: "Taslak hesap ile tamamlanmış işlem ve dış yükümlülükleri ayırın.", path: "/personnel", coverage: "screen",
  },
  {
    key: "daily", title: "Günlük yönetim ve analiz", purpose: "Bekleyen işleri, sorumluları ve risk sinyallerini gözden geçirin.",
    connection: "İş kartının gerekçesinden ilgili gerçek kayda ilerleyin.", path: "/inbox?tab=daily", coverage: "tasks",
  },
];

export function GuidanceProcessMap({ isHrAdmin = true }: { isHrAdmin?: boolean }) {
  const visibleProcesses = isHrAdmin ? processes
    : processes.filter((process) => !["company", "recruitment", "entry", "payroll", "offboarding"].includes(process.key));
  return <section className="ez-process-map" aria-labelledby="guidance-process-map">
    <div className="ez-process-map-heading">
      <h2 id="guidance-process-map">İK süreç haritası</h2>
      <p>İhtiyacınız olan süreci seçin; rehberini veya işlem ekranını açın.</p>
    </div>
    <ol>
      {visibleProcesses.map((process) => <li key={process.key}>
        <article className="ez-process-map-card">
          <div className="ez-process-title"><span className="ez-process-icon"><i className={`fa-solid fa-${processIcons[process.key]}`} aria-hidden="true" /></span><h3>{process.title}</h3></div>
          <p>{process.purpose}</p>
          <details><summary>Süreç ayrıntısı</summary><p className="ez-process-connection">{process.connection}</p><span className={`ez-process-coverage ez-process-coverage--${process.coverage}`}>{coverageLabels[process.coverage]}</span></details>
          <div className="ez-process-links">
            <Link to={appPath(`/guidance/catalog?process=${process.key === "entry" ? "personnel-entry" : process.key}`)} aria-label={`${process.title} için kaynaklı rehberi aç`}>Rehberi aç</Link>
            <Link to={appPath(process.path)} aria-label={`${process.title} ekranını aç`}>Ekranı aç <span aria-hidden="true">→</span></Link>
          </div>
        </article>
      </li>)}
    </ol>
    <p className="ez-process-map-note">Durum etiketleri rehber kapsamını gösterir; ekranın bulunması, açıklamalı eğitimin tamamlandığı anlamına gelmez.</p>
  </section>;
}
