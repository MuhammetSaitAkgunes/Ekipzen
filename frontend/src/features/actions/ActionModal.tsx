import { useState } from "react";
import { ApiError } from "../../api/client";
import { useToast } from "../../layout/ToastProvider";
import { useCreateGlobalAction, useUpdateGlobalAction, readCurrentAction, type GlobalActionDto } from "./queries";
import { GuidanceOperationSource } from "../guidance/GuidanceOperationSource";
import { ActionRecordSummary } from "./ActionOperationDialog";

export function ActionModal({ onClose, action }: { onClose: () => void; action?: GlobalActionDto }) {
  const { showToast } = useToast();
  const createAction = useCreateGlobalAction();
  const updateAction = useUpdateGlobalAction();
  const pending = createAction.isPending || updateAction.isPending;
  const [baseline, setBaseline] = useState(action);
  const [guidanceVersion, setGuidanceVersion] = useState<number | null>(null);
  const [confirmed, setConfirmed] = useState(false);
  const [refreshing, setRefreshing] = useState(false);
  const [form, setForm] = useState({
    title: action?.title ?? "", source: action?.source ?? "", owner: action?.owner ?? "", sourceRoute: action?.sourceRoute ?? "", due: action?.due ?? "Bugün", priority: action?.priority ?? "medium", recommendedAction: action?.action ?? "",
  });
  const [error, setError] = useState<string | null>(null);

  const set = (key: keyof typeof form) =>
    (e: React.ChangeEvent<HTMLInputElement | HTMLSelectElement | HTMLTextAreaElement>) =>
      { setConfirmed(false); setForm((f) => ({ ...f, [key]: e.target.value })); };

  const submit = async () => {
    setError(null);
    if (!form.title.trim() || !form.source.trim() || !form.owner.trim()) {
      setError("Başlık, kaynak ve sahip zorunludur.");
      return;
    }
    try {
      const body = {
        title: form.title.trim(),
        source: form.source.trim(),
        owner: form.owner.trim(),
        sourceRoute: form.sourceRoute.trim() || null,
        due: form.due.trim() || null,
        priority: form.priority,
        recommendedAction: form.recommendedAction.trim() || null,
        guidanceContentRevisionId: guidanceVersion ?? undefined,
      };
      if (action) {
        if (!confirmed || !baseline) { setError("Güncel kaynak ve düzeltmenin etkisini doğrulayın."); return; }
        await updateAction.mutateAsync({ id: action.id!, ...body, expectedRevision: baseline.revision });
      } else await createAction.mutateAsync(body);
      showToast(action ? "Aksiyon bilgileri kaydedildi; durum değişmedi." : "Aksiyon oluşturuldu.", "success");
      onClose();
    } catch (e) {
      setConfirmed(false);
      setError(e instanceof ApiError ? e.message : "Aksiyon oluşturulamadı.");
    }
  };

  return (
    <div className="modal-overlay" style={{ display: "flex" }}>
      <div className="modal-card scale-in" role="dialog" aria-modal="true" aria-label={action ? "Aksiyon bilgilerini düzelt" : "Yeni Aksiyon"}>
        <div className="modal-head">
          <div>
            <h3>{action ? "Aksiyon bilgilerini düzelt" : "Yeni Aksiyon"}</h3>
            <p>Bu takip kaydı gerçek İK işleminin yerine geçmez. Vade etiketi yasal son tarih değildir.</p>
          </div>
          <button className="btn-icon-sm" onClick={onClose} title="Kapat" aria-label="Aksiyon penceresini kapat">
            <i aria-hidden="true" className="fa-solid fa-xmark" />
          </button>
        </div>
        <div className="modal-body-scroll">
          <GuidanceOperationSource process="daily" step="task-action" value={guidanceVersion}
            onChange={id => { setGuidanceVersion(id); setConfirmed(false); }} disabled={pending} />
          {baseline && <><ActionRecordSummary action={baseline} />
            <button type="button" className="btn btn-ghost" disabled={pending || refreshing} onClick={async () => {
              setRefreshing(true); setConfirmed(false);
              try { setBaseline(await readCurrentAction(baseline.id!)); }
              catch (cause) { setError(cause instanceof Error ? cause.message : "Kaynak okunamadı."); }
              finally { setRefreshing(false); }
            }}>Güncel aksiyonu yeniden incele</button>
            <p>Yenileme girdiğiniz düzeltmeyi değiştirmez. Yukarıdaki güncel kayıtla formunuzu karşılaştırın; bilgi düzeltmek tamamlanan aksiyonu yeniden açmaz.</p></>}
          {error && <p className="form-error" role="alert">{error}</p>}
          <div className="form-grid-2">
            <div className="input-group">
              <label className="input-label" htmlFor="act-title">Başlık</label>
              <input id="act-title" className="input-control" value={form.title} onChange={set("title")} />
            </div>
            <div className="input-group">
              <label className="input-label" htmlFor="act-source">Kaynak</label>
              <input id="act-source" className="input-control" value={form.source} onChange={set("source")} />
            </div>
            <div className="input-group">
              <label className="input-label" htmlFor="act-owner">Sahip</label>
              <input id="act-owner" className="input-control" value={form.owner} onChange={set("owner")} />
            </div>
            <div className="input-group">
              <label className="input-label" htmlFor="act-due">Vade etiketi</label>
              <input id="act-due" className="input-control" value={form.due} onChange={set("due")} />
            </div>
            <div className="input-group">
              <label className="input-label" htmlFor="act-priority">Öncelik</label>
              <select id="act-priority" className="input-control" value={form.priority} onChange={set("priority")}>
                <option value="high">Yüksek</option>
                <option value="medium">Orta</option>
                <option value="low">Düşük</option>
              </select>
            </div>
            <div className="input-group">
              <label className="input-label" htmlFor="act-route">Kaynak rota (ops.)</label>
              <input id="act-route" className="input-control" placeholder="payroll, compliance-risk…" value={form.sourceRoute} onChange={set("sourceRoute")} />
            </div>
            <div className="input-group">
              <label className="input-label" htmlFor="act-recommended">Önerilen aksiyon</label>
              <textarea id="act-recommended" className="input-control" rows={2} value={form.recommendedAction} onChange={set("recommendedAction")} />
            </div>
          </div>
        </div>
        {action && <label><input type="checkbox" checked={confirmed} onChange={event => setConfirmed(event.target.checked)} /> Güncel kayıtla düzeltmemi karşılaştırdım; yalnız takip bilgileri değişecek.</label>}
        <div className="modal-footer">
          <button className="btn btn-ghost" onClick={onClose}>Vazgeç</button>
          <button className="btn btn-primary" onClick={submit} disabled={pending || refreshing || (!!action && !confirmed)}>
            <i aria-hidden="true" className="fa-solid fa-check" /> Kaydet
          </button>
        </div>
      </div>
    </div>
  );
}
