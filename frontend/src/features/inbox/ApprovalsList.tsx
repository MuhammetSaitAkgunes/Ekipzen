import { useState } from "react";
import { Link } from "react-router-dom";
import { ApiError } from "../../api/client";
import { appPath } from "../../paths";
import { useToast } from "../../layout/ToastProvider";
import { useDecideLeave, usePendingLeaves, type LeaveRequestDto } from "../leaves/queries";
import { formatLeaveDate } from "../leaves/format";
import { PageError, PageLoading } from "../shared/PageState";

function Approval({ request }: { request: LeaveRequestDto }) {
  const decide = useDecideLeave();
  const { showToast } = useToast();
  const [expanded, setExpanded] = useState(false);
  const [error, setError] = useState("");
  const resolve = async (approve: boolean) => {
    if (request.id == null) return;
    setError("");
    try {
      await decide.mutateAsync({ id: request.id, approve });
      showToast(`${request.employeeName} izin talebi ${approve ? "onaylandı" : "reddedildi"}.`, "success");
    } catch (cause) { setError(cause instanceof ApiError ? cause.message : "İşlem tamamlanamadı. Tekrar deneyin."); }
  };
  return <article className="ez-request">
    <div className="ez-request-main">
      <span className="ez-person-avatar" aria-hidden="true">{request.employeeName?.split(" ").map((s) => s[0]).slice(0, 2).join("")}</span>
      <div className="ez-request-person"><strong>{request.employeeName}</strong><span>{request.leaveTypeName} · {request.days ?? "—"} gün</span></div>
      <span className="ez-request-date">{formatLeaveDate(request.startDate)} – {formatLeaveDate(request.endDate)}</span>
      <button className="btn btn-secondary btn-sm" onClick={() => setExpanded(!expanded)} aria-expanded={expanded} aria-controls={`request-${request.id}`}>{expanded ? "Kapat" : "İncele"}</button>
    </div>
    {expanded && <div className="ez-request-detail" id={`request-${request.id}`}>
      <p>{request.description || "Bu talebe açıklama eklenmemiş."}</p>
      {error && <p role="alert" className="form-error">{error}</p>}
      <div className="toolbar-actions"><button className="btn btn-secondary btn-sm" disabled={decide.isPending} onClick={() => resolve(false)}>Reddet</button><button className="btn btn-primary btn-sm" disabled={decide.isPending} onClick={() => resolve(true)}>{decide.isPending ? "İşleniyor…" : "Onayla"}</button></div>
    </div>}
  </article>;
}
export function ApprovalsList({ limit }: { limit?: number }) {
  const query = usePendingLeaves(true);
  if (query.isPending) return <PageLoading />;
  if (query.isError) return <PageError error={query.error} />;
  const requests = query.data;
  return <div className="ez-requests">
    {requests.length === 0 ? <div className="ez-empty"><span className="ez-empty-icon"><i className="fa-solid fa-check" aria-hidden="true" /></span><h3>Her şey güncel</h3><p>İlginizi bekleyen bir izin talebi yok.</p></div> : requests.slice(0, limit).map((request) => <Approval key={request.id} request={request} />)}
    {limit && requests.length > limit && <Link className="ez-section-link" to={appPath("/inbox")}>Tüm {requests.length} talebi incele <span aria-hidden="true">→</span></Link>}
  </div>;
}
