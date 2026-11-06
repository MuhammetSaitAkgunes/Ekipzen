import { useEffect, useMemo, useState } from "react";
import { useNavigate, useSearchParams } from "react-router-dom";
import { useAuth } from "../../auth/AuthContext";
import { PageError, PageLoading } from "../shared/PageState";
import { formatTimeAgo } from "../recruitment/format";
import { appRoutes } from "../../routes";
import { ActionModal } from "./ActionModal";
import { ActionOperationDialog } from "./ActionOperationDialog";
import {
  actionLevelText, actionPillClass, actionStatusText, nextActionStatusLabel,
} from "./format";
import {
  useAuditLogs, useGlobalActions, type GlobalActionDto,
} from "./queries";

const TABS: [string, string][] = [["open", "Açık"], ["week", "Bu Hafta"], ["done", "Tamamlanan"]];

const routePathFor = (routeKey?: string | null): string | null =>
  appRoutes.find((route) => route.key === routeKey)?.path ?? null;

export function ActionsPage({ embedded = false }: { embedded?: boolean }) {
  const { user } = useAuth();
  const navigate = useNavigate();
  const [params] = useSearchParams();
  const requestedId = Number(params.get("actionId"));
  const isMgmt = user?.role === "hr-admin" || user?.role === "manager";
  const isAdmin = user?.role === "hr-admin";
  const [tab, setTab] = useState("open");
  const [followedId, setFollowedId] = useState<number | null>(null);
  const [filters, setFilters] = useState({ priority: "", source: "", owner: "" });
  const [createOpen, setCreateOpen] = useState(false);
  const [editing, setEditing] = useState<GlobalActionDto | null>(null);
  const [decision, setDecision] = useState<{ action: GlobalActionDto; kind: "status" | "delete" } | null>(null);

  // Aksiyon kümesi küçük; tek (filtresiz) sorgu çekilip filtreleme client-side yapılır.
  // Böylece filtre değişiminde ağ isteği olmaz ve seçenek listeleri (kaynak/sahip) stabil kalır.
  const actionsQ = useGlobalActions({ priority: "", source: "", owner: "" });
  const auditQ = useAuditLogs(isMgmt && tab === "audit");

  const actions = useMemo(() => actionsQ.data ?? [], [actionsQ.data]);
  useEffect(() => {
    if (requestedId <= 0 || followedId === requestedId) return;
    const selected = actions.find(action => action.id === requestedId);
    if (selected?.status) { setTab(selected.status); setFilters({ priority: "", source: "", owner: "" }); setFollowedId(requestedId); }
  }, [actions, requestedId, followedId]);
  useEffect(() => {
    if (followedId) document.getElementById("action-" + followedId)?.focus();
  }, [followedId, tab]);
  const kpis = {
    today: actions.filter((item) => item.due === "Bugün" && item.status !== "done").length,
    overdue: actions.filter((item) => item.due === "Gecikti" && item.status !== "done").length,
    high: actions.filter((item) => item.priority === "high" && item.status !== "done").length,
    done: actions.filter((item) => item.status === "done").length,
  };
  const sources = [...new Set(actions.map((item) => item.source ?? ""))].filter(Boolean);
  const owners = [...new Set(actions.map((item) => item.owner ?? ""))].filter(Boolean);
  const visible = useMemo(
    () => (tab === "audit" ? [] : actions.filter((item) =>
      item.status === tab &&
      (!filters.priority || item.priority === filters.priority) &&
      (!filters.source || item.source === filters.source) &&
      (!filters.owner || item.owner === filters.owner))),
    [actions, tab, filters],
  );

  if (actionsQ.isPending) return <PageLoading />;
  if (actionsQ.isError) return <PageError error={actionsQ.error} />;

  const setFilter = (key: "priority" | "source" | "owner") =>
    (e: React.ChangeEvent<HTMLSelectElement>) =>
      setFilters((f) => ({ ...f, [key]: e.target.value }));

  const renderCard = (action: GlobalActionDto) => {
    const sourcePath = routePathFor(action.sourceRoute);
    return (
      <article key={action.id} id={"action-" + action.id} tabIndex={-1} aria-label={action.title ?? "Aksiyon"} className={`global-action-card ${action.priority}`}>
        <div className="global-action-top">
          <span className={`status-pill ${actionPillClass(action.priority)}`}>{actionLevelText(action.priority)}</span>
          <span>{action.due}</span>
        </div>
        <h4>{action.title}</h4>
        <p>{action.action}</p>
        <div className="global-action-meta">
          <span><i aria-hidden="true" className="fa-solid fa-layer-group" /> {action.source}</span>
          <span><i aria-hidden="true" className="fa-solid fa-user" /> {action.owner}</span>
        </div>
        <div className="global-action-footer">
          <span className="status-pill info">{actionStatusText(action.status)}</span>
          <div className="toolbar-actions">
            {isMgmt && nextActionStatusLabel(action.status) && (
              <button className="btn btn-primary btn-sm" onClick={() => setDecision({ action, kind: "status" })}>
                {nextActionStatusLabel(action.status)}
              </button>
            )}
            {sourcePath && (
              <button className="btn btn-secondary btn-sm" onClick={() => navigate(sourcePath)}>Kaynağa git</button>
            )}
            {isAdmin && <button className="btn btn-secondary btn-sm" onClick={() => setEditing(action)}>Bilgileri düzelt</button>}
            {isAdmin && (
              <button
                className="btn-icon-sm"
                title="Sil"
                aria-label={`${action.title} aksiyonunu sil`}
                onClick={() => setDecision({ action, kind: "delete" })}
              >
                <i aria-hidden="true" className="fa-solid fa-trash" />
              </button>
            )}
          </div>
        </div>
      </article>
    );
  };

  return (
    <div id="actions-screen">
      {(!embedded || isAdmin) && (
      <div className="page-header">
        <div>
          {!embedded && <h2>Aksiyonlar</h2>}
        </div>
        {isAdmin && (
          <button className="btn btn-primary" onClick={() => setCreateOpen(true)}>
            <i aria-hidden="true" className="fa-solid fa-plus" /> Yeni Aksiyon
          </button>
        )}
      </div>
      )}

      <div className="actions-kpi-grid">
        <div className="stat-box"><span className="sb-label">Bugün etiketli</span><strong className="sb-val">{kpis.today}</strong></div>
        <div className="stat-box"><span className="sb-label">Gecikti etiketli</span><strong className="sb-val text-red">{kpis.overdue}</strong></div>
        <div className="stat-box"><span className="sb-label">Yüksek Öncelik</span><strong className="sb-val text-orange">{kpis.high}</strong></div>
        <div className="stat-box"><span className="sb-label">Tamamlanan</span><strong className="sb-val">{kpis.done}</strong></div>
      </div>

      <details className="card actions-filter-bar">
        <summary>Filtreler{Object.values(filters).some(Boolean) ? ` · ${Object.values(filters).filter(Boolean).length} etkin` : ""}</summary>
        <div className="ez-action-filters">
          {Object.values(filters).some(Boolean) && <button className="btn btn-secondary" onClick={() => setFilters({ priority: "", source: "", owner: "" })}>Filtreleri temizle</button>}
          <label className="sr-only" htmlFor="actions-priority-filter">Öncelik filtresi</label>
          <select id="actions-priority-filter" className="small-select" value={filters.priority} onChange={setFilter("priority")}>
            <option value="">Öncelik: Tümü</option>
            <option value="high">Yüksek</option>
            <option value="medium">Orta</option>
            <option value="low">Düşük</option>
          </select>
          <label className="sr-only" htmlFor="actions-source-filter">Kaynak filtresi</label>
          <select id="actions-source-filter" className="small-select" value={filters.source} onChange={setFilter("source")}>
            <option value="">Kaynak: Tümü</option>
            {sources.map((source) => <option key={source} value={source}>{source}</option>)}
          </select>
          <label className="sr-only" htmlFor="actions-owner-filter">Sahip filtresi</label>
          <select id="actions-owner-filter" className="small-select" value={filters.owner} onChange={setFilter("owner")}>
            <option value="">Sahip: Tümü</option>
            {owners.map((owner) => <option key={owner} value={owner}>{owner}</option>)}
          </select>
        </div>
      </details>

      <div className="actions-tabs">
        {TABS.map(([key, label]) => (
          <button key={key} className={`action-tab ${tab === key ? "active" : ""}`} onClick={() => setTab(key)}>
            {label}
          </button>
        ))}
        {isMgmt && (
          <button className={`action-tab ${tab === "audit" ? "active" : ""}`} onClick={() => setTab("audit")}>
            Denetim İzi
          </button>
        )}
      </div>

      {tab !== "audit" && (
        <section className="actions-tab-content active">
          <div className="global-actions-grid">
            {visible.map(renderCard)}
            {visible.length === 0 && <div className="empty-lane">Bu filtrede aksiyon yok.</div>}
          </div>
        </section>
      )}

      {tab === "audit" && (
        <section className="actions-tab-content active">
          <section className="card">
            {auditQ.isPending && <PageLoading />}
            {auditQ.isError && <PageError error={auditQ.error} />}
            {auditQ.data && (
              <div className="audit-timeline">
                {auditQ.data.map((log) => (
                  <div key={log.id} className="audit-item">
                    <div className="audit-dot" />
                    <div className="audit-body">
                      <div className="audit-head">
                        <strong>{log.action}</strong>
                        <span>{formatTimeAgo(log.createdAtUtc)}</span>
                      </div>
                      <p>{log.detail}</p>
                      <small>{log.actor} · {log.module}</small>
                    </div>
                  </div>
                ))}
                {auditQ.data.length === 0 && <p className="pending-desc">Henüz denetim kaydı yok.</p>}
              </div>
            )}
          </section>
        </section>
      )}

      {editing && <ActionModal key={editing.id} action={editing} onClose={() => setEditing(null)} />}
      {decision && <ActionOperationDialog key={`${decision.kind}-${decision.action.id}`} {...decision} onClose={() => setDecision(null)} />}
      {createOpen && <ActionModal onClose={() => setCreateOpen(false)} />}
    </div>
  );
}
