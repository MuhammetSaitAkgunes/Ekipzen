import { Link } from "react-router-dom";
import { appPath } from "../../paths";

type ReviewStep = {
  title: string;
  check: string;
  reason: string;
  path: string;
  hrOnly?: boolean;
};

const STEPS: ReviewStep[] = [
  {
    title: "İzin kararları",
    check: "Bekleyen talebin kişi, tür, tarih ve açıklamasını açın.",
    reason: "Onay veya ret, gerçek talebe dayanmalı; başlangıç tarihi karar için kayıtlı son tarih değildir.",
    path: "/inbox?tab=approvals",
  },
  {
    title: "Çalışan kayıtları",
    check: "Yeni işe giriş, değişiklik veya ayrılış varsa ilgili personel kartını inceleyin.",
    reason: "Kartın açılması belge, ücret ve uygulama dışı adımların tamamlandığını göstermez.",
    path: "/personnel",
  },
  {
    title: "Mesai ve puantaj",
    check: "Çalışma günü, gün tipi ve mola kayıtlarını ilgili dönemde karşılaştırın.",
    reason: "Yanlış veya eksik girdi bordro hazırlığından önce düzeltilmelidir.",
    path: "/attendance",
  },
  {
    title: "Belge takibi",
    check: "Eksik veya süresi yaklaşan kaydı ve sorumlu kişiyi kontrol edin.",
    reason: "Durum etiketi gerçek dosya ve son tarih ile doğrulanmalıdır.",
    path: "/risk/compliance",
  },
  {
    title: "Aksiyonlar",
    check: "Açık aksiyonun sahibini ve sonraki takip adımını inceleyin.",
    reason: "Bir sinyalin görünmesi, gerekli insan takibinin yapıldığı anlamına gelmez.",
    path: "/inbox?tab=actions",
  },
  {
    title: "Adaylar ve pozisyonlar",
    check: "Yeni adayları, aşama gerekçelerini ve açık kadroları inceleyin.",
    reason: "Teklif veya red aşaması adaya yapılan dış iletişimi kanıtlamaz.",
    path: "/recruitment",
    hrOnly: true,
  },
  {
    title: "Bordro dönemi",
    check: "İlgili ayın dönemini, satır kontrolünü ve onay durumunu inceleyin.",
    reason: "Dönem onayı ödeme veya dış bildirimin yapıldığını göstermez.",
    path: "/payroll",
    hrOnly: true,
  },
];

export function DailyReviewRoute({ isHrAdmin }: { isHrAdmin: boolean }) {
  const steps = STEPS.filter((step) => isHrAdmin || !step.hrOnly);
  return <section className="ez-daily-review-route" aria-labelledby="daily-review-route-title">
    <div className="ez-daily-review-route-head">
      <h2 id="daily-review-route-title">Günlük kontrol rotası</h2>
      <p>Bu bağlantılar ekranları hangi amaçla inceleyeceğinizi anlatır. Otomatik görev, son tarih veya tamamlanma kanıtı değildir. Gerçek kayıt yoksa işlem yapmanız gerekmez.</p>
    </div>
    <ol>
      {steps.map((step) => <li key={step.title}>
        <article className="ez-daily-review-step">
          <h3>{step.title}</h3>
          <p><strong>Neye bakmalı?</strong> {step.check}</p>
          <p><strong>Neden?</strong> {step.reason}</p>
          <Link to={appPath(step.path)}>{step.title} ekranını aç <span aria-hidden="true">→</span></Link>
        </article>
      </li>)}
    </ol>
  </section>;
}
