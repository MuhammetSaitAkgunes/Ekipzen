import { useRef, useState } from "react";
import { ApiError } from "../../api/client";
import { useToast } from "../../layout/ToastProvider";
import { useEmployeeOptions, usePreviewAttendanceEntry, useSaveAttendanceEntry, type AttendanceEntryModel, type AttendanceEntryPreviewDto, type TimesheetRowDto } from "./queries";

const formatMinutes = (minutes: number) => `${Math.floor(minutes / 60)} saat ${minutes % 60} dakika`;
const statusLabel = (status: string) => ({ ok: "normal", late: "geç giriş", absent: "devamsız", overtime: "fazla mesai" })[status as "ok" | "late" | "absent" | "overtime"] ?? status;

export function AttendanceEntryModal({ rowId, initial, defaultEmployeeId, onClose }: {
  rowId: number | null;
  initial?: TimesheetRowDto;
  defaultEmployeeId: number | null;
  onClose: () => void;
}) {
  const { showToast } = useToast();
  const actionInFlight = useRef(false);
  const formRevision = useRef(0);
  const employeesQ = useEmployeeOptions();
  const save = useSaveAttendanceEntry();
  const previewEntry = usePreviewAttendanceEntry();

  const [employeeId, setEmployeeId] = useState(String(rowId !== null ? defaultEmployeeId ?? "" : ""));
  const [workDate, setWorkDate] = useState(initial?.workDate ?? "");
  const [type, setType] = useState(initial?.type ?? "");
  const [checkIn, setCheckIn] = useState(initial?.checkIn?.slice(0, 5) ?? "");
  const [checkOut, setCheckOut] = useState(initial?.checkOut?.slice(0, 5) ?? "");
  const [breakMinutes, setBreakMinutes] = useState(initial ? String(initial.breakMinutes ?? "") : "");
  const [note, setNote] = useState(initial?.note ?? "");
  const [error, setError] = useState<string | null>(null);
  const [reviewing, setReviewing] = useState(false);
  const [preview, setPreview] = useState<AttendanceEntryPreviewDto | null>(null);

  const isEdit = rowId !== null;
  const selectedEmployee = (employeesQ.data ?? []).find((employee) => employee.id === Number(employeeId));

  const buildModel = (): AttendanceEntryModel => ({
    workDate,
    checkIn: checkIn || null,
    checkOut: checkOut || null,
    breakMinutes: Number(breakMinutes),
    type,
    note: note || null,
  });

  const startReview = async () => {
    if (previewEntry.isPending) return;
    setError(null);
    if (!workDate) {
      setError("Tarih seçin.");
      return;
    }
    if (!isEdit && (!employeeId || !selectedEmployee)) {
      setError("Kaydın ait olduğu personeli açıkça seçin.");
      return;
    }
    if (!type) {
      setError("Gün tipini gerçek kayda göre seçin.");
      return;
    }
    const minutes = Number(breakMinutes);
    if (!breakMinutes.trim() || !Number.isInteger(minutes) || minutes < 0 || minutes > 480) {
      setError("Mola süresini 0–480 arasında tam dakika olarak girin.");
      return;
    }
    if (checkIn && checkOut && checkOut <= checkIn) {
      setError("Çıkış saati giriş saatinden sonra olmalı.");
      return;
    }
    if (note.length > 500) {
      setError("Not 500 karakteri geçemez.");
      return;
    }
    const revision = formRevision.current;
    try {
      const result = await previewEntry.mutateAsync({
        employeeId: Number(employeeId) || 0,
        entryId: rowId,
        model: buildModel(),
      });
      if (revision !== formRevision.current) return;
      if (!result.canSave) {
        setError(result.blockReason || "Bu puantaj kaydı şu an kaydedilemiyor; bilgileri kontrol edin.");
        return;
      }
      setPreview(result);
      setReviewing(true);
    } catch (e) {
      if (revision === formRevision.current) {
        setError(e instanceof ApiError ? e.message : "Puantaj önizlemesi alınamadı; yeniden deneyin.");
      }
    }
  };

  const submit = async () => {
    setError(null);
    if (!reviewing || !preview || save.isPending || actionInFlight.current) return;
    actionInFlight.current = true;
    try {
      await save.mutateAsync({
        id: rowId,
        employeeId: Number(employeeId) || 0,
        model: buildModel(),
      });
      showToast(isEdit ? "Puantaj kaydı güncellendi." : "Manuel giriş eklendi.", "success");
      onClose();
    } catch (e) {
      setError(e instanceof ApiError ? e.message : "Puantaj kaydı kaydedilemedi. Güncel satırı kontrol edin.");
    } finally {
      actionInFlight.current = false;
    }
  };

  return (
    <div className="modal-overlay" style={{ display: "flex" }}>
      <div className="modal-card scale-in">
        <div className="modal-head">
          <div>
            <h3>{isEdit ? "Puantaj Kaydını Düzenle" : "Manuel Puantaj Girişi"}</h3>
            <p>Giriş-çıkış saatlerini ve gün tipini doğru girin.</p>
          </div>
          <button className="btn-icon-sm" onClick={onClose} disabled={save.isPending || previewEntry.isPending} title="Kapat" aria-label="Puantaj penceresini kapat">
            <i aria-hidden="true" className="fa-solid fa-xmark" />
          </button>
        </div>

        <div className="modal-body-scroll">
          {error && <p className="form-error" role="alert">{error}</p>}
          <p className="ez-attendance-guide-note">Bu form gerçek puantaj kaydı oluşturur veya mevcut günü düzeltir. Gün tipini ve molayı gerçek kaynağa göre seçin: “Tam” veya “Mesai” seçimi tek başına çalışılan süre ya da yasal fazla çalışma kararı oluşturmaz. Süre sunucuda saatlerden hesaplanır; “Rapor” başka bir sisteme bildirim yapmaz.</p>
          {reviewing ? (
            <section className="ez-attendance-review" aria-labelledby="attendance-review-title">
              <h4 id="attendance-review-title">Kaydetmeden önce kontrol edin</h4>
              <dl>
                <div><dt>Personel</dt><dd>{selectedEmployee?.name ?? "Seçili personel"}</dd></div>
                <div><dt>Tarih ve tip</dt><dd>{workDate} · {type}</dd></div>
                <div><dt>Giriş / çıkış</dt><dd>{checkIn || "Girilmedi"} / {checkOut || "Girilmedi"}</dd></div>
                <div><dt>Mola</dt><dd>{breakMinutes} dakika</dd></div>
                <div><dt>Not</dt><dd>{note || "Yok"}</dd></div>
              </dl>
              {preview && <p role="status">Sunucu hesabı: <strong>{formatMinutes(preview.workedMinutes)} net çalışma</strong>, <strong>{formatMinutes(preview.overtimeMinutes)} fazla mesai alanı</strong>; durum: {statusLabel(preview.status)}, kaydedilecek tip: {preview.effectiveType}.</p>}
              {preview?.warnings.map((warning) => <p className="ez-attendance-review-warning" key={warning}>{warning}</p>)}
              <p>Örnek: 09:00–18:00 arası ve 60 dakika mola girildiğinde uygulama 8 saat net süre hesaplar. Bu hesap tek başına yasal fazla çalışma değerlendirmesi değildir. Kaydederken sunucu kuralları yeniden uygulanır.</p>
              <p>Yanlış gün veya çalışan seçtiyseniz düzeltin. Kaydettikten sonra aynı çalışanın aylık puantajındaki satırı düzenleyebilirsiniz. Başka biri satırı arada değiştirmiş olabilir; güncel puantajı ve oluşturulmuş bordro girdilerini ayrıca kontrol edin.</p>
            </section>
          ) : <>
            {!isEdit && (
              <div className="input-group">
                <label className="input-label" htmlFor="ae-employee">Personel</label>
                <select id="ae-employee" className="input-control" value={employeeId} onChange={(e) => { formRevision.current++; setEmployeeId(e.target.value); }}>
                  <option value="">Seçin</option>
                  {(employeesQ.data ?? []).map((emp) => (
                    <option key={emp.id} value={emp.id}>{emp.name} ({emp.department})</option>
                  ))}
                </select>
              </div>
            )}
            <div className="form-grid-2 mt-3">
              <div className="input-group">
                <label className="input-label" htmlFor="ae-date">Tarih</label>
                <input id="ae-date" type="date" className="input-control" value={workDate} onChange={(e) => { formRevision.current++; setWorkDate(e.target.value); }} disabled={isEdit} />
              </div>
              <div className="input-group">
                <label className="input-label" htmlFor="ae-type">Tip</label>
                <select id="ae-type" className="input-control" value={type} onChange={(e) => { formRevision.current++; setType(e.target.value); }}>
                  <option value="">Gün tipini seçin</option>
                  <option>Tam</option><option>Mesai</option><option>Rapor</option>
                </select>
              </div>
            </div>
            <div className="form-grid-2 mt-3">
              <div className="input-group">
                <label className="input-label" htmlFor="ae-in">Giriş</label>
                <input id="ae-in" type="time" className="input-control" value={checkIn} onChange={(e) => { formRevision.current++; setCheckIn(e.target.value); }} />
              </div>
              <div className="input-group">
                <label className="input-label" htmlFor="ae-out">Çıkış</label>
                <input id="ae-out" type="time" className="input-control" value={checkOut} onChange={(e) => { formRevision.current++; setCheckOut(e.target.value); }} />
              </div>
            </div>
            <div className="form-grid-2 mt-3">
              <div className="input-group">
                <label className="input-label" htmlFor="ae-break">Mola (dk)</label>
                <input id="ae-break" type="number" min="0" max="480" step="1" className="input-control" value={breakMinutes} onChange={(e) => { formRevision.current++; setBreakMinutes(e.target.value); }} />
              </div>
              <div className="input-group">
                <label className="input-label" htmlFor="ae-note">Not</label>
                <input id="ae-note" type="text" className="input-control" value={note ?? ""} onChange={(e) => { formRevision.current++; setNote(e.target.value); }} />
              </div>
            </div>
          </>}
        </div>

        <div className="modal-footer">
          <button className="btn btn-ghost" onClick={onClose} disabled={save.isPending || previewEntry.isPending}>Vazgeç</button>
          {reviewing && <button className="btn btn-ghost" onClick={() => { setReviewing(false); setPreview(null); }} disabled={save.isPending || previewEntry.isPending}>Düzelt</button>}
          <button className="btn btn-primary" onClick={reviewing ? submit : startReview} disabled={save.isPending || previewEntry.isPending}>
            <i aria-hidden="true" className="fa-solid fa-check" /> {reviewing ? "Onayla ve kaydet" : previewEntry.isPending ? "Sunucuda kontrol ediliyor..." : "Kaydı gözden geçir"}
          </button>
        </div>
      </div>
    </div>
  );
}
