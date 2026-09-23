import { useSearchParams } from "react-router-dom";
import { useAuth } from "../../auth/AuthContext";
import { ActionsPage } from "../actions/ActionsPage";
import { ApprovalsList } from "./ApprovalsList";

export function InboxPage() {
  const { user } = useAuth();
  const management = user?.role === "hr-admin" || user?.role === "manager";
  const [params, setParams] = useSearchParams();
  const tab = management && params.get("tab") !== "actions" ? "approvals" : "actions";
  return <div className="ez-inbox">
    <div className="page-header"><div><span className="ez-overline">GÜNLÜK ÇALIŞMA</span><h1>İş Kutusu</h1><p>Bekleyen işleri gözden geçirin, gününüzü kolaylaştırın.</p></div></div>
    <div className="ez-tabs" role="tablist" aria-label="İş kutusu bölümleri">
      {management && <button role="tab" id="inbox-approvals" aria-selected={tab === "approvals"} aria-controls="inbox-content" onClick={() => setParams({ tab: "approvals" })}>Onaylar</button>}
      <button role="tab" id="inbox-actions" aria-selected={tab === "actions"} aria-controls="inbox-content" onClick={() => setParams({ tab: "actions" })}>Aksiyonlar</button>
    </div>
    <section id="inbox-content" role="tabpanel" aria-labelledby={`inbox-${tab}`}>
      {tab === "approvals" ? <div className="ez-panel"><div className="ez-panel-heading"><div><h2>Bekleyen izin talepleri</h2><p>Karar vermeden önce talebin ayrıntılarını inceleyin.</p></div></div><ApprovalsList /></div> : <ActionsPage embedded />}
    </section>
  </div>;
}
