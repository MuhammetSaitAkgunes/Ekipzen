import { useEffect, useRef, useState } from "react";
import { GuidanceSourceLink } from "../guidance/GuidanceSourceLink";
import { GuidanceStepLessons } from "../guidance/GuidanceStepLessons";
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
  const [params, setParams] = useSearchParams();
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
  const [pendingStatus, setPendingStatus] = useState<{ document: ComplianceDocumentDto; status: string } | null>(null);
  const confirmButtonRef = useRef<HTMLButtonElement>(null);
  const guideRef = useRef<HTMLElement>(null);

  useEffect(() => {
    const timer = setTimeout(() => setDebouncedSearch(search.trim()), 300);
    return () => clearTimeout(timer);
  }, [search]);

  useEffect(() => {
    if (pendingStatus) confirmButtonRef.current?.focus();
  }, [pendingStatus]);

  const cancelStatusChange = () => {
    const documentId = pendingStatus?.document.id;
    setPendingStatus(null);
    if (documentId) document.getElementById("doc-status-" + documentId)?.focus();
  };

  const documentsQ = useComplianceDocuments({ search: debouncedSearch, status: statusFilter, level: levelFilter });
  const readinessQ = useComplianceReadiness();
  const selectedDocumentExists = documentsQ.data?.some((document) => document.id === selectedDocumentId);

  useEffect(() => {
    if (selectedDocumentId > 0 && selectedDocumentExists) guideRef.current?.focus();
  }, [selectedDocumentId, selectedDocumentExists]);

  if (documentsQ.isPending || readinessQ.isPending) return <PageLoading />;
  if (documentsQ.isError) return <PageError error={documentsQ.error} />;
  if (readinessQ.isError) return <PageError error={readinessQ.error} />;

  const documents = documentsQ.data;
  const displayDocuments = selectedDocumentId > 0
    ? [...documents].sort((a, b) => Number(b.id === selectedDocumentId) - Number(a.id === selectedDocumentId))
    : documents;
  const readiness = readinessQ.data;
  const selectedDocument = documents.find((document) => document.id === selectedDocumentId);

  const changeStatus = async (doc: ComplianceDocumentDto, status: string) => {
    try {
      await setDocumentStatus.mutateAsync({ id: doc.id!, status });
      setPendingStatus(null);
      showToast(`${doc.document} durumu "${status}" yapıldı.`, "success");
    } catch (e) {
      showToast(e instanceof ApiError ? e.message : "Durum güncellenemedi.", "error");
    }
  };

  const showGuide = (id: number) => {
    if (id === selectedDocumentId) {
      guideRef.current?.focus();
      return;
    }
    const next = new URLSearchParams(params);
    next.set("documentId", String(id));
    setParams(next);
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
          <GuidanceSourceLink processKey="compliance" />
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

      <GuidanceStepLessons process="compliance" />
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

      {selectedDocument && (
        <section ref={guideRef} tabIndex={-1} className="ez-panel ez-compliance-guide" aria-labelledby="compliance-guide-title">
          <span className="ez-overline">GERÇEK KAYIT ÜZERİNDE REHBER</span>
          <h3 id="compliance-guide-title">{selectedDocument.employee} · {selectedDocument.document}</h3>
          <p>Bu takip kaydı <strong>{selectedDocument.status}</strong> durumunda. Önce belgenin gerçekten mevcut ve uygun olup olmadığını kendi kaynağından doğrulayın; burada durum değiştirmek dosya yüklemez veya dış kuruma bildirim yapmaz.</p>
          <ol>
            <li>Sorumluyu ({selectedDocument.owner || "henüz atanmadı"}) ve kayıtlı son tarihi ({selectedDocument.dueDate || "yok"}) kontrol edin. Tarih yoksa hukuki son tarih varsaymayın.</li>
            <li>Eksikse sorumluyu belirleyin ve belgeyi temin edin. İncelemedeyse doğrulama sonucunu ayrıca takip edin; bu durum günlük iş kartını kaldırır.</li>
            <li>Yalnız doğruladığınız sonucu {isAdmin ? "durum alanına işleyin" : "İK yöneticisine bildirin"}. Tamamlandı seçimi bu kaydı günlük iş listesinden çıkarır ve risk göstergesini etkiler.</li>
          </ol>
          <p><strong>Örnek:</strong> Bir iş sözleşmesi kaydı “Eksik” görünüyorsa önce imzalı nüshanın gerçekten saklandığını kontrol edin; yalnız çalışana istem göndermek “Tamamlandı” demek için yeterli değildir.</p>
          <p><strong>Düzeltme:</strong> {isAdmin ? "Yanlış durum seçtiyseniz aynı satırdan doğru durumu yeniden seçin. Ad, son tarih, risk veya sorumlu hatalıysa Düzenle’yi açın." : "Hatalı bir kayıt görürseniz İK yöneticisine iletin."} Tamamlanmış kayıt yeniden açıldığında risk seviyesini de tekrar değerlendirin.</p>
          <p><strong>Dayanak sınırı:</strong> Bu ekrandaki tarih ve risk seviyesi kayıt alanlarıdır. Belgenin hangi mevzuat hükmüyle zorunlu olduğu burada doğrulanmadığından, bunları yasal zorunluluk veya son tarih olarak yorumlamayın.</p>
        </section>
      )}
      {selectedDocumentId > 0 && !selectedDocument && (
        <p className="ez-panel" role="status">Bağlantıdaki belge mevcut filtrelerde görünmüyor. Filtreleri temizleyin veya kaydın erişiminizi kontrol edin.</p>
      )}

      {pendingStatus && (
        <section className="ez-panel ez-compliance-confirm" role="group" aria-label="Belge durum değişikliği ön kontrolü" aria-live="polite">
          <h3>Durum değişikliğini doğrulayın</h3>
          <p><strong>{pendingStatus.document.employee} · {pendingStatus.document.document}</strong>: {pendingStatus.document.status} → {pendingStatus.status}</p>
          <p>{pendingStatus.status === "Tamamlandı"
            ? "Belgenin gerçekten mevcut ve doğrulanmış olduğunu kontrol edin. Bu seçim günlük işi kapatır ve risk göstergesini düşürür."
            : pendingStatus.status === "İncelemede"
              ? "Bu seçim kaydı günlük iş listesinden çıkarır; incelemenin sonucunu ayrıca takip edin. Hata varsa durumu yeniden açabilirsiniz."
              : "Bu seçim kaydı günlük iş listesine getirir. Risk seviyesini ve sorumluyu tekrar kontrol edin."}</p>
          <div className="ez-compliance-confirm-actions">
            <button className="btn btn-ghost" onClick={cancelStatusChange} disabled={setDocumentStatus.isPending}>Vazgeç</button>
            <button ref={confirmButtonRef} className="btn btn-primary" onClick={() => changeStatus(pendingStatus.document, pendingStatus.status)} disabled={setDocumentStatus.isPending}>Durumu kaydet</button>
          </div>
        </section>
      )}

      <div className="compliance-layout">
        <div className="table-container">
          <table className="detail-table data-table">
            <thead>
              <tr><th>Personel</th><th>Departman</th><th>Evrak</th><th>Sorumlu</th><th>Son Tarih</th><th>Durum</th><th>Risk</th><th>Rehber</th>{isAdmin && <th>İşlem</th>}</tr>
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
                          onChange={(e) => setPendingStatus({ document: doc, status: e.target.value })}
                          disabled={setDocumentStatus.isPending}
                        >
                          {COMPLIANCE_STATUSES.map((status) => <option key={status} value={status}>{status}</option>)}
                        </select>
                      </>
                    ) : (
                      <span className={`status-pill ${compliancePillClass(doc.status)}`}>{doc.status}</span>
                    )}
                  </td>
                  <td><span className={`risk-badge ${doc.level ?? ""}`}>{getLevelText(doc.level)}</span></td>
                  <td><button className="btn btn-ghost btn-sm" aria-label={doc.document + " belgesinin rehberini aç"} onClick={() => showGuide(doc.id!)}>Rehber</button></td>
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
                <tr><td colSpan={isAdmin ? 9 : 8}><p className="pending-desc">Filtreye uyan belge yok.</p></td></tr>
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
