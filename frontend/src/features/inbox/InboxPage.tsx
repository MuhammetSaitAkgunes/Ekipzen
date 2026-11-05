import { useSearchParams } from "react-router-dom";
import { useAuth } from "../../auth/AuthContext";
import { ActionsPage } from "../actions/ActionsPage";
import { ApprovalsList } from "./ApprovalsList";
import { DailyGuidanceTasks } from "../guidance/DailyGuidanceTasks";
import { DailyReviewRoute } from "../guidance/DailyReviewRoute";
import { GuidanceDrawer } from "../guidance/GuidanceDrawer";
import { GuidanceSourceLink } from "../guidance/GuidanceSourceLink";
import { GuidanceStepLessons } from "../guidance/GuidanceStepLessons";

export function InboxPage() {
  const { user } = useAuth();
  const management = user?.role === "hr-admin" || user?.role === "manager";
  const [params, setParams] = useSearchParams();
  const requestedTab = params.get("tab");
  const tab = management && requestedTab === "daily" ? "daily"
    : management && requestedTab !== "actions" ? "approvals" : "actions";
  return <div className="ez-inbox">
    <div className="page-header">
      <h1>İş Kutusu</h1>
      <GuidanceDrawer title="İş Kutusu rehberi">
        <GuidanceSourceLink processKey="daily" />
        <GuidanceStepLessons process="daily" />
        <DailyReviewRoute isHrAdmin={user?.role === "hr-admin"} />
      </GuidanceDrawer>
    </div>
    <div className="ez-tabs" role="tablist" aria-label="İş kutusu bölümleri">
      {management && <button role="tab" id="inbox-daily" aria-selected={tab === "daily"} aria-controls="inbox-content" onClick={() => setParams({ tab: "daily" })}>Günlük işler</button>}
      {management && <button role="tab" id="inbox-approvals" aria-selected={tab === "approvals"} aria-controls="inbox-content" onClick={() => setParams({ tab: "approvals" })}>Onaylar</button>}
      <button role="tab" id="inbox-actions" aria-selected={tab === "actions"} aria-controls="inbox-content" onClick={() => setParams({ tab: "actions" })}>Aksiyonlar</button>
    </div>
    <section id="inbox-content" role="tabpanel" aria-labelledby={`inbox-${tab}`}>
      {tab === "daily" ? <div className="ez-panel"><DailyGuidanceTasks compact /></div>
        : tab === "approvals" ? <div className="ez-panel"><div className="ez-panel-heading"><h2>Bekleyen izin talepleri</h2></div><ApprovalsList /></div>
        : <ActionsPage embedded />}
    </section>
  </div>;
}
