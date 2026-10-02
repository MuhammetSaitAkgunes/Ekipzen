import { useRef, useState } from "react";
import { Link } from "react-router-dom";
import { appPath } from "../../paths";
import { useDialog } from "../../ui/useDialog";
import { PageError, PageLoading } from "../shared/PageState";
import { ApiError } from "../../api/client";
import { useAuth } from "../../auth/AuthContext";
import { useToast } from "../../layout/ToastProvider";
import { useCreateLeave, useLeaveBalance, useLeaveTypes, usePreviewLeave, useSubstituteOptions, type LeaveTypeDto } from "./queries";
import { GuidanceOperationSource } from "../guidance/GuidanceOperationSource";
import { LeaveTypeConditions } from "./LeaveTypeConditions";

const typeCardMeta = (type: LeaveTypeDto, remaining: number): { icon: string; tone: string; small: string } => {
  const name = type.name ?? "";
  const appearance = name.includes("Rapor") ? { icon: "fa-notes-medical", tone: "sick" }
    : name.includes("Mazeret") ? { icon: "fa-clock", tone: "excuse" }
      : name.includes("Uzaktan") ? { icon: "fa-laptop-house", tone: "remote" }
        : { icon: "fa-sun", tone: "annual" };
  return {
    ...appearance,
    small: type.deductsFromAnnualBalance === true ? "Yıllık bakiye: " + remaining + " gün"
      : type.deductsFromAnnualBalance === false ? "Yıllık bakiyeden düşmez" : "Bakiye etkisi bilinmiyor",
  };
};

type LeaveReview = {
  typeId: number;
  typeName: string;
  deductsFromAnnualBalance: boolean;
  requiresApproval: boolean;
  startDate: string;
  endDate: string;
  calendarDays: number;
  workingDays: number;
  description: string;
  substituteId: number | null;
  substituteName: string | null;
  recordToken: string | null;
  guidanceContentRevisionId: number | null;
};

