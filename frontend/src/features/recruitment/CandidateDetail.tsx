import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { ApiError } from "../../api/client";
import { useToast } from "../../layout/ToastProvider";
import { appPath } from "../../paths";
import { PageError, PageLoading } from "../shared/PageState";
import { GuidanceDecisionCheck } from "../guidance/GuidanceDecisionCheck";
import { PublishedGuidancePractice } from "../guidance/PublishedGuidancePractice";
import { GuidanceOperationSource } from "../guidance/GuidanceOperationSource";
import { useDepartments } from "../personnel/queries";
import { NOTE_TYPES, PIPELINE_STATUSES, formatTimeAgo } from "./format";
import { CandidateCorrectionModal } from "./CandidateCorrectionModal";
import { CandidateEvidenceModal, type EvidenceSelection } from "./CandidateEvidenceModal";
import { useAddCandidateEvaluation, useAddInterviewNote, useCandidate, useHireCandidate, useRetryCandidateProvisioning, useSetCandidateStatus, type HireResultDto, type CandidateDetailDto, type CandidateDecisionEvidenceReference } from "./queries";

const STAGE_EXPLANATIONS: Record<string, string> = {
  "Yeni": "Başvuruyu yeniden ilk inceleme sırasına alır; önceki notlar ve aşama geçmişi kalır.",
  "Mülakat": "Adayı değerlendirme aşamasında gösterir; görüşme randevusu oluşturmaz.",
  "Teklif": "Adayı teklif aşamasında gösterir; teklif belgesi hazırlamaz veya adaya göndermez.",
  "Red": "Adayı reddedilmiş olarak işaretler ve bu durumdayken işe alma düğmesini kapatır; adaya mesaj göndermez.",
};

const STAGE_EXAMPLES: Record<string, string> = {
  "Yeni": "Örneğin reddedilmiş başvuru yeniden incelenecekse ilk sıraya alınabilir; önceki red geçmişte görünmeye devam eder.",
  "Mülakat": "Örneğin görüşme planlandıysa takvim davetinin gerçekten gönderildiğini ayrıca doğrulayın.",
  "Teklif": "Örneğin ücret koşulları konuşulduysa bile teklif belgesinin gönderimini ayrıca doğrulayın.",
  "Red": "Örneğin değerlendirme olumsuzsa adayın bilgilendirilmesini bu etiketin dışında takip edin.",
};

const TABS: [string, string][] = [
  ["cv", "Özgeçmiş"], ["notes", "Mülakat Notları"], ["eval", "Değerlendirme"], ["history", "Geçmiş"],
];

type InterviewNoteReview = {
  candidateId: number;
  candidateName: string;
  noteType: string;
  text: string;
};

type CandidateEvaluationReview = {
  candidateId: number;
  candidateName: string;
  criterion: string;
  score: number;
  maxScore: number;
};

const initialsOf = (name?: string | null): string =>
  (name ?? "")
    .split(" ")
    .filter(Boolean)
    .map((part) => part[0]?.toLocaleUpperCase("tr-TR"))
    .slice(0, 2)
    .join("") || "İK";

