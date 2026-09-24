import { useEffect, useState } from "react";
import { useSearchParams } from "react-router-dom";
import { ApiError } from "../../api/client";
import { useAuth } from "../../auth/AuthContext";
import { useToast } from "../../layout/ToastProvider";
import { PageError, PageLoading } from "../shared/PageState";
import { BackToRisk } from "../dashboard/BackToRisk";
import { getLevelText } from "../dashboard/format";
import { DocumentModal } from "./DocumentModal";
import {
  COMPLIANCE_STATUSES, RISK_LEVELS, useComplianceDocuments, useComplianceReadiness,
  useSetComplianceStatus, type ComplianceDocumentDto,
} from "./queries";

export const compliancePillClass = (status?: string | null): string =>
  status === "Tamamlandı" ? "approved" : status === "Eksik" ? "rejected" : "pending";

export function CompliancePage() {
  const { user } = useAuth();
  const [params] = useSearchParams();
  const selectedDocumentId = Number(params.get("documentId"));
  const isAdmin = user?.role === "hr-admin";
  const { showToast } = useToast();
  const setDocumentStatus = useSetComplianceStatus();
  const [search, setSearch] = useState("");
  const [debouncedSearch, setDebouncedSearch] = useState("");
  const [statusFilter, setStatusFilter] = useState("");
  const [levelFilter, setLevelFilter] = useState("");
  const [documentModal, setDocumentModal] = useState<
    { mode: "create" } | { mode: "edit"; document: ComplianceDocumentDto } | null
  >(null);

  useEffect(() => {
    const timer = setTimeout(() => setDebouncedSearch(search.trim()), 300);
    return () => clearTimeout(timer);
  }, [search]);

  const documentsQ = useComplianceDocuments({ search: debouncedSearch, status: statusFilter, level: levelFilter });
  const readinessQ = useComplianceReadiness();

  if (documentsQ.isPending || readinessQ.isPending) return <PageLoading />;
  if (documentsQ.isError) return <PageError error={documentsQ.error} />;
  if (readinessQ.isError) return <PageError error={readinessQ.error} />;

  const documents = documentsQ.data;
  const displayDocuments = selectedDocumentId > 0
    ? [...documents].sort((a, b) => Number(b.id === selectedDocumentId) - Number(a.id === selectedDocumentId))
    : documents;
  const readiness = readinessQ.data;

  const changeStatus = async (doc: ComplianceDocumentDto, status: string) => {
    try {
      await setDocumentStatus.mutateAsync({ id: doc.id!, status });
      showToast(`${doc.document} durumu "${status}" yapıldı.`, "success");
    } catch (e) {
      showToast(e instanceof ApiError ? e.message : "Durum güncellenemedi.", "error");
    }
  };

  const deadlines = documents
    .filter((doc) => doc.status !== "Tamamlandı" && doc.dueDate)
    .sort((a, b) => String(a.dueDate).localeCompare(String(b.dueDate)))
    .slice(0, 5);

  return (
    <div className="detail-page">
      <div className="page-header">
        <div>
          <h2>Uyum, Evrak ve Denetim Risk Merkezi</h2>
          <p>Eksik evrak, yaklaşan son tarih ve denetim hazırlığını operasyonel takip görünümünde yönetin.</p>
        </div>
        <BackToRisk />
      </div>

      <div className="detail-kpi-grid">
        <div className="stat-box"><span className="sb-label">Evrak Uyum Skoru</span><strong className="sb-val">{readiness.documentComplianceScore ?? 0}<small>/100</small></strong></div>
        <div className="stat-box"><span className="sb-label">Eksik Evrak</span><strong className="sb-val text-red">{readiness.missingCount ?? 0}</strong></div>
        <div className="stat-box"><span className="sb-label">Süresi Yaklaşan</span><strong className="sb-val text-orange">{readiness.dueSoonCount ?? 0}</strong></div>
        <div className="stat-box"><span className="sb-label">Denetim Riski</span><strong className="sb-val text-orange">{readiness.readinessRisk}</strong></div>
      </div>

      <div className="toolbar-actions compliance-toolbar">
        <div className="search-wrap">
          <i aria-hidden="true" className="fa-solid fa-magnifying-glass" />
          <label className="sr-only" htmlFor="comp-search">Evrak veya personel ara</label>
          <input
            id="comp-search"
            type="text"
            placeholder="Evrak veya personel ara"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
          />
        </div>
        <label className="sr-only" htmlFor="comp-status">Durum filtresi</label>
        <select id="comp-status" className="input-control" value={statusFilter} onChange={(e) => setStatusFilter(e.target.value)}>
          <option value="">Tüm durumlar</option>
          {COMPLIANCE_STATUSES.map((status) => <option key={status} value={status}>{status}</option>)}
        </select>
        <label className="sr-only" htmlFor="comp-level">Risk filtresi</label>
        <select id="comp-level" className="input-control" value={levelFilter} onChange={(e) => setLevelFilter(e.target.value)}>
          <option value="">Tüm riskler</option>
          {RISK_LEVELS.map((level) => <option key={level} value={level}>{getLevelText(level)}</option>)}
        </select>
        {isAdmin && (
          <button className="btn btn-primary" onClick={() => setDocumentModal({ mode: "create" })}>
            <i aria-hidden="true" className="fa-solid fa-plus" /> Yeni Belge
          </button>
        )}
      </div>

      <div className="compliance-layout">
        <div className="table-container">
          <table className="detail-table data-table">
            <thead>
              <tr><th>Personel</th><th>Departman</th><th>Evrak</th><th>Sorumlu</th><th>Son Tarih</th><th>Durum</th><th>Risk</th>{isAdmin && <th>İşlem</th>}</tr>
            </thead>
            <tbody>
              {displayDocuments.map((doc) => (
                <tr key={doc.id} id={`document-${doc.id}`} className={doc.id === selectedDocumentId ? "ez-guidance-target" : undefined}>
                  <td><strong>{doc.employee}</strong></td>
                  <td>{doc.dept}</td>
                  <td>{doc.document}</td>
                  <td>{doc.owner || "-"}</td>
                  <td>{doc.dueLabel}</td>
                  <td>
                    {isAdmin ? (
                      <>
                        <label className="sr-only" htmlFor={`doc-status-${doc.id}`}>Belge durumu</label>
                        <select
                          id={`doc-status-${doc.id}`}
                          className="input-control input-sm"
                          value={doc.status ?? "Eksik"}
                          onChange={(e) => changeStatus(doc, e.target.value)}
                        >
                          {COMPLIANCE_STATUSES.map((status) => <option key={status} value={status}>{status}</option>)}
                        </select>
                      </>
                    ) : (
                      <span className={`status-pill ${compliancePillClass(doc.status)}`}>{doc.status}</span>
                    )}
                  </td>
                  <td><span className={`risk-badge ${doc.level ?? ""}`}>{getLevelText(doc.level)}</span></td>
                  {isAdmin && (
                    <td>
                      <button
                        className="btn-icon-sm"
                        title="Düzenle"
                        aria-label={`${doc.document} belgesini düzenle`}
                        onClick={() => setDocumentModal({ mode: "edit", document: doc })}
                      >
                        <i aria-hidden="true" className="fa-solid fa-pen" />
                      </button>
                    </td>
                  )}
                </tr>
              ))}
              {documents.length === 0 && (
                <tr><td colSpan={isAdmin ? 8 : 7}><p className="pending-desc">Filtreye uyan belge yok.</p></td></tr>
              )}
            </tbody>
          </table>
        </div>

        <aside className="card insight-panel">
          <div className="card-header-clean">
            <div>
              <h4>Yaklaşan Son Tarihler</h4>
              <p className="text-muted">Kritik evrak aksiyonları ve sorumlular.</p>
            </div>
          </div>
          <div className="deadline-list">
            {deadlines.map((doc) => (
              <div key={doc.id} className={`deadline-item ${doc.level ?? ""}`}>
                <div>
                  <strong>{doc.document}</strong>
                  <span>{doc.employee} · {doc.owner || "Sorumlu yok"}</span>
                </div>
                <em>{doc.dueLabel}</em>
              </div>
            ))}
            {deadlines.length === 0 && <p className="pending-desc">Yaklaşan son tarih yok.</p>}
          </div>
        </aside>
      </div>

      <div className="detail-support-grid">
        <section className="card">
          <div className="card-header-clean"><h4>Denetim Hazırlığı</h4><span className="status-pill pending">{readiness.readinessScore ?? 0}/100</span></div>
          <div className="audit-readiness-list">
            {(readiness.auditChecklist ?? []).map((item) => (
              <div key={item.label} className={`audit-readiness-item ${item.level ?? ""}`}>
                <div className="capacity-top"><span>{item.label}</span><strong>{item.value}%</strong></div>
                <div className="progress-bar"><div className="fill" style={{ width: `${item.value}%` }} /></div>
              </div>
            ))}
          </div>
        </section>
        <section className="card">
          <div className="card-header-clean"><h4>Önerilen Aksiyonlar</h4></div>
          <div className="signal-list">
            {(readiness.recommendedActions ?? []).map((action) => (
              <div key={action} className="signal-note action">
                <i aria-hidden="true" className="fa-solid fa-check" /><span>{action}</span>
              </div>
            ))}
          </div>
        </section>
      </div>

      {documentModal && (
        <DocumentModal
          document={documentModal.mode === "edit" ? documentModal.document : null}
          onClose={() => setDocumentModal(null)}
        />
      )}
    </div>
  );
}
