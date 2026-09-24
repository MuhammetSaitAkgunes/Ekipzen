import { useState } from "react";
import { ApiError } from "../../api/client";
import { useToast } from "../../layout/ToastProvider";
import { useEmployees } from "../personnel/queries";
import { getLevelText } from "../dashboard/format";
import {
  COMPLIANCE_STATUSES, RISK_LEVELS,
  useAssignComplianceOwner, useCreateComplianceDocument, useUpdateComplianceDocument,
  type ComplianceDocumentDto,
} from "./queries";

export function DocumentModal({ document: doc, onClose }: {
  document: ComplianceDocumentDto | null; onClose: () => void;
}) {
  const { showToast } = useToast();
  const isEdit = doc !== null;
  const createDocument = useCreateComplianceDocument();
  const updateDocument = useUpdateComplianceDocument();
  const assignOwner = useAssignComplianceOwner();
  const employeesQ = useEmployees({ search: "", departmentId: "", status: "" });
  const [form, setForm] = useState({
    employeeId: doc ? String(doc.employeeId ?? "") : "",
    documentName: doc?.document ?? "",
    ownerName: doc?.owner ?? "",
    dueDate: doc?.dueDate ?? "",
    status: doc?.status ?? "Eksik",
    level: doc?.level ?? "medium",
  });
  const [error, setError] = useState<string | null>(null);

  const set = (key: keyof typeof form) =>
    (e: React.ChangeEvent<HTMLInputElement | HTMLSelectElement>) =>
      setForm((f) => ({ ...f, [key]: e.target.value }));

  const submit = async () => {
    setError(null);
    if (!form.documentName.trim() || (!isEdit && !form.employeeId)) {
      setError("Personel ve belge adı zorunludur.");
      return;
    }
    try {
      if (isEdit) {
        await updateDocument.mutateAsync({
          id: doc.id!,
          documentName: form.documentName.trim(),
          dueDate: form.dueDate || null,
          level: form.level ?? "medium",
        });
        const newOwner = form.ownerName.trim();
        if (newOwner && newOwner !== (doc.owner ?? "")) {
          await assignOwner.mutateAsync({ id: doc.id!, ownerName: newOwner });
        }
        showToast("Belge güncellendi.", "success");
      } else {
        await createDocument.mutateAsync({
          employeeId: Number(form.employeeId),
          documentName: form.documentName.trim(),
          ownerName: form.ownerName.trim() || null,
          dueDate: form.dueDate || null,
          status: form.status,
          level: form.level,
        });
        showToast("Uyum belgesi oluşturuldu.", "success");
      }
      onClose();
    } catch (e) {
      setError(e instanceof ApiError ? e.message : "Belge kaydedilemedi.");
    }
  };

  return (
    <div className="modal-overlay" style={{ display: "flex" }}>
      <div className="modal-card scale-in">
        <div className="modal-head">
          <div>
            <h3>{isEdit ? `Belgeyi Düzenle: ${doc.document}` : "Yeni Uyum Belgesi"}</h3>
            <p>{isEdit ? `${doc.employee} · ${doc.dept}` : "Personel için takip edilecek belge kaydı açın."}</p>
          </div>
          <button className="btn-icon-sm" onClick={onClose} title="Kapat" aria-label="Belge penceresini kapat">
            <i aria-hidden="true" className="fa-solid fa-xmark" />
          </button>
        </div>
        <div className="modal-body-scroll">
          <p className="ez-compliance-modal-note">Burada bir belge takip kaydı açılır veya düzenlenir; dosya yüklenmez. Son tarih ve risk düzeyini kaynağına göre doğrulayın. “Tamamlandı” durumunu yalnız belgeyi gerçekten kontrol ettiyseniz seçin.</p>
          {error && <p className="form-error" role="alert">{error}</p>}
          <div className="form-grid-2">
            {!isEdit && (
              <div className="input-group">
                <label className="input-label" htmlFor="doc-employee">Personel</label>
                <select id="doc-employee" className="input-control" value={form.employeeId} onChange={set("employeeId")}>
                  <option value="">Seçin</option>
                  {(employeesQ.data?.items ?? []).map((employee) => (
                    <option key={employee.id} value={employee.id}>{employee.name}</option>
                  ))}
                </select>
              </div>
            )}
            <div className="input-group">
              <label className="input-label" htmlFor="doc-name">Belge adı</label>
              <input id="doc-name" className="input-control" value={form.documentName} onChange={set("documentName")} />
            </div>
            <div className="input-group">
              <label className="input-label" htmlFor="doc-owner">Sorumlu</label>
              <input id="doc-owner" className="input-control" value={form.ownerName} onChange={set("ownerName")} />
            </div>
            <div className="input-group">
              <label className="input-label" htmlFor="doc-due">Son tarih</label>
              <input id="doc-due" type="date" className="input-control" value={form.dueDate ?? ""} onChange={set("dueDate")} />
            </div>
            {!isEdit && (
              <div className="input-group">
                <label className="input-label" htmlFor="doc-status">Durum</label>
                <select id="doc-status" className="input-control" value={form.status ?? "Eksik"} onChange={set("status")}>
                  {COMPLIANCE_STATUSES.map((status) => <option key={status} value={status}>{status}</option>)}
                </select>
              </div>
            )}
            <div className="input-group">
              <label className="input-label" htmlFor="doc-level">Risk seviyesi</label>
              <select id="doc-level" className="input-control" value={form.level ?? "medium"} onChange={set("level")}>
                {RISK_LEVELS.map((level) => <option key={level} value={level}>{getLevelText(level)}</option>)}
              </select>
            </div>
          </div>
        </div>
        <div className="modal-footer">
          <button className="btn btn-ghost" onClick={onClose}>Vazgeç</button>
          <button
            className="btn btn-primary"
            onClick={submit}
            disabled={createDocument.isPending || updateDocument.isPending || assignOwner.isPending}
          >
            <i aria-hidden="true" className="fa-solid fa-check" /> Kaydet
          </button>
        </div>
      </div>
    </div>
  );
}