export function CandidateDetail({ id }: { id: number }) {
  const { showToast } = useToast();
  const detailQ = useCandidate(id);
  const setStatus = useSetCandidateStatus();
  const addNote = useAddInterviewNote();
  const addEvaluation = useAddCandidateEvaluation();
  const retryProvisioning = useRetryCandidateProvisioning();
  const [tab, setTab] = useState("cv");
  const [noteSource, setNoteSource] = useState<number | null>(null);
  const [evaluationSource, setEvaluationSource] = useState<number | null>(null);
  const [noteText, setNoteText] = useState("");
  const [noteType, setNoteType] = useState(NOTE_TYPES[0]);
  const [noteReview, setNoteReview] = useState<InterviewNoteReview | null>(null);
  const [noteError, setNoteError] = useState<string | null>(null);
  const [criterion, setCriterion] = useState("");
  const [evaluationScore, setEvaluationScore] = useState("");
  const [evaluationMaxScore, setEvaluationMaxScore] = useState("5");
  const [evaluationReview, setEvaluationReview] = useState<CandidateEvaluationReview | null>(null);
  const [evaluationError, setEvaluationError] = useState<string | null>(null);
  const [hireOpen, setHireOpen] = useState(false);
  const [correctionOpen, setCorrectionOpen] = useState(false);
  const [evidenceSelection, setEvidenceSelection] = useState<EvidenceSelection | null>(null);
  const [statusReview, setStatusReview] = useState<{ candidateId: number; from: string; to: string; source: CandidateDetailDto } | null>(null);
  const [statusConfirmed, setStatusConfirmed] = useState(false);
  const [statusReason, setStatusReason] = useState("");
  const [statusEvidence, setStatusEvidence] = useState<CandidateDecisionEvidenceReference[]>([]);
  const [guidanceVersion, setGuidanceVersion] = useState<number | null>(null);
  const [hireOutcome, setHireOutcome] = useState<HireResultDto | null>(null);
  const [provisioningEmail, setProvisioningEmail] = useState("");
  const [retrySource, setRetrySource] = useState<number | null>(null);
  const [retryReview, setRetryReview] = useState<{ email: string; source: number | null } | null>(null);

  useEffect(() => {
    setHireOutcome(null); setHireOpen(false); setProvisioningEmail(""); setRetrySource(null); setRetryReview(null);
    setCorrectionOpen(false);
    setEvidenceSelection(null);
    setStatusReview(null); setStatusConfirmed(false); setStatusReason(""); setStatusEvidence([]);
    setGuidanceVersion(null);
    setNoteSource(null); setEvaluationSource(null);
    setNoteReview(null);
    setNoteText("");
    setNoteError(null);
    setCriterion("");
    setEvaluationScore("");
    setEvaluationMaxScore("5");
    setEvaluationReview(null);
    setEvaluationError(null);
  }, [id]);

  useEffect(() => {
    const email = detailQ.data?.provisioning?.email;
    if (email) setProvisioningEmail(email);
  }, [detailQ.data?.provisioning?.email]);

  if (detailQ.isPending) return <PageLoading />;
  if (detailQ.isError) return <PageError error={detailQ.error} />;

  const candidate = detailQ.data;
  const currentHireOutcome = hireOutcome?.candidateId === id ? hireOutcome : null;
  const isHired = candidate.status === "İşe Alındı" || currentHireOutcome !== null;
  const provisioning = candidate.provisioning;

  const pendingStatus = statusReview?.candidateId === id && statusReview.from === (candidate.status ?? "Yeni") ? statusReview : null;
  const pendingNote = noteReview?.candidateId === id && noteReview.candidateName === candidate.name ? noteReview : null;
  const pendingEvaluation = evaluationReview?.candidateId === id && evaluationReview.candidateName === candidate.name ? evaluationReview : null;

  const prepareStatus = (status: string) => {
    setGuidanceVersion(null);
    setStatusConfirmed(false);
    setStatusReason(""); setStatusEvidence([]);
    setStatusReview(status === (candidate.status ?? "Yeni") ? null : {
      candidateId: id, from: candidate.status ?? "Yeni", to: status, source: candidate,
    });
  };

  const changeStatus = async () => {
    if (!pendingStatus || !statusConfirmed || setStatus.isPending
      || !statusReason.trim() || statusReason.trim().length > 2000 || statusEvidence.length > 20) return;
    try {
      const latest = await detailQ.refetch();
      if (latest.isError || !latest.data) throw new Error("Adayın güncel durumu okunamadı.");
      if (latest.data.status !== pendingStatus.from || latest.data.version !== pendingStatus.source.version) {
        setStatusReview(null);
        showToast("Adayın durumu bu sırada değişti veya yeni bilgi eklendi. Güncel kaydı inceleyip yeniden karar verin.", "warning");
        return;
      }
      await setStatus.mutateAsync({ id, status: pendingStatus.to, expectedVersion: pendingStatus.source.version,
        reason: statusReason.trim(), evidence: statusEvidence,
        ...(guidanceVersion !== null ? { guidanceContentRevisionId: guidanceVersion } : {}) });
      setStatusReview(null);
      setStatusConfirmed(false);
      showToast(candidate.name + " için pipeline durumu " + pendingStatus.to + " olarak kaydedildi.", "success");
    } catch (e) {
      showToast(e instanceof ApiError ? e.message : "Durum güncellenemedi; adayı yeniden kontrol edin.", "error");
    }
  };

  const prepareNote = () => {
    const text = noteText.trim();
    setNoteError(null);
    if (!text) {
      setNoteError("Önce not içeriğini yazın.");
      return;
    }
    if (text.length > 4000) {
      setNoteError("Mülakat notu en fazla 4000 karakter olabilir.");
      return;
    }
    setNoteReview({ candidateId: id, candidateName: candidate.name ?? "", noteType, text });
  };

  const submitNote = async () => {
    if (!pendingNote || addNote.isPending || isHired) return;
    setNoteError(null);
    try {
      await addNote.mutateAsync({ id: pendingNote.candidateId, noteType: pendingNote.noteType, text: pendingNote.text,
        ...(noteSource !== null ? { guidanceContentRevisionId: noteSource } : {}) });
      setNoteReview(null);
      setNoteText(""); setNoteSource(null);
      showToast("Mülakat notu eklendi.", "success");
    } catch (e) {
      setNoteError(e instanceof ApiError ? e.message : "Not eklenemedi.");
    }
  };

  const prepareEvaluation = () => {
    const cleanCriterion = criterion.trim();
    setEvaluationError(null);
    if (!cleanCriterion || cleanCriterion.length > 128) {
      setEvaluationError("Ölçüt adını 1-128 karakter arasında girin.");
      return;
    }
    if (!/^(?:[1-9]|10)$/.test(evaluationMaxScore)) {
      setEvaluationError("Azami puan 1-10 arasında tam sayı olmalıdır.");
      return;
    }
    const maxScore = Number(evaluationMaxScore);
    if (!/^(0|[1-9]|10)$/.test(evaluationScore) || Number(evaluationScore) > maxScore) {
      setEvaluationError("Verilen puan 0 ile azami puan arasında tam sayı olmalıdır.");
      return;
    }
    setEvaluationReview({
      candidateId: id, candidateName: candidate.name ?? "",
      criterion: cleanCriterion, score: Number(evaluationScore), maxScore,
    });
  };

  const submitEvaluation = async () => {
    if (!pendingEvaluation || addEvaluation.isPending || isHired) return;
    setEvaluationError(null);
    try {
      await addEvaluation.mutateAsync({
        id: pendingEvaluation.candidateId,
        criterion: pendingEvaluation.criterion,
        score: pendingEvaluation.score,
        maxScore: pendingEvaluation.maxScore,
        ...(evaluationSource !== null ? { guidanceContentRevisionId: evaluationSource } : {}),
      });
      setEvaluationReview(null);
      setCriterion("");
      setEvaluationScore(""); setEvaluationSource(null);
      showToast("Aday değerlendirmesi kaydedildi.", "success");
    } catch (e) {
      setEvaluationError(e instanceof ApiError ? e.message : "Değerlendirme kaydedilemedi.");
    }
  };

  const retryLoginProvisioning = async () => {
    if (!retryReview || retryProvisioning.isPending) return;
    try {
      const result = await retryProvisioning.mutateAsync({
        id,
        email: retryReview.email || null,
        ...(retryReview.source !== null ? { guidanceContentRevisionId: retryReview.source } : {}),
      });
      showToast(
        result.status === "completed"
          ? "Personel giriş hesabı hazır; davetin teslimini ayrıca kontrol edin."
          : "Provizyon yeniden kuyruğa alındı; otomatik deneme devam edecek.",
        result.status === "completed" ? "success" : "warning",
      );
      setRetryReview(null); setRetrySource(null);
    } catch (e) {
      showToast(e instanceof ApiError ? e.message : "Provizyon yeniden denenemedi.", "error");
    }
  };

  return (
    <>
      <div className="detail-header">
        <div className="dh-profile">
          <div className="dh-avatar-lg">{candidate.initials}</div>
          <div>
            <h2>{candidate.name}</h2>
            <Link to={appPath("/guidance/catalog?process=recruitment")}>İşe alımın kaynaklı rehberini aç</Link>
            <p className="dh-role">{candidate.appliedRole}</p>
            {candidate.positionTitle && <p className="dh-role">Bağlı pozisyon: {candidate.positionTitle}</p>}
            <div className="dh-tags">
              <span className="tag-pill"><i aria-hidden="true" className="fa-solid fa-location-dot" /> {candidate.location || "Belirtilmedi"}</span>
              <span className="tag-pill"><i aria-hidden="true" className="fa-solid fa-briefcase" /> {candidate.experienceYears ?? 0} yıl deneyim</span>
            </div>
          </div>
        </div>
        <div className="dh-actions">
          <button className="btn btn-secondary" onClick={() => setCorrectionOpen(true)} disabled={setStatus.isPending}>
            {isHired ? "Düzeltme geçmişi" : "Aday kaydını düzelt"}
          </button>
          <div className="match-score"><span className="score-circle">{candidate.score ?? "—"}</span><span className="score-label">{candidate.score == null ? "Genel puan henüz verilmedi" : "Elle verilen aday puanı"}</span></div>
          <label className="sr-only" htmlFor="candidate-status">Pipeline durumu</label>
          <select
            id="candidate-status"
            className="input-control"
            value={isHired ? "" : (pendingStatus?.to ?? candidate.status ?? "Yeni")}
            disabled={isHired || setStatus.isPending}
            onChange={(e) => prepareStatus(e.target.value)}
          >
            {isHired && <option value="">İşe Alındı</option>}
            {PIPELINE_STATUSES.map((status) => (
              <option key={status} value={status}>{status}</option>
            ))}
          </select>
          <button className="btn btn-primary" disabled={isHired || candidate.status === "Red" || pendingStatus !== null} onClick={() => setHireOpen(true)}>
            <i aria-hidden="true" className="fa-solid fa-thumbs-up" /> İşe Al
          </button>
        </div>
      </div>

      {pendingStatus && <section className="ez-candidate-status-review ez-workbench" role="region" aria-label="Aday aşaması ön kontrolü">
          <div className="ez-workbench-layout">
<div className="ez-workbench-guide" role="group" aria-label="İşlem açıklamaları" tabIndex={0}><GuidanceOperationSource label="Aşama kararında kullandığım rehber sürümü" process="recruitment" step="first-review"
          value={guidanceVersion} onChange={value => { setGuidanceVersion(value); setStatusConfirmed(false); }} disabled={setStatus.isPending} /></div>
          <div className="ez-workbench-main">

        <h3>Aşama değişikliğinden önce</h3>
        <p><strong>Gerçek kayıt:</strong> {candidate.name} · {pendingStatus.from} → {pendingStatus.to}</p>
        <p><strong>Ne olacak?</strong> {STAGE_EXPLANATIONS[pendingStatus.to]}</p>
        <p><strong>Örnek:</strong> {STAGE_EXAMPLES[pendingStatus.to]}</p>
        <p><strong>Neden kontrol ediyoruz?</strong> Mülakat notları, değerlendirme ve başvuru belgeleriyle bu aşamanın gerekçesini karşılaştırın. Elle verilen aday puanı tek başına karar kanıtı değildir.</p>
        <p>Yanlış aşamayı kaydederseniz işe alınmamış adayın durumunu yeniden değiştirebilirsiniz; önceki hareket geçmişte kalır. İşe alınmış adayın aşaması bu alandan değiştirilemez.</p>
        <p>Gerekçeyi kaydediyoruz. Seçtiğiniz dayanakların karar anındaki sürümü ve içeriği korunur; sonradan düzeltme yapılması eski kararın dayanağını değiştirmez.</p>
        <p>Onaydan önce aday sürümü yeniden okunur ve sunucu kaydederken aynı sürümü doğrular; yeni bilgi veya düzeltme araya girerse kararınızı yeniden değerlendirin.</p>
        <div className="input-group"><label className="input-label" htmlFor="candidate-stage-reason">Aşama kararının gerekçesi ve dayanağı</label>
          <textarea id="candidate-stage-reason" className="input-control" value={statusReason}
            onChange={e => { setStatusReason(e.target.value); setStatusConfirmed(false); }} maxLength={2000} />
        </div>
        <fieldset><legend>Karara bağlanacak mevcut dayanaklar (isteğe bağlı)</legend>
          {(pendingStatus.source.notes ?? []).map(note => <label key={`note-${note.id}`}>
            <input type="checkbox" checked={statusEvidence.some(item => item.kind === "note" && item.id === note.id)}
              onChange={e => { setStatusConfirmed(false); setStatusEvidence(previous => e.target.checked
                ? [...previous, { kind: "note", id: note.id!, version: note.version }]
                : previous.filter(item => item.kind !== "note" || item.id !== note.id)); }} />
            Not #{note.id} · sürüm {note.version} · {note.noteType}: {note.text}
          </label>)}
          {(pendingStatus.source.evaluations ?? []).map(evaluation => <label key={`evaluation-${evaluation.id}`}>
            <input type="checkbox" checked={statusEvidence.some(item => item.kind === "evaluation" && item.id === evaluation.id)}
              onChange={e => { setStatusConfirmed(false); setStatusEvidence(previous => e.target.checked
                ? [...previous, { kind: "evaluation", id: evaluation.id!, version: evaluation.version }]
                : previous.filter(item => item.kind !== "evaluation" || item.id !== evaluation.id)); }} />
            Değerlendirme #{evaluation.id} · sürüm {evaluation.version} · {evaluation.criterion}: {evaluation.score}/{evaluation.maxScore}
          </label>)}
          {(pendingStatus.source.notes?.length ?? 0) + (pendingStatus.source.evaluations?.length ?? 0) === 0
            && <p>Kayıtlı not veya değerlendirme yok; kararınızın bilgi kaynağını gerekçede açıklayın.</p>}
          <p>Yalnız bu kararda kullandığınız dayanakları seçin; en fazla 20 kayıt bağlanabilir. Seçim kanıtın doğruluğunu otomatik onaylamaz.</p>
        </fieldset>
        <GuidanceDecisionCheck process="recruitment" step="first-review" operationName="aday aşama kararı" />

        <label><input type="checkbox" checked={statusConfirmed} onChange={(e) => setStatusConfirmed(e.target.checked)} /> Aday kaydını ve aşama gerekçesini kontrol ettim.</label>
        <div className="ez-candidate-status-actions">
          <button className="btn btn-ghost" onClick={() => { setStatusReview(null); setStatusConfirmed(false); }}>Vazgeç</button>
          <button className="btn btn-primary" onClick={changeStatus} disabled={!statusConfirmed || setStatus.isPending
            || !statusReason.trim() || statusReason.trim().length > 2000 || statusEvidence.length > 20
            || !Number.isInteger(pendingStatus.source.version) || pendingStatus.source.version < 1}>Aşamayı kaydet</button>
        </div>
      </div></div></section>}

      {candidate.status === "Red" && <p role="status" className="ez-hire-status-note">
        Reddedilmiş aday işe alınamaz. Karar değiştiyse önce pipeline durumunu güncelleyin.
      </p>}
      {currentHireOutcome && <section className="ez-hire-outcome" aria-labelledby="hire-outcome-title" role="status">
        <h3 id="hire-outcome-title">Personel kaydı oluşturuldu</h3>
        <p>{currentHireOutcome.employeeName || candidate.name} için çalışan kaydı açıldı. Adayı işe almak, özlük belgelerini, ücret paketini veya uygulama dışındaki bildirimleri tamamlamaz.</p>
        <p>{currentHireOutcome.provisioning?.status === "completed"
          ? "Giriş hesabı hazır. Davetin teslimini ayrıca kontrol edin."
          : currentHireOutcome.provisioning?.status
            ? "Giriş hesabı hazırlanıyor; durumunu bu aday ekranından takip edin."
            : "Giriş hesabının durumu yanıtta doğrulanamadı; aday kaydını yenileyip kontrol edin."}</p>
        {currentHireOutcome.employeeId
          ? <Link className="btn btn-secondary" to={appPath("/personnel/" + currentHireOutcome.employeeId + "?edit=1&guide=1&tab=tab-evrak")} state={{ guideCreated: true }}>Personel kartını ve kalan adımları aç</Link>
          : <Link className="btn btn-secondary" to={appPath("/personnel")}>Çalışan listesini kontrol et</Link>}
      </section>}

      {isHired && !currentHireOutcome && <section className="ez-hire-outcome" aria-labelledby="candidate-employee-link-title">
        <h3 id="candidate-employee-link-title">Adaydan oluşan çalışan kaydı</h3>
        {candidate.linkedEmployee ? <>
          <p>{candidate.linkedEmployee.name} · {candidate.linkedEmployee.isActive ? "Aktif çalışan" : "Aktif olmayan çalışan"}</p>
          <p>Başvuru geçmişi bu aday kaydında kalır. Çalışan bilgileri ve belgeler personel kartında yönetilir; işe alınmış başvurunun bilgileri bu ekrandan değiştirilemez.</p>
          <Link className="btn btn-secondary" to={appPath("/personnel/" + candidate.linkedEmployee.id)}>Bağlı personel kartını aç</Link>
          {candidate.linkedEmployee.isActive
            ? <Link className="btn btn-secondary" to={appPath("/personnel/" + candidate.linkedEmployee.id + "?edit=1&guide=1&tab=tab-evrak")}>İşe giriş rehberine devam et</Link>
            : <p>Çalışan şu anda aktif değil. Bu bağlantı işe dönüş işlemi yapmaz; güncel istihdam durumunu personel kartında kontrol edin.</p>}
          <p>Çalışan bağlantısı, giriş hesabının hazır veya davetin teslim edilmiş olduğunu göstermez. Evrak, ücret paketi ve uygulama dışı adımları ayrıca kontrol edin.</p>
        </> : <>
          <p>İlgili çalışan kaydı güvenilir biçimde doğrulanamadı. Personel listesini ve işe alma geçmişini kontrol edin; aynı kişiye yeniden kayıt açmayın.</p>
          <Link className="btn btn-secondary" to={appPath("/personnel")}>Personel listesini kontrol et</Link>
        </>}
      </section>}

      {isHired && provisioning && provisioning.status !== "completed" && (
        <div
          className={[`provisioning-banner is-${provisioning.status ?? "pending"}`, "ez-workbench"].filter(Boolean).join(" ")}
          role={provisioning.status === "failed" ? "alert" : "status"}
        >
          <div className="ez-workbench-layout">
<div className="ez-workbench-guide" role="group" aria-label="İşlem açıklamaları" tabIndex={0}><GuidanceOperationSource label="Hesap yeniden denemesinde kullandığım rehber sürümü" process="recruitment" step="hire-preparation"
            value={retrySource} onChange={value => { setRetrySource(value); setRetryReview(null); }} disabled={retryProvisioning.isPending} /></div>
          <div className="ez-workbench-main">

          <div>
            <strong>
              {provisioning.status === "failed"
                ? "Giriş hesabı müdahale bekliyor"
                : "Giriş hesabı hazırlanıyor"}
            </strong>
            <p>{provisioning.lastError || "Otomatik provizyon denemeleri devam ediyor."}</p>
          </div>
          <label className="sr-only" htmlFor="provisioning-email">Provizyon e-postası</label>
          <input
            id="provisioning-email"
            type="email"
            className="input-control"
            value={provisioningEmail}
            onChange={(event) => { setProvisioningEmail(event.target.value); setRetryReview(null); }}
          />
          <PublishedGuidancePractice process="recruitment" step="hire-preparation" />

          <button className="btn btn-primary btn-sm" onClick={() => setRetryReview({ email: provisioningEmail.trim() || provisioning.email || "", source: retrySource })} disabled={retryProvisioning.isPending}>
            Şimdi yeniden dene
          </button>
          {retryReview && <section aria-label="Hesap yeniden deneme ön kontrolü">
            <p>{candidate.name} · {retryReview.email}. Var olan hesap hazırlığı yeniden istenir; yeni çalışan oluşturulmaz. Başka bir deneme çalışıyorsa sunucu işlemi durdurur.</p>
            <p>İstek kaydı teslim kanıtı değildir. Sonuçta hesap durumunu ve davetin gerçek teslimini ayrıca kontrol edin.</p>
            <button className="btn btn-ghost" onClick={() => setRetryReview(null)} disabled={retryProvisioning.isPending}>Yeniden denemeden vazgeç</button>
            <button className="btn btn-primary" onClick={retryLoginProvisioning} disabled={retryProvisioning.isPending}>Kontrol ettim; yeniden denemeyi iste</button>
          </section>}
        </div></div></div>
      )}

      <div className="detail-tabs">
        {TABS.map(([key, label]) => (
          <button key={key} className={`tab-link ${tab === key ? "active" : ""}`} onClick={() => setTab(key)}>
            {label}
          </button>
        ))}
      </div>

      <div className="detail-content-wrapper">
        {tab === "cv" && (
          <div id="tab-cv" className="tab-content active">
            <div className="content-block">
              <h4><i aria-hidden="true" className="fa-regular fa-file-lines" /> Başvuru Özeti</h4>
              <p className="summary-text">{candidate.summary || "Başvuru özeti girilmedi."}</p>
            </div>
            <div className="content-block">
              <h4><i aria-hidden="true" className="fa-solid fa-wand-magic-sparkles" /> Yetenek Seti</h4>
              <div className="skills-wrap">
                {(candidate.skills ?? []).map((skill) => (
                  <span key={skill.id} className="skill-tag">{skill.name}</span>
                ))}
                {(candidate.skills ?? []).length === 0 && <span className="text-muted">Yetenek girilmedi.</span>}
              </div>
            </div>
            <div className="content-block">
              <h4><i aria-hidden="true" className="fa-solid fa-history" /> İş Deneyimi</h4>
              <div className="timeline">
                {(candidate.experiences ?? []).map((experience) => (
                  <div key={experience.id} className="tl-item">
                    <div className="tl-dot" />
                    <div className="tl-content">
                      <strong>{experience.title}</strong>
                      <span>{experience.company} • {experience.period || "-"}</span>
                      <p>{experience.description}</p>
                    </div>
                  </div>
                ))}
                {(candidate.experiences ?? []).length === 0 && <p className="text-muted">Deneyim girilmedi.</p>}
              </div>
            </div>
          </div>
        )}

        {tab === "notes" && (
          <div id="tab-notes" className="tab-content active">
            <div className="notes-container ez-workbench">
          <div className="ez-workbench-layout">
<div className="ez-workbench-guide" role="group" aria-label="İşlem açıklamaları" tabIndex={0}>{!isHired && <GuidanceOperationSource process="recruitment" step="first-review" value={noteSource} onChange={setNoteSource} disabled={addNote.isPending} label="Not eklerken kullandığım rehber sürümü" />}</div>
          <div className="ez-workbench-main">

              <section className="ez-interview-note-guide" aria-label="Mülakat notu rehberi">
                <p><strong>Ne yapıyoruz?</strong> Görüşmede gözlediğiniz iş ile ilgili bilgiyi aday kaydına ekliyoruz. Bu işlem değerlendirme puanı veya pipeline aşamasını değiştirmez.</p>
                <p><strong>Neden kontrol ediyoruz?</strong> Notu sonraki değerlendirmede aynı adaya ve doğru görüşme türüne bağlayabilmek için. Örneğin teknik görüşme bulgusunu İK görüşmesi diye etiketlemeyin; gözlem ve karar yorumunu açıkça ayırın.</p>
                <p>İlk kayıt ve yazarı korunur. Hatalı not yeni sürüm ve gerekçeyle düzeltilebilir; güncel sürüm bu listede görünür. İşe alınmış adayın geçmişi kilitlidir; ekleme veya düzeltme yapılamaz.</p>
              </section>

              <div className="add-note-box">
                <label className="sr-only" htmlFor="interview-note">Mülakat notu</label>
                <textarea
                  id="interview-note"
                  placeholder="Mülakat notunuzu buraya girin"
                  value={noteText}
                  onChange={(e) => { setNoteText(e.target.value); setNoteReview(null); setNoteError(null); }}
                />
                <div className="note-actions">
                  <label className="sr-only" htmlFor="interview-note-type">Not türü</label>
                  <select id="interview-note-type" value={noteType} onChange={(e) => { setNoteType(e.target.value); setNoteReview(null); setNoteError(null); }}>
                    {NOTE_TYPES.map((type) => <option key={type}>{type}</option>)}
                  </select>
                  {pendingNote && <button className="btn btn-ghost btn-sm" onClick={() => setNoteReview(null)}>Notu düzelt</button>}
                  <button className="btn btn-primary btn-sm" onClick={pendingNote ? submitNote : prepareNote} disabled={addNote.isPending || isHired}>
                    {pendingNote ? "Notu kaydet" : "Notu kontrol et"}
                  </button>
                </div>
              </div>
              {noteError && <p className="form-error" role="alert">{noteError}</p>}
              {pendingNote && <section className="ez-interview-note-review" role="region" aria-label="Mülakat notu ön kontrolü" aria-live="polite">
                <h4>Notu kaydetmeden önce</h4>
                <p><strong>Aday:</strong> {pendingNote.candidateName} · <strong>Görüşme türü:</strong> {pendingNote.noteType}</p>
                <p><strong>Kaydedilecek metin:</strong> {pendingNote.text}</p>
                <p>Bu not adayın Mülakat Notları sekmesinde görünecek ve ekleme olayı geçmişe yazılacak. Adaya mesaj gönderilmeyecek.</p>
              </section>}
              {(candidate.notes ?? []).map((note) => (
                <div key={note.id} className="note-item">
                  <div className="note-avatar" aria-hidden="true">{initialsOf(note.authorName)}</div>
                  <div className="note-body">
                    <div className="note-header">
                      <strong>İlk kayıt: {note.authorName} · güncel tür: {note.noteType}</strong> <span>{formatTimeAgo(note.createdAtUtc)}</span>
                    </div>
                    <p>{note.text}</p>
                    <p>Güncel sürüm {note.version ?? 1}</p>
                    <button className="btn btn-ghost btn-sm" onClick={() => setEvidenceSelection({ kind: "notes", record: note })}>
                      {isHired ? "Notun sürüm geçmişi" : "Notu düzelt ve geçmişini gör"}
                    </button>
                  </div>
                </div>
              ))}
              {(candidate.notes ?? []).length === 0 && <p className="pending-desc">Henüz mülakat notu yok.</p>}
            </div></div></div>
          </div>
        )}

        {tab === "eval" && (
          <div id="tab-eval" className="tab-content active ez-workbench">
          <div className="ez-workbench-layout">
<div className="ez-workbench-guide" role="group" aria-label="İşlem açıklamaları" tabIndex={0}>{!isHired && <GuidanceOperationSource process="recruitment" step="first-review" value={evaluationSource} onChange={setEvaluationSource} disabled={addEvaluation.isPending} label="Değerlendirme eklerken kullandığım rehber sürümü" />}</div>
          <div className="ez-workbench-main">

            <section className="ez-candidate-evaluation-guide" aria-label="Aday değerlendirme rehberi">
              <p><strong>Ne yapıyoruz?</strong> Adayı belirli bir iş ölçütü için puanlıyoruz. Örneğin teknik görüşmede TypeScript bilgisi için 4/5 kaydı açabilirsiniz.</p>
              <p><strong>Neden ölçüt yazıyoruz?</strong> Sayının neyi ölçtüğü ve azami puanı açık olursa sonraki incelemede gerekçe anlaşılır. Bu kayıt adayın genel puanını veya pipeline aşamasını otomatik değiştirmez.</p>
              <p>Hatalı değerlendirme gerekçeli yeni sürümle düzeltilir; ilk puan ve önceki sürümler korunur. İşe alınmış adayın geçmişi kilitlidir; ekleme veya düzeltme yapılamaz. Gözlemin dayanağını mülakat notunda belirtin.</p>
            </section>

            <div className="ez-candidate-evaluation-form">
              <div className="input-group">
                <label className="input-label" htmlFor="candidate-evaluation-criterion">Değerlendirme ölçütü</label>
                <input id="candidate-evaluation-criterion" className="input-control" value={criterion}
                  onChange={(e) => { setCriterion(e.target.value); setEvaluationReview(null); setEvaluationError(null); }} />
              </div>
              <div className="input-group">
                <label className="input-label" htmlFor="candidate-evaluation-score">Verilen puan</label>
                <input id="candidate-evaluation-score" className="input-control" type="number" min="0" max="10" step="1" value={evaluationScore}
                  onChange={(e) => { setEvaluationScore(e.target.value); setEvaluationReview(null); setEvaluationError(null); }} />
              </div>
              <div className="input-group">
                <label className="input-label" htmlFor="candidate-evaluation-max">Azami puan</label>
                <input id="candidate-evaluation-max" className="input-control" type="number" min="1" max="10" step="1" value={evaluationMaxScore}
                  onChange={(e) => { setEvaluationMaxScore(e.target.value); setEvaluationReview(null); setEvaluationError(null); }} />
              </div>
              <div className="ez-candidate-evaluation-actions">
                {pendingEvaluation && <button className="btn btn-ghost" onClick={() => setEvaluationReview(null)}>Bilgileri düzelt</button>}
                <button className="btn btn-primary" onClick={pendingEvaluation ? submitEvaluation : prepareEvaluation} disabled={addEvaluation.isPending || isHired}>
                  {pendingEvaluation ? "Değerlendirmeyi kaydet" : "Bilgileri kontrol et"}
                </button>
              </div>
            </div>
            {evaluationError && <p className="form-error" role="alert">{evaluationError}</p>}
            {pendingEvaluation && <section className="ez-candidate-evaluation-review" role="region" aria-label="Aday değerlendirmesi ön kontrolü" aria-live="polite">
              <h4>Değerlendirmeyi kaydetmeden önce</h4>
              <p><strong>Aday:</strong> {pendingEvaluation.candidateName} · <strong>Ölçüt:</strong> {pendingEvaluation.criterion}</p>
              <p><strong>Puan:</strong> {pendingEvaluation.score}/{pendingEvaluation.maxScore}</p>
              <p>Bu ölçüt adayın değerlendirmelerine eklenecek. Aşama değişmeyecek ve adaya mesaj gönderilmeyecek.</p>
            </section>}
            <div className="eval-grid">
              {(candidate.evaluations ?? []).map((evaluation) => (
                <div key={evaluation.id} className="eval-card">
                  <div className="eval-header">
                    <span>{evaluation.criterion}</span>
                    <strong>{Number(evaluation.score).toFixed(1)}/{evaluation.maxScore}</strong>
                  </div>
                  <div className="progress-bg">
                    <div
                      className="progress-fill"
                      style={{ width: `${Math.round(((evaluation.score ?? 0) / (evaluation.maxScore || 1)) * 100)}%` }}
                    />
                  </div>
                  <p>Güncel sürüm {evaluation.version ?? 1}</p>
                  <button className="btn btn-ghost btn-sm" onClick={() => setEvidenceSelection({ kind: "evaluations", record: evaluation })}>
                    {isHired ? "Değerlendirmenin sürüm geçmişi" : "Değerlendirmeyi düzelt ve geçmişini gör"}
                  </button>
                </div>
              ))}
              {(candidate.evaluations ?? []).length === 0 && <p className="pending-desc">Henüz değerlendirme yok.</p>}
            </div>
          </div></div></div>
        )}

        {tab === "history" && (
          <div id="tab-history" className="tab-content active">
            <section aria-label="Gerekçeli aşama kararları"><h4>Gerekçeli aşama kararları</h4>
              <p>Önceki aşama kararları karar anındaki dayanakla saklanır. Bu özellikten önceki hareketlerde gerekçe veya dayanak bilgisi bulunmayabilir.</p>
              {(candidate.stageDecisions ?? []).map(decision => <details key={decision.id}>
                <summary>{decision.fromStatus} → {decision.toStatus} · {decision.reason}</summary>
                <p>{decision.decidedByName} · {decision.decidedAtUtc} · aday sürümü {decision.fromVersion} → {decision.toVersion}</p>
                {(decision.evidence ?? []).length === 0 && <p>Bu karara kayıtlı not/değerlendirme bağlanmadı.</p>}
                {(decision.evidence ?? []).map(item => <div key={`${item.kind}-${item.id}`}>
                  <strong>{item.kind === "note" ? "Not" : "Değerlendirme"} #{item.id} · karar anındaki sürüm {item.version}</strong>
                  <p>{!item.state ? "Karar anındaki dayanak ayrıntısı bulunamadı." : item.kind === "note" ? `${item.state.noteType}: ${item.state.text}`
                    : `${item.state?.criterion}: ${item.state?.score}/${item.state?.maxScore}`}</p>
                </div>)}
              </details>)}
            </section>
            <div className="history-list">
              {(candidate.history ?? []).map((entry) => (
                <div key={entry.id} className="hist-item">
                  <i aria-hidden="true" className="fa-solid fa-envelope bg-blue" />
                  <div>
                    <strong>{entry.event}</strong>
                    <small>{formatTimeAgo(entry.occurredAtUtc)}</small>
                  </div>
                </div>
              ))}
            </div>
          </div>
        )}
      </div>

      {correctionOpen && <CandidateCorrectionModal key={id} candidate={{ ...candidate, status: isHired ? "İşe Alındı" : candidate.status }} onClose={() => setCorrectionOpen(false)} />}
      {evidenceSelection && <CandidateEvidenceModal key={`${id}-${evidenceSelection.kind}-${evidenceSelection.record.id}`}
        candidate={{ ...candidate, status: isHired ? "İşe Alındı" : candidate.status }} selection={evidenceSelection} onClose={() => setEvidenceSelection(null)} />}
      {hireOpen && (
        <HireModal
          candidateId={id}
          candidateVersion={candidate.version}
          candidateName={candidate.name ?? ""}
          appliedRole={candidate.appliedRole ?? ""}
          onClose={() => setHireOpen(false)}
          onHired={setHireOutcome}
        />
      )}
    </>
  );
}

