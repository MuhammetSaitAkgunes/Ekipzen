import { useState } from "react";
import { ApiError } from "../../api/client";
import { useToast } from "../../layout/ToastProvider";
import { GuidanceOperationDrawer } from "../guidance/GuidanceOperationDrawer";
import { actionStatusText, nextActionStatus } from "./format";
import { readCurrentAction, useDeleteGlobalAction, useSetActionStatus, type GlobalActionDto } from "./queries";

export function ActionRecordSummary({ action }: { action: GlobalActionDto }) {
  return <section aria-label="İncelenen güncel aksiyon">
    <p><strong>{action.title}</strong> · {actionStatusText(action.status)} · Sorumlu: {action.owner}</p>
    <p>Kaynak: {action.source} · Ekran: {action.sourceRoute || "Belirtilmedi"} · Vade etiketi: {action.due || "Yok"} · Öncelik: {action.priority === "high" ? "Yüksek" : action.priority === "low" ? "Düşük" : "Orta"}</p>
    <p>Yapılacak iş: {action.action || "Belirtilmedi"}. Vade etiketi yasal son tarih değildir.</p>
  </section>;
}

export function ActionOperationDialog({ action, kind, onClose }: {
  action: GlobalActionDto; kind: "status" | "delete"; onClose: () => void;
}) {
  const status = useSetActionStatus(); const remove = useDeleteGlobalAction(); const { showToast } = useToast();
  const [baseline, setBaseline] = useState(action);
  const [guidanceVersion, setGuidanceVersion] = useState<number | null>(null);
  const [confirmed, setConfirmed] = useState(false); const [refreshing, setRefreshing] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const next = nextActionStatus(action.status);
  const pending = status.isPending || remove.isPending;
  const save = async () => {
    if (!confirmed) return;
    setError(null);
    try {
      const args = { id: action.id!, expectedRevision: baseline.revision, guidanceContentRevisionId: guidanceVersion ?? undefined };
      if (kind === "delete") await remove.mutateAsync(args);
      else if (next) await status.mutateAsync({ ...args, status: next });
      else return;
      showToast(kind === "delete" ? "Takip kaydı silindi; kaynak İK işlemi değişmedi." : "Aksiyon durumu kaydedildi; günlük liste yenilendi.", "success");
      onClose();
    } catch (cause) { setConfirmed(false); setError(cause instanceof ApiError ? cause.message : "Sonuç doğrulanamadı; tekrar göndermeden güncel kaydı inceleyin."); }
  };
  return <div className="modal-overlay" style={{ display: "flex" }}><div className="modal-card scale-in" role="dialog" aria-modal="true" aria-label={kind === "delete" ? "Takip aksiyonunu sil" : "Aksiyon durumunu ilerlet"}>
    <div className="modal-head"><h3>{kind === "delete" ? "Takip aksiyonunu sil" : "Aksiyon durumunu ilerlet"}</h3><button type="button" className="btn btn-ghost" onClick={onClose}>Vazgeç</button></div>
    <div className="modal-body-scroll ez-workbench">
          <div className="ez-workbench-layout">
<div className="ez-workbench-guide" role="group" aria-label="İşlem açıklamaları" tabIndex={0}><GuidanceOperationDrawer process="daily" step="task-action" value={guidanceVersion} onChange={id => { setGuidanceVersion(id); setConfirmed(false); }} disabled={pending} /></div>
          <div className="ez-workbench-main">

      <ActionRecordSummary action={baseline} />
      <p>{kind === "delete" ? "Yalnız takip kaydı kaldırılır. Kaynak bordro, izin veya uyum kaydı ve önceki öğrenme kanıtı korunur. İşin listeden kaybolması gerçek yükümlülüğün tamamlandığı anlamına gelmez."
        : `Hedef durum: ${actionStatusText(next)}. Bu, takip durumudur; gerçek İK işlemini veya dış bildirimi yapmaz. Tamamlanan aksiyon bu ekrandan yeniden açılamaz.`}</p>

      <button type="button" className="btn btn-ghost" disabled={pending || refreshing} onClick={async () => {
        setRefreshing(true); setConfirmed(false);
        try { setBaseline(await readCurrentAction(action.id!)); }
        catch (cause) { setError(cause instanceof Error ? cause.message : "Kaynak okunamadı."); }
        finally { setRefreshing(false); }
      }}>Güncel aksiyonu yeniden incele</button>
      <label><input type="checkbox" checked={confirmed} onChange={event => setConfirmed(event.target.checked)} /> Güncel kaydı ve takip işleminin etkisini doğruladım.</label>
      {error && <p role="alert">{error}</p>}
    </div></div></div>
    <div className="modal-footer"><button type="button" className="btn btn-primary" disabled={!confirmed || pending || refreshing || (kind === "status" && (!next || baseline.status === "done" || baseline.status === next))} onClick={save}>{kind === "delete" ? "Takip kaydını sil" : "Durumu kaydet"}</button></div>
  </div></div>;
}
