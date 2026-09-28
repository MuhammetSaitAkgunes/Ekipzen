import { useSearchParams } from "react-router-dom";
import { useAuth } from "../../auth/AuthContext";
import { ActionsPage } from "../actions/ActionsPage";
import { ApprovalsList } from "./ApprovalsList";
import { DailyGuidanceTasks } from "../guidance/DailyGuidanceTasks";
import { DailyReviewRoute } from "../guidance/DailyReviewRoute";

export function InboxPage() {
  const { user } = useAuth();
  const management = user?.role === "hr-admin" || user?.role === "manager";
  const [params, setParams] = useSearchParams();
  const requestedTab = params.get("tab");
  const tab = management && requestedTab === "daily" ? "daily"
    : management && requestedTab !== "actions" ? "approvals" : "actions";
  return <div className="ez-inbox">
    <div className="page-header"><div><span className="ez-overline">GÜNLÜK ÇALIŞMA</span><h1>İş Kutusu</h1><p>Bekleyen işleri gözden geçirin, gününüzü kolaylaştırın.</p></div></div>
    <div className="ez-tabs" role="tablist" aria-label="İş kutusu bölümleri">
      {management && <button role="tab" id="inbox-daily" aria-selected={tab === "daily"} aria-controls="inbox-content" onClick={() => setParams({ tab: "daily" })}>Günlük işler</button>}
      {management && <button role="tab" id="inbox-approvals" aria-selected={tab === "approvals"} aria-controls="inbox-content" onClick={() => setParams({ tab: "approvals" })}>Onaylar</button>}
      <button role="tab" id="inbox-actions" aria-selected={tab === "actions"} aria-controls="inbox-content" onClick={() => setParams({ tab: "actions" })}>Aksiyonlar</button>
    </div>
    <section id="inbox-content" role="tabpanel" aria-labelledby={`inbox-${tab}`}>
      {tab === "daily" ? <div className="ez-panel"><div className="ez-panel-heading"><div><h2>Gerçek kayıtlardan günlük işler</h2><p>İzin onayları ve takip gerektiren belge kayıtları güncel durumlarıyla listelenir.</p></div></div><DailyGuidanceTasks /><DailyReviewRoute isHrAdmin={user?.role === "hr-admin"} /></div> : tab === "approvals" ? <div className="ez-panel"><div className="ez-panel-heading"><div><h2>Bekleyen izin talepleri</h2><p>Karar vermeden önce talebin ayrıntılarını inceleyin.</p></div></div><ApprovalsList /></div> : <ActionsPage embedded />}
    </section>
  </div>;
}