export function LeaveRequestModal({ onClose }: { onClose: () => void }) {
  const dialogRef = useRef<HTMLDivElement>(null);
  useDialog(dialogRef, true, onClose);
  const { user } = useAuth();
  const { showToast } = useToast();
  const typesQ = useLeaveTypes();
  const balanceQ = useLeaveBalance();
  const substitutesQ = useSubstituteOptions(user?.role !== "employee");
  const createLeave = useCreateLeave();
  const previewLeave = usePreviewLeave();
  const formRevision = useRef(0);

  const [typeId, setTypeId] = useState<number | null>(null);
  const [startDate, setStartDate] = useState("");
  const [endDate, setEndDate] = useState("");
  const [description, setDescription] = useState("");
  const [substituteId, setSubstituteId] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [review, setReview] = useState<LeaveReview | null>(null);
  const [source, setSource] = useState<number | null>(null);

  const types = typesQ.data ?? [];
  const remaining = balanceQ.data?.remainingDays ?? 0;
  const selectedType = types.find((type) => type.id === typeId);
  const validRange = Boolean(startDate && endDate && endDate >= startDate);
  // Bu yalnız takvim günü sayısıdır; sunucunun iş günü hesabının yerine geçmez.
  const previewDays = validRange
    ? Math.round((Date.parse(endDate + "T00:00:00Z") - Date.parse(startDate + "T00:00:00Z")) / 86_400_000) + 1
    : null;

  const prepareReview = async () => {
    if (previewLeave.isPending) return;
    setError(null);
    if (!selectedType?.id) {
      setError("İzin türünü kayıttaki koşullarla karşılaştırıp açıkça seçin.");
      return;
    }
    if (selectedType.deductsFromAnnualBalance === undefined || selectedType.requiresApproval === undefined) {
      setError("İzin türünün bakiye veya onay bilgisi okunamadı; talebi göndermeden önce yeniden yükleyin.");
      return;
    }
    if (!validRange || previewDays === null || !Number.isFinite(previewDays)) {
      setError("Başlangıç ve bitiş tarihlerini doğrulayın; bitiş başlangıçtan önce olamaz.");
      return;
    }
    if (description.trim().length > 1000) {
      setError("Açıklama 1000 karakteri geçemez.");
      return;
    }
    const substitute = substitutesQ.data?.find((employee) => String(employee.id) === substituteId);
    if (substituteId && !substitute?.id) {
      setError("Seçilen vekil doğrulanamadı; listeyi yeniden yükleyip kontrol edin.");
      return;
    }
    const revision = formRevision.current;
    try {
      const preview = await previewLeave.mutateAsync({
        leaveTypeId: selectedType.id,
        startDate,
        endDate,
        substituteEmployeeId: substitute?.id ?? null,
      });
      if (revision !== formRevision.current) return;
      if (!preview.canSubmit) {
        setError(preview.blockReason || "Bu talep şu an gönderilemiyor; bilgileri kontrol edin.");
        return;
      }
      if (source !== null && !preview.recordToken) {
        setError("Rehberli talebin ön kontrol belirteci okunamadı; bilgileri yeniden kontrol edin.");
        return;
      }
      setReview({
        typeId: selectedType.id,
        typeName: selectedType.name ?? "İzin",
        deductsFromAnnualBalance: preview.deductsFromAnnualBalance,
        requiresApproval: preview.requiresApproval,
        startDate,
        endDate,
        calendarDays: previewDays,
        workingDays: preview.workingDays,
        description: description.trim(),
        substituteId: substitute?.id ?? null,
        substituteName: substitute?.name ?? null,
        recordToken: preview.recordToken ?? null,
        guidanceContentRevisionId: source,
      });
    } catch (e) {
      if (revision === formRevision.current) {
        setError(e instanceof ApiError ? e.message : "İzin önizlemesi alınamadı; yeniden deneyin.");
      }
    }
  };

  const submit = async () => {
    if (!review || createLeave.isPending) return;
    setError(null);
    try {
      const created = await createLeave.mutateAsync({
        leaveTypeId: review.typeId,
        startDate: review.startDate,
        endDate: review.endDate,
        description: review.description || null,
        substituteEmployeeId: review.substituteId,
        expectedRecordToken: review.recordToken,
        ...(review.guidanceContentRevisionId !== null ? { guidanceContentRevisionId: review.guidanceContentRevisionId } : {}),
      });
      showToast(created.status === "approved"
        ? "İzin talebi oluşturuldu ve onaylandı."
        : created.status === "pending"
          ? "İzin talebi oluşturuldu; onay bekliyor."
          : "İzin talebi oluşturuldu; güncel durumunu izin listesinde kontrol edin.", "success");
      onClose();
    } catch (e) {
      setReview(null);
      setError(e instanceof ApiError ? e.message : "İzin talebi oluşturulamadı.");
    }
  };

  return (
    <div id="leave-modal-overlay" className="modal-overlay" style={{ display: "flex" }}>
      <div className="modal-card scale-in" ref={dialogRef} role="dialog" aria-modal="true" aria-labelledby="leave-request-title" tabIndex={-1}>
        <div className="modal-head">
          <div>
            <h3 id="leave-request-title">Yeni İzin Talebi</h3>
            <p>İzin türünü ve tarihleri doğrulayın; göndermeden önce sonucu gözden geçirin.</p>
          </div>

          <button className="btn-icon-sm" onClick={onClose} disabled={createLeave.isPending || previewLeave.isPending} title="Kapat" aria-label="İzin talebi penceresini kapat">
            <i aria-hidden="true" className="fa-solid fa-xmark" />
          </button>
        </div>

        <div className="modal-body-scroll">
          {error && <p className="form-error" role="alert">{error}</p>}
          <section className="ez-leave-request-guide" aria-label="İzin talebi rehberi">
            <strong>Ne yapıyoruz?</strong>
            <p>Gerçek izin talebini oluşturuyoruz. Türü, başlangıç ve bitişi; ilgili kayıt ve belgelerle karşılaştırın.</p>
            <p><strong>Neden?</strong> Tür, yıllık bakiyeden düşme ve onay gereksinimini etkiler. Örneğin “Raporlu” adını görmeniz tek başına belge koşulunu kanıtlamaz; belge ve şirket kuralını ayrıca doğrulayın.</p>
            <p>Ön kontrol, iş gününü ve talebin mevcut kayıtlara göre gönderilebilirliğini sunucudan alır. Kaynaklı metinler bağımsız inceleme ve yayın onayından sonra katalogda görünür.</p>
            <Link to={appPath("/guidance/catalog?process=leave")}>İznin kaynaklı rehberini aç</Link>
          </section>
          {typesQ.isPending || balanceQ.isPending ? <PageLoading /> : typesQ.isError || balanceQ.isError ? <PageError error={typesQ.error ?? balanceQ.error} /> : <p className="ez-balance-note">Görüntülenen yıllık izin bakiyesi: <strong>{remaining} gün</strong></p>}
          <p className="input-label" id="leave-type-label">İzin Türü — açıkça seçin</p>
          {typesQ.isSuccess && types.length === 0 && <p role="status">Tanımlı izin türü yok; talep oluşturulamıyor.</p>}
          <div className="type-grid" role="radiogroup" aria-labelledby="leave-type-label">
            {types.map((type) => {
              const meta = typeCardMeta(type, remaining);
              return (
                <label key={type.id} className="type-card">
                  <input
                    type="radio"
                    name="leaveType"
                    checked={typeId === type.id}
                    onChange={() => { formRevision.current++; setReview(null); setTypeId(type.id ?? null); }}
                  />
                  <div className="tc-content">
                    <div className={`tc-icon ${meta.tone}`}><i aria-hidden="true" className={`fa-solid ${meta.icon}`} /></div>
                    <span>{type.name}</span>
                    <small>{meta.small}</small>
                    <small>{type.requiresApproval === true ? "Yönetici onayı gerekir"
                      : type.requiresApproval === false ? "Onay beklemeden oluşur" : "Onay koşulu bilinmiyor"}</small>
                  </div>
                </label>
              );
            })}
          </div>

          <LeaveTypeConditions type={selectedType} />
          <GuidanceOperationSource process="leave" step="request-lifecycle" value={source}
            onChange={id => { formRevision.current++; setSource(id); setReview(null); }} disabled={createLeave.isPending || previewLeave.isPending} />
          <div className="form-grid-2 mt-4">
            <div className="input-group">
              <label className="input-label" htmlFor="start-date">Başlangıç Tarihi</label>
              <input type="date" className="input-control" id="start-date" value={startDate} onChange={(e) => { formRevision.current++; setReview(null); setStartDate(e.target.value); }} />
            </div>
            <div className="input-group">
              <label className="input-label" htmlFor="end-date">Bitiş Tarihi</label>
              <input type="date" className="input-control" id="end-date" value={endDate} onChange={(e) => { formRevision.current++; setReview(null); setEndDate(e.target.value); }} />
            </div>
          </div>

          <div className="calc-box ez-leave-request-calendar">
            <div className="cb-item"><span>Takvim günü önizlemesi</span><strong id="calc-days">{previewDays !== null ? String(previewDays) + " gün" : "- gün"}</strong></div>
          </div>

          <div className="input-group mt-4">
            <label className="input-label" htmlFor="leave-desc">Açıklama / Adres</label>
            <textarea id="leave-desc" className="input-control" rows={2} placeholder="İzin nedeni veya bulunacağınız adres" value={description} onChange={(e) => { formRevision.current++; setReview(null); setDescription(e.target.value); }} />
          </div>

          <p className="ez-quiet">Takvim günü, izin bakiyesinden düşecek iş günü değildir. İş günü, çakışma ve bakiye ön kontrolde sunucudan alınır; gönderimde yeniden doğrulanır. Arada kayıtlar değişebilir. Gösterilen bakiye bugünkü kayıttır.</p>
          <div className="input-group mt-3">
            <label className="input-label" htmlFor="leave-substitute">Yerine Bakacak Kişi</label>
            <select id="leave-substitute" className="input-control" value={substituteId} onChange={(e) => { formRevision.current++; setReview(null); setSubstituteId(e.target.value); }}>
              <option value="">Seçiniz...</option>
              {(substitutesQ.data ?? []).map((emp) => (
                <option key={emp.id} value={emp.id}>{emp.name}</option>
              ))}
            </select>
          </div>
          {review && <section className="ez-leave-request-review" aria-labelledby="leave-review-title" aria-live="polite">
            <h4 id="leave-review-title">Göndermeden önce kontrol edin</h4>
            <p><strong>Tür:</strong> {review.typeName} · {review.deductsFromAnnualBalance ? "Yıllık bakiyeden düşer" : "Yıllık bakiyeden düşmez"}</p>
            <p><strong>Tarih:</strong> {review.startDate} – {review.endDate} · {review.calendarDays} takvim günü</p>
            <p><strong>İş günü:</strong> {review.workingDays} gün (sunucu önizlemesi; gönderimde yeniden doğrulanır)</p>
            <p><strong>Onay:</strong> {review.requiresApproval ? "Yönetici onayı bekleyecek" : "Sunucu kurallarına uygunsa doğrudan onaylanabilir"}</p>
            {review.substituteName && <p><strong>Vekil:</strong> {review.substituteName}</p>}
            {review.description && <p><strong>Açıklama:</strong> {review.description}</p>}
            <p>Gönderimden sonra tarih veya tür bu ekranda düzenlenmez. Bekleyen ya da onaylı talebi iptal edip gerekirse doğru bilgilerle yeni talep oluşturabilirsiniz; onaylı iptal bakiyede ters hareket üretir.</p>
          </section>}
        </div>

        <div className="modal-footer">
          {review ? <>
            <button className="btn btn-ghost" onClick={() => setReview(null)} disabled={createLeave.isPending}>Bilgileri düzelt</button>
            <button className="btn btn-primary" onClick={submit} disabled={createLeave.isPending}>
              <i aria-hidden="true" className="fa-solid fa-paper-plane" /> Talebi onayla ve gönder
            </button>
          </> : <>
            <button className="btn btn-ghost" onClick={onClose}>Vazgeç</button>
            <button className="btn btn-primary" onClick={prepareReview} disabled={createLeave.isPending || previewLeave.isPending || !typesQ.isSuccess || !balanceQ.isSuccess || types.length === 0}>
              {previewLeave.isPending ? "Sunucuda kontrol ediliyor..." : "Bilgileri kontrol et"}
            </button>
          </>}
        </div>
      </div>
    </div>
  );
}