type HireReview = {
  departmentId: number;
  departmentName: string;
  email: string;
  title: string;
  hireDate: string;
  contractType: "indefinite" | "fixed-term";
};

function HireModal({ candidateId, candidateVersion, candidateName, appliedRole, onClose, onHired }: {
  candidateId: number; candidateVersion: number; candidateName: string; appliedRole: string;
  onClose: () => void; onHired: (result: HireResultDto) => void;
}) {
  const { showToast } = useToast();
  const hire = useHireCandidate();
  const [initialVersion] = useState(candidateVersion);
  const [hireSource, setHireSource] = useState<number | null>(null);
  const departmentsQ = useDepartments();
  const [departmentId, setDepartmentId] = useState("");
  const [email, setEmail] = useState("");
  const [title, setTitle] = useState(appliedRole);
  const [hireDate, setHireDate] = useState("");
  const [contractType, setContractType] = useState("");
  const [review, setReview] = useState<HireReview | null>(null);
  const [error, setError] = useState<string | null>(null);

  const prepareReview = () => {
    setError(null);
    const department = departmentsQ.data?.find((item) => String(item.id) === departmentId);
    if (!department?.id) {
      setError("Departman listesini doğrulayıp bir departman seçin.");
      return;
    }
    if (!email.trim() || !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email.trim())) {
      setError("Geçerli bir iş e-postası girin; giriş hesabı bu adrese bağlanır.");
      return;
    }
    if (!hireDate) {
      setError("İşe giriş tarihini onaylı iş bilgisiyle doğrulayıp seçin.");
      return;
    }
    if (contractType !== "indefinite" && contractType !== "fixed-term") {
      setError("İmzalı sözleşmedeki türü doğrulayıp seçin.");
      return;
    }
    setReview({
      departmentId: department.id,
      departmentName: department.name ?? "Departman",
      email: email.trim(),
      title: title.trim() || appliedRole,
      hireDate,
      contractType,
    });
  };

  const submit = async () => {
    if (!review || hire.isPending) return;
    setError(null);
    try {
      const result = await hire.mutateAsync({
        id: candidateId,
        departmentId: review.departmentId,
        email: review.email,
        title: review.title,
        hireDate: review.hireDate,
        contractType: review.contractType,
        expectedCandidateVersion: initialVersion,
        ...(hireSource !== null ? { guidanceContentRevisionId: hireSource } : {}),
      });
      onHired(result);
      showToast(result.provisioning?.status === "completed"
        ? "Personel kaydı oluşturuldu; giriş hesabı hazır."
        : "Personel kaydı oluşturuldu; giriş hesabının durumunu ayrıca kontrol edin.",
      result.provisioning?.status === "completed" ? "success" : "warning");
      onClose();
    } catch (e) {
      setError(e instanceof ApiError ? e.message : "Aday işe alınamadı.");
    }
  };
  return (
    <div className="modal-overlay" style={{ display: "flex" }}>
      <div className="modal-card scale-in">
        <div className="modal-head">
          <div>
            <h3>İşe Al: {candidateName}</h3>
            <p>İşlem çalışan kaydı açar; bağlı pozisyon varsa kontenjanı günceller. Giriş hesabı ayrı hazırlanabilir.</p>
          </div>
          <button className="btn-icon-sm" onClick={onClose} disabled={hire.isPending} title="Kapat" aria-label="İşe alım penceresini kapat">
            <i aria-hidden="true" className="fa-solid fa-xmark" />
          </button>
        </div>
        <div className="modal-body-scroll ez-workbench">
          <div className="ez-workbench-layout">
<div className="ez-workbench-guide" role="group" aria-label="İşlem açıklamaları" tabIndex={0}><GuidanceOperationSource label="İşe alırken kullandığım rehber sürümü" process="recruitment" step="hire-preparation"
            value={hireSource} onChange={value => { setHireSource(value); setReview(null); }} disabled={hire.isPending} /></div>
          <div className="ez-workbench-main">

          {error && <p className="form-error" role="alert">{error}</p>}
          <section className="ez-hire-guide" aria-label="İşe alma rehberi">
            <strong>Bu adımda ne yapıyoruz?</strong>
            <p>Adayı çalışan kaydına dönüştürüyoruz. Adı, departmanı, iş e-postasını ve başlangıç tarihini onaylı bilgilerle karşılaştırın.</p>
            <p><strong>Neden?</strong> Hatalı tarih ve departman personel, izin ve bordro hazırlığını etkileyebilir. Örneğin sözleşmedeki 1 Ekim başlangıcını bugünün tarihiyle değiştirmeyin.</p>
            <p>İmzalı belgedeki sözleşme türünü seçin; seçiminiz çalışan kaydına ve ilk istihdam dönemine birlikte yazılır. Örneğin belirli süreli sözleşmeyi belirsiz süreli seçmeyin. İşe alma sonrası özlük, ücret ve dış bildirim adımları ayrıca kontrol edilir.</p>
          </section>
          <PublishedGuidancePractice process="recruitment" step="hire-preparation" />

          {departmentsQ.isPending && <p role="status">Departmanlar yükleniyor…</p>}
          {departmentsQ.isError && <p className="form-error" role="alert">Departman listesi okunamadı; işe alma ön kontrolü yapılamaz.</p>}
          <div className="form-grid-2">
            <div className="input-group">
              <label className="input-label" htmlFor="hire-department">Departman</label>
              <select
                id="hire-department"
                className="input-control"
                value={departmentId}
                onChange={(e) => { setReview(null); setDepartmentId(e.target.value); }}
              >
                <option value="">Seçin</option>
                {(departmentsQ.data ?? []).map((department) => (
                  <option key={department.id} value={department.id}>{department.name}</option>
                ))}
              </select>
            </div>
            <div className="input-group">
              <label className="input-label" htmlFor="hire-email">İş e-postası</label>
              <input id="hire-email" type="email" className="input-control" value={email} onChange={(e) => { setReview(null); setEmail(e.target.value); }} />
            </div>
            <div className="input-group">
              <label className="input-label" htmlFor="hire-title">Ünvan</label>
              <input id="hire-title" className="input-control" value={title} onChange={(e) => { setReview(null); setTitle(e.target.value); }} />
            </div>
            <div className="input-group">
              <label className="input-label" htmlFor="hire-date">İşe giriş tarihi</label>
              <input id="hire-date" type="date" className="input-control" value={hireDate} onChange={(e) => { setReview(null); setHireDate(e.target.value); }} />
            </div>
            <div className="input-group">
              <label className="input-label" htmlFor="hire-contract">İmzalı sözleşmedeki tür</label>
              <select id="hire-contract" className="input-control" value={contractType}
                onChange={(e) => { setReview(null); setContractType(e.target.value); }}>
                <option value="">Belgeden doğrulayıp seçin</option>
                <option value="indefinite">Belirsiz süreli</option>
                <option value="fixed-term">Belirli süreli</option>
              </select>
            </div>
          </div>
          {review && <section className="ez-hire-review" aria-labelledby="hire-review-title" aria-live="polite">
            <h4 id="hire-review-title">İşe almadan önce son kontrol</h4>
            <p><strong>Aday:</strong> {candidateName}</p>
            <p><strong>Departman:</strong> {review.departmentName} · <strong>Ünvan:</strong> {review.title}</p>
            <p><strong>İşe giriş:</strong> {review.hireDate} · <strong>İş e-postası:</strong> {review.email}</p>
            <p><strong>Sözleşme:</strong> {review.contractType === "fixed-term" ? "Belirli süreli" : "Belirsiz süreli"} olarak kaydedilecek. Bu tür imzalı belgeyle uyuşmuyorsa devam etmeyin.</p>
            <p>Onaydan sonra çalışan kaydı ve varsa pozisyon kontenjanı değişir. Giriş hesabı hazır olmasa bile çalışan kaydı oluşmuş olabilir; aynı işlemi tekrar göndermeden önce sonucu kontrol edin.</p>
          </section>}
        </div></div></div>
        <div className="modal-footer">
          {review
            ? <>
              <button className="btn btn-ghost" onClick={() => setReview(null)} disabled={hire.isPending}>Bilgileri düzelt</button>
              <button className="btn btn-primary" onClick={submit} disabled={hire.isPending}>
                <i aria-hidden="true" className="fa-solid fa-check" /> İşe almayı onayla
              </button>
            </>
            : <>
              <button className="btn btn-ghost" onClick={onClose}>Vazgeç</button>
              <button className="btn btn-primary" onClick={prepareReview} disabled={hire.isPending || departmentsQ.isPending || departmentsQ.isError}>
                Bilgileri kontrol et
              </button>
            </>}
        </div>
      </div>
    </div>
  );
}
