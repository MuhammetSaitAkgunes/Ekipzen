import { Link, useSearchParams } from "react-router-dom";
import { appPath } from "../../paths";
import { useAuth } from "../../auth/AuthContext";
import { GuidanceNavigation } from "./GuidanceNavigation";
import { GuidanceCoverageJourney } from "./GuidanceCoverageJourney";
import { GuidanceHelpSettings } from "./GuidanceHelpControl";
import { PageError, PageLoading } from "../shared/PageState";
import { useLeaveApprovalJourney, usePersonnelEntryJourney, useRecruitmentJourney, type LeaveApprovalJourney, type PersonnelEntryJourney, type RecruitmentJourney } from "./journeyQueries";
import { GuidanceProcessMap } from "./GuidanceProcessMap";
import { GuidanceReadingJourney } from "./GuidanceReadingJourney";
import { GuidancePracticeJourney } from "./GuidancePracticeJourney";
import { GuidanceOperationJourney } from "./GuidanceOperationJourney";

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
      <div><dt>Yanıtlanan karar konusu</dt><dd>{journey.answeredChecks}/6</dd></div>
      <div><dt>Son denemesi doğru olan konu</dt><dd>{journey.correctChecks}/6</dd></div>
      {journey.lastVerifiedAtUtc && <div><dt>Son gerçek işlem</dt><dd>{new Date(journey.lastVerifiedAtUtc).toLocaleDateString("tr-TR")}</dd></div>}
    </dl>
    <p>Bu kanıt yalnız çalışan kartı oluşturmayı gösterir. Evrak, ücret paketi ve uygulama dışındaki adımların tamamlandığı ya da tüm işe giriş sürecinde bağımsız yeterlilik kazanıldığı anlamına gelmez.</p>
    <p>Karar alıştırması sayıları gerçek işlem kanıtından ayrıdır; doğru yanıt tek başına bağımsız yeterlilik değildir.</p>
    <p>Rehber konumu form taslağı değildir; kaydedilmemiş alanları yeniden girmeniz gerekebilir.</p>
    <Link className="btn btn-primary" to={appPath("/personnel/new")}>Gerçek bir işe girişte rehberi aç</Link>
  </section>;
}

function RecruitmentEvidence({ journey }: { journey: RecruitmentJourney }) {
  return <section className="ez-panel ez-journey-card" aria-labelledby="journey-recruitment">
    <span className="ez-overline">İŞE ALIM · GERÇEK İŞLEM KANITI</span>
    <h2 id="journey-recruitment">Aday ilk incelemesi</h2>
    <dl>
      <div><dt>Doğrulanmış ilk aşama kararı</dt><dd>{journey.verifiedFirstReviews} aday</dd></div>
      <div><dt>Yanıtlanan karar konusu</dt><dd>{journey.answeredChecks}/2</dd></div>
      <div><dt>Son denemesi doğru olan konu</dt><dd>{journey.correctChecks}/2</dd></div>
      {journey.lastVerifiedAtUtc && <div><dt>Son gerçek karar</dt><dd>{new Date(journey.lastVerifiedAtUtc).toLocaleDateString("tr-TR")}</dd></div>}
    </dl>
    <p>Bu kayıt, “Yeni” aşamasındaki bir adayın durumunu gerçekten değiştirdiğinizi gösterir. Kararın doğruluğunu, adayla iletişimi veya işe alım sürecinde bağımsız yeterliliği kanıtlamaz.</p>
    <p>Karar alıştırmaları gerçek aşama değişikliği kanıtından ayrıdır; doğru yanıt yeterlilik onayı değildir.</p>
    <Link className="btn btn-secondary" to={appPath("/recruitment")}>Aday havuzunu aç</Link>
  </section>;
}

function LeaveApprovalEvidence({ journey }: { journey: LeaveApprovalJourney }) {
  return <section className="ez-panel ez-journey-card" aria-labelledby="journey-leave-approval">
    <span className="ez-overline">İZİN ONAYI · GERÇEK KARAR VE ALIŞTIRMA</span>
    <h2 id="journey-leave-approval">İzin kararlarını öğrenme</h2>
    <dl>
      <div><dt>Kaydedilmiş gerçek karar</dt><dd>{journey.verifiedDecisions} talep</dd></div>
      <div><dt>Yanıtlanan karar konusu</dt><dd>{journey.answeredChecks}/2</dd></div>
      <div><dt>Son denemesi doğru olan konu</dt><dd>{journey.correctChecks}/2</dd></div>
      {journey.lastVerifiedAtUtc && <div><dt>Son gerçek karar</dt><dd>{new Date(journey.lastVerifiedAtUtc).toLocaleDateString("tr-TR")}</dd></div>}
    </dl>
    <p>Gerçek karar sayısı onay ve ret kayıtlarından gelir; kararın doğruluğunu veya bağımsız yeterliliği kanıtlamaz. Alıştırma yanıtları gerçek işlemden ayrı sayılır.</p>
    <Link className="btn btn-secondary" to={appPath("/inbox?tab=approvals")}>Bekleyen izinleri aç</Link>
  </section>;
}

