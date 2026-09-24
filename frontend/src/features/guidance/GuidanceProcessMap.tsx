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

const processes: Process[] = [
  {
    key: "company", title: "Şirket hazırlığı", purpose: "Şirket, departman ve kullanıcı ayarlarını gözden geçirin.",
    connection: "Departman yapısı çalışan kaydında kullanılacak.", path: "/settings", coverage: "screen",
  },
  {
    key: "recruitment", title: "İşe alım", purpose: "Pozisyonu ve adayları değerlendirme aşamalarında izleyin.",
    connection: "Adayın ilerlemesi ile çalışan kartının oluşmasını ayrı doğrulayın.", path: "/recruitment", coverage: "screen",
  },
  {
    key: "entry", title: "İşe giriş", purpose: "Gerçek çalışan kartını açıp temel iş bilgilerini doğrulayın.",
    connection: "Karttan sonra belge ve ücret paketi adımlarını ayrıca kontrol edin.", path: "/personnel/new", coverage: "entry",
  },
  {
    key: "personnel", title: "Personel yönetimi", purpose: "Mevcut kartları, değişiklikleri ve belgeleri izleyin.",
    connection: "Kayıt düzeltmesinin izin ve bordro girdilerine etkisini değerlendirin.", path: "/personnel", coverage: "screen",
  },
  {
    key: "leave", title: "İzin ve onay", purpose: "Bekleyen talepleri gerçek tür, tarih ve açıklamayla inceleyin.",
    connection: "Karardan önce eksikleri netleştirin; sonuç ilgili izin kaydına işlenir.", path: "/inbox?tab=approvals", coverage: "decision",
  },
  {
    key: "attendance", title: "Mesai ve puantaj", purpose: "Çalışma kayıtlarıyla dönem özetini karşılaştırın.",
    connection: "Eksik veya hatalı girdileri bordro hazırlığından önce inceleyin.", path: "/attendance", coverage: "screen",
  },
  {
    key: "payroll", title: "Bordro", purpose: "Dönemi, girdileri, hesaplamayı ve onayı ayrı aşamalar olarak izleyin.",
    connection: "Uygulamadaki onay, dış ödeme veya bildirimin yapıldığını göstermez.", path: "/payroll", coverage: "screen",
  },
  {
    key: "compliance", title: "Uyum ve belgeler", purpose: "Eksik veya süresi yaklaşan belge kayıtlarını inceleyin.",
    connection: "Belge durumunu gerçek dosya ve sorumlu kişiyle doğrulayın.", path: "/risk/compliance", coverage: "screen",
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

export function GuidanceProcessMap() {
  return <section className="ez-process-map" aria-labelledby="guidance-process-map">
    <div className="ez-process-map-heading">
      <h2 id="guidance-process-map">İK süreç haritası</h2>
      <p>Bu alanlar günlük işte birbirine bağlanır; sıra her durum için zorunlu değildir. Bağlantılar gerçek kayıt ekranlarını açar.</p>
    </div>
    <ol>
      {processes.map((process) => <li key={process.key}>
        <article className="ez-process-map-card">
          <span className={`ez-process-coverage ez-process-coverage--${process.coverage}`}>{coverageLabels[process.coverage]}</span>
          <h3>{process.title}</h3>
          <p>{process.purpose}</p>
          <p className="ez-process-connection"><strong>Bağlantı:</strong> {process.connection}</p>
          <Link to={appPath(process.path)} aria-label={`${process.title} ekranını aç`}>Ekranı aç <span aria-hidden="true">→</span></Link>
        </article>
      </li>)}
    </ol>
    <p className="ez-process-map-note">Durum etiketleri rehber kapsamını gösterir; ekranın bulunması, açıklamalı eğitimin tamamlandığı anlamına gelmez.</p>
  </section>;
}
