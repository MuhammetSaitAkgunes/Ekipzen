import { GuidanceOperationDrawer } from "../guidance/GuidanceOperationDrawer";
import { useRef, useState } from "react";
import { ApiError } from "../../api/client";
import { useToast } from "../../layout/ToastProvider";
import { useEmployees } from "../personnel/queries";
import { getLevelText } from "../dashboard/format";
import {
  COMPLIANCE_STATUSES, RISK_LEVELS,
  useCreateComplianceDocument, useUpdateComplianceDocument,
  type ComplianceDocumentDto,
} from "./queries";

export function DocumentModal({ document: doc, onClose }: {
  document: ComplianceDocumentDto | null; onClose: () => void;
}) {
  const { showToast } = useToast();
  const actionInFlight = useRef(false);
  const isEdit = doc !== null;
  const createDocument = useCreateComplianceDocument();
  const updateDocument = useUpdateComplianceDocument();
  const employeesQ = useEmployees({ search: "", departmentId: "", status: "" });
  const [form, setForm] = useState({
    employeeId: doc ? String(doc.employeeId ?? "") : "",
    documentName: doc?.document ?? "",
    ownerName: doc?.owner ?? "",
    dueDate: doc?.dueDate ?? "",
    status: doc?.status ?? "",
    level: doc?.level ?? "",
  });
  const [error, setError] = useState<string | null>(null);
  const [guidanceVersion, setGuidanceVersion] = useState<number | null>(null);
  const [reviewing, setReviewing] = useState(false);
  const [acknowledged, setAcknowledged] = useState(false);
  const isBusy = createDocument.isPending || updateDocument.isPending;
  const selectedEmployee = (employeesQ.data?.items ?? []).find((employee) => String(employee.id) === form.employeeId);

  const set = (key: keyof typeof form) =>
    (e: React.ChangeEvent<HTMLInputElement | HTMLSelectElement>) => {
      setForm((f) => ({ ...f, [key]: e.target.value }));
      setReviewing(false);
      setAcknowledged(false);
    };

  const validate = (): string | null => {
    if (!isEdit && (!form.employeeId || !selectedEmployee)) return "Belge kaydının ait olduğu personeli seçin.";
    if (!form.documentName.trim() || form.documentName.trim().length > 200) return "Belge adını doğrulayın (1–200 karakter).";
    if (form.ownerName.trim().length > 128) return "Sorumlu adı 128 karakteri geçemez.";
    if (isEdit && !doc.revision) return "Güncel belge sürümü bulunamadı. Kaydı yeniden açın.";
    if (!isEdit && !form.status) return "Belgenin gerçek durumunu açıkça seçin.";
    if (!form.level) return "Risk seviyesini kayıt kaynağına göre açıkça seçin.";
    return null;
  };

  const startReview = () => {
    setError(null);
    const validationError = validate();
    if (validationError) { setError(validationError); return; }
    setAcknowledged(false);
    setReviewing(true);
  };

  const submit = async () => {
    if (!reviewing || !acknowledged || isBusy || actionInFlight.current) return;
    setError(null);
    const validationError = validate();
    if (validationError) { setError(validationError); return; }
    actionInFlight.current = true;
    try {
      if (isEdit) {
        await updateDocument.mutateAsync({
          id: doc.id!,
          expectedRevision: doc.revision!,
          updateOwner: true, ownerName: form.ownerName.trim() || null,
          documentName: form.documentName.trim(),
          dueDate: form.dueDate || null,
          level: form.level,
          ...(guidanceVersion === null ? {} : { guidanceContentRevisionId: guidanceVersion }),
        });
        showToast("Belge güncellendi.", "success");
      } else {
        await createDocument.mutateAsync({
          employeeId: Number(form.employeeId),
          documentName: form.documentName.trim(),
          ownerName: form.ownerName.trim() || null,
          dueDate: form.dueDate || null,
          status: form.status,
          level: form.level,
          ...(guidanceVersion === null ? {} : { guidanceContentRevisionId: guidanceVersion }),
        });
        showToast("Uyum belgesi oluşturuldu.", "success");
      }
      onClose();
    } catch (e) {
      const message = e instanceof ApiError ? e.message : "Belge kaydedilemedi.";
      setError(message);
    } finally {
      actionInFlight.current = false;
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
          <button className="btn-icon-sm" onClick={onClose} disabled={isBusy} title="Kapat" aria-label="Belge penceresini kapat">
            <i aria-hidden="true" className="fa-solid fa-xmark" />
          </button>
        </div>
        <div className="modal-body-scroll ez-workbench">
          <div className="ez-workbench-layout">
<div className="ez-workbench-guide" role="group" aria-label="İşlem açıklamaları" tabIndex={0}><GuidanceOperationDrawer process="compliance" step={isEdit ? "record-correction" : "record-review"} value={guidanceVersion} onChange={(id) => { setGuidanceVersion(id); setAcknowledged(false); }} disabled={isBusy} /></div>
          <div className="ez-workbench-main">

          <p className="ez-compliance-modal-note">Burada bir belge takip kaydı açılır veya düzenlenir; dosya yüklenmez. Son tarih, durum ve risk düzeyini kaynağına göre doğrulayın. “Tamamlandı” seçimi günlük iş görünümünü etkiler ama belgenin kendisini veya dış bildirimi doğrulamaz.</p>

          {isEdit && <p>Belge bilgileri ve sorumlu birlikte kaydedilir; güncel sürüm değişmişse kayıt durdurulur. Sorumlu boş bırakılırsa atama kaldırılır.</p>}
          {error && <p className="form-error" role="alert">{error}</p>}
          {reviewing ? <section className="ez-compliance-document-review" role="region" aria-label="Belge kaydını kaydetmeden önce kontrol edin">
            <h4>Belge kaydını kaydetmeden önce kontrol edin</h4>
            <p><strong>Personel:</strong> {isEdit ? doc.employee : selectedEmployee?.name} · <strong>Belge:</strong> {form.documentName.trim()}</p>
            <p><strong>Durum:</strong> {form.status} · <strong>Risk:</strong> {getLevelText(form.level)} · <strong>Son tarih:</strong> {form.dueDate || "Kayıtlı değil"}</p>
            <p><strong>Sorumlu:</strong> {form.ownerName.trim() || "Atanmadı"}{isEdit && form.ownerName.trim() !== (doc.owner ?? "") ? " (birlikte güncellenecek)" : ""}</p>
            <p>Bu kayıt bir takip kaydıdır; belge dosyasını yüklemez. Son tarih yoksa hukuki süre varsaymayın. Yanlış adı, tarihi veya riski daha sonra düzenleyebilir; durumu listeden değiştirebilirsiniz.</p>
            <label><input type="checkbox" checked={acknowledged} onChange={(event) => setAcknowledged(event.target.checked)} /> Personel, belge, durum, risk ve kaynak bilgisini kontrol ettim.</label>
          </section> : <div className="form-grid-2">
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
                <select id="doc-status" className="input-control" value={form.status} onChange={set("status")}>
                  <option value="">Durumu seçin</option>
                  {COMPLIANCE_STATUSES.map((status) => <option key={status} value={status}>{status}</option>)}
                </select>
              </div>
            )}
            <div className="input-group">
              <label className="input-label" htmlFor="doc-level">Risk seviyesi</label>
              <select id="doc-level" className="input-control" value={form.level} onChange={set("level")}>
                <option value="">Risk seviyesini seçin</option>
                {RISK_LEVELS.map((level) => <option key={level} value={level}>{getLevelText(level)}</option>)}
              </select>
            </div>
          </div>}
        </div></div></div>
        <div className="modal-footer">
          <button className="btn btn-ghost" onClick={onClose} disabled={isBusy}>Vazgeç</button>
          {reviewing && <button className="btn btn-ghost" onClick={() => { setReviewing(false); setAcknowledged(false); }} disabled={isBusy}>Bilgileri düzelt</button>}
          <button className="btn btn-primary" onClick={reviewing ? submit : startReview}
            disabled={isBusy || (reviewing && !acknowledged)}>
            <i aria-hidden="true" className="fa-solid fa-check" /> {reviewing ? "Onayla ve kaydet" : "Kaydı gözden geçir"}
          </button>
        </div>
      </div>
    </div>
  );
}