export function GuidanceJourneyPage() {
  const { user } = useAuth();
  const isHrAdmin = user?.role === "hr-admin";
  const [params] = useSearchParams();
  const history = params.get("tab") === "history";
  const query = usePersonnelEntryJourney(isHrAdmin);
  const recruitmentQ = useRecruitmentJourney(isHrAdmin);
  const leaveQ = useLeaveApprovalJourney();
  return <div className="ez-journey-page">
    <div className="page-header">
      <div><span className="ez-overline">İK REHBERİ</span><h1>İK Rehberi</h1>
        <p>İşinizi adım adım öğrenin, nedenini anlayın ve gerçek kayıtta uygulayın.</p></div>
    </div>
    <GuidanceNavigation history={history} />
    {history ? <div className="ez-learning-history"><div className="ez-section-heading"><h2>Öğrenme geçmişim</h2><p>İnceleme, alıştırma ve gerçek işlem kayıtları ayrı izlenir. Bu kayıtlar bağımsız yeterlilik onayı değildir.</p></div>
    {isHrAdmin && (query.isPending ? <PageLoading /> : query.isError
      ? <PageError error={query.error} />
      : <JourneyEvidence journey={query.data} />)}
    {isHrAdmin && (recruitmentQ.isPending
      ? <p role="status">Aday yolculuğu yükleniyor.</p>
      : recruitmentQ.isError
        ? <p role="alert">Aday yolculuğu yüklenemedi. Daha sonra yeniden deneyin.</p>
        : <RecruitmentEvidence journey={recruitmentQ.data} />)}
    {leaveQ.isPending
      ? <p role="status">İzin onayı yolculuğu yükleniyor.</p>
      : leaveQ.isError
        ? <p role="alert">İzin onayı yolculuğu yüklenemedi. Daha sonra yeniden deneyin.</p>
        : <LeaveApprovalEvidence journey={leaveQ.data} />}
    <GuidanceOperationJourney />
    <GuidanceReadingJourney />
    <GuidancePracticeJourney />
    <GuidanceCoverageJourney /></div> : <>
      <section className="ez-guide-start ez-panel">
        <div><span className="ez-overline">BİR SONRAKİ ADIMINIZ</span><h2>{isHrAdmin ? query.data?.hasSavedPosition ? "Kaldığınız yerden devam edin" : "İK işlerinize sağlam bir başlangıç yapın" : "Günlük işlerinizi rehberle yönetin"}</h2>
        <p>{isHrAdmin ? "İlgili işlem ekranında rehber size ne yapacağınızı, nedenini ve sonucu nasıl kontrol edeceğinizi gösterecek." : "Bekleyen işi açın; karar vermeden önce talebin koşullarını ve dayanağını kontrol edin."}</p>
        <Link className="btn btn-primary" to={appPath(isHrAdmin ? query.data?.hasSavedPosition ? "/personnel/new" : "/settings" : "/inbox?tab=daily")}>{isHrAdmin ? query.data?.hasSavedPosition ? "Çalışan rehberine devam et" : "Şirket hazırlığıyla başla" : "Günlük işlerimi aç"} <span aria-hidden="true">→</span></Link>
        </div><div className="ez-guide-start-path"><span>01 · Açıklamayı incele</span><span>02 · Gerçek kayıtta uygula</span><span>03 · Sonucu kontrol et</span></div>
      </section>
      {isHrAdmin && query.isError && <p role="alert">Kayıtlı konumunuz okunamadı. <button type="button" className="btn btn-secondary" onClick={() => void query.refetch()}>Konumu tekrar yükle</button></p>}
    <GuidanceProcessMap isHrAdmin={isHrAdmin} />
    <GuidanceHelpSettings />
    </>}
  </div>;
}
