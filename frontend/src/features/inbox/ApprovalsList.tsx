import { GuidanceDrawer } from "../guidance/GuidanceDrawer";
import { useEffect, useState } from "react";
import { Link, useSearchParams } from "react-router-dom";
import { ApiError } from "../../api/client";
import { appPath } from "../../paths";
import { useToast } from "../../layout/ToastProvider";
import { useDecideLeave, usePendingLeaves, type LeaveRequestDto } from "../leaves/queries";
import { formatLeaveDate } from "../leaves/format";
import { PageError, PageLoading } from "../shared/PageState";
import { GuidanceDecisionCheck } from "../guidance/GuidanceDecisionCheck";
import { GuidanceOperationDrawer } from "../guidance/GuidanceOperationDrawer";

function Approval({ request, selected }: { request: LeaveRequestDto; selected: boolean }) {
  const decide = useDecideLeave();
  const { showToast } = useToast();
  const [expanded, setExpanded] = useState(selected);
  useEffect(() => { if (selected) setExpanded(true); }, [selected]);
  const [error, setError] = useState("");
  const [reviewDecision, setReviewDecision] = useState<boolean | null>(null);
  const [note, setNote] = useState("");
  const [guidanceVersion, setGuidanceVersion] = useState<number | null>(null);
  const beginReview = (approve: boolean) => {
    setError("");
    setReviewDecision(approve);
    setGuidanceVersion(null);
  };
  const resolve = async () => {
    if (reviewDecision === null) return;
    const approve = reviewDecision;
    if (request.id == null) return;
    setError("");
    try {
      await decide.mutateAsync({ id: request.id, approve, note,
        ...(guidanceVersion !== null ? { guidanceContentRevisionId: guidanceVersion } : {}) });
      setReviewDecision(null);
      setNote("");
      showToast(`${request.employeeName} izin talebi ${approve ? "onaylandı" : "reddedildi"}.`, "success");
    } catch (cause) { setError(cause instanceof ApiError ? cause.message : "İşlem tamamlanamadı. Tekrar deneyin."); }
  };
  return <article id={`request-${request.id}`} className={`ez-request ${selected ? "ez-guidance-target" : ""}`}>
    <div className="ez-request-main">
      <span className="ez-person-avatar" aria-hidden="true">{request.employeeName?.split(" ").map((s) => s[0]).slice(0, 2).join("")}</span>
      <div className="ez-request-person"><strong>{request.employeeName}</strong><span>{request.leaveTypeName} · {request.days ?? "—"} gün</span></div>
      <span className="ez-request-date">{formatLeaveDate(request.startDate)} – {formatLeaveDate(request.endDate)}</span>
      <button className="btn btn-secondary btn-sm" onClick={() => setExpanded(!expanded)} aria-expanded={expanded} aria-controls={`request-detail-${request.id}`}>{expanded ? "Kapat" : "İncele"}</button>
    </div>
    {expanded && <div className="ez-request-detail" id={`request-detail-${request.id}`}>
      <p>{request.description || "Bu talebe açıklama eklenmemiş."}</p>
      {error && <p role="alert" className="form-error">{error}</p>}
      {reviewDecision === null && <GuidanceDrawer title="İzin kararı rehberi"><div className="ez-leave-guide">
        <strong>Karar öncesi kontrol</strong>
        <p>Çalışanı, izin türünü, tarihleri, gün sayısını ve talep açıklamasını gözden geçirin. Eksik veya çelişkili bilgiyi ilgili kişiyle netleştirin.</p>
        <GuidanceDecisionCheck process="leave-approval" step="review-request"
          operationName="izin kararı" eager={false} />
      </div></GuidanceDrawer>}
      {reviewDecision === null ? (
        <div className="toolbar-actions">
          <button className="btn btn-secondary btn-sm" onClick={() => beginReview(false)}>Reddetmeyi incele</button>
          <button className="btn btn-primary btn-sm" onClick={() => beginReview(true)}>Onayı incele</button>
        </div>
      ) : (
        <div className="ez-leave-decision-review ez-workbench" role="group" aria-label="İzin kararını gözden geçir">
          <div className="ez-workbench-layout">
<div className="ez-workbench-guide" role="group" aria-label="İşlem açıklamaları" tabIndex={0}><GuidanceOperationDrawer process="leave" step="decision-effect" value={guidanceVersion}
            onChange={setGuidanceVersion} disabled={decide.isPending}>
          <div className="ez-guide-practice"><h4>Karar alıştırmaları</h4><GuidanceDecisionCheck process="leave-approval" step="decision-effect"
            operationName="izin kararı" eager={false} /></div></GuidanceOperationDrawer>
          </div>
          <div className="ez-workbench-main">

          <h4>{reviewDecision ? "Onay kararı" : "Ret kararı"}</h4>
          <p>{reviewDecision
            ? "Onay talebin durumunu değiştirir. Bakiyeden düşen türlerde sistem onay anında bakiyeyi yeniden kontrol eder ve kullanım hareketini kaydeder."
            : "Ret talebin durumunu değiştirir ve bekleyen onay kuyruğundan çıkarır. Bu ekranda kararı geri alma işlemi yoktur."}</p>

          <label htmlFor={`decision-note-${request.id}`}>Karar notu (isteğe bağlı)</label>
          <textarea id={`decision-note-${request.id}`} className="input-control" rows={2}
            maxLength={1000} value={note} onChange={(event) => setNote(event.target.value)}
            placeholder="Kararın gerekçesini kısa ve somut yazın" />
          <small>Not, izin talebinin karar kaydında saklanır. Gerekçeyi kişisel hassas bilgiler eklemeden yazın.</small>
          <div className="toolbar-actions">
            <button className="btn btn-secondary btn-sm" disabled={decide.isPending} onClick={() => setReviewDecision(null)}>Vazgeç</button>
            <button className={`btn btn-sm ${reviewDecision ? "btn-primary" : "btn-secondary"}`} disabled={decide.isPending} onClick={() => void resolve()}>
              {decide.isPending ? "İşleniyor…" : reviewDecision ? "Onayı kaydet" : "Ret kararını kaydet"}
            </button>
          </div>
        </div></div></div>
      )}
    </div>}
  </article>;
}
export function ApprovalsList({ limit }: { limit?: number }) {
  const [params] = useSearchParams();
  const requestedId = Number(params.get("requestId"));
  const query = usePendingLeaves(true);
  if (query.isPending) return <PageLoading />;
  if (query.isError) return <PageError error={query.error} />;
  const requests = requestedId > 0
    ? [...query.data].sort((a, b) => Number(b.id === requestedId) - Number(a.id === requestedId))
    : query.data;
  return <div className="ez-requests">
    {requests.length === 0 ? <div className="ez-empty"><span className="ez-empty-icon"><i className="fa-solid fa-check" aria-hidden="true" /></span><h3>Her şey güncel</h3><p>İlginizi bekleyen bir izin talebi yok.</p></div> : requests.slice(0, limit).map((request) => <Approval key={request.id} request={request} selected={request.id === requestedId} />)}
    {limit && requests.length > limit && <Link className="ez-section-link" to={appPath("/inbox")}>Tüm {requests.length} talebi incele <span aria-hidden="true">→</span></Link>}
  </div>;
}
