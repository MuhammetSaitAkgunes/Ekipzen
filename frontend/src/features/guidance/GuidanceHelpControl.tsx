import { useId, useState, type ReactNode } from "react";
import { useAuth } from "../../auth/AuthContext";
import { processTitles } from "./catalogQueries";
import { guidanceProcessPath } from "./processNavigation";
import { useGuidanceHelpPreferences, useSaveGuidanceHelpPreference } from "./helpQueries";

export function GuidanceHelpControl({ process, children, adaptiveReady = false }: { process: string; adaptiveReady?: boolean; children?: (compact: boolean) => ReactNode }) {
  const { user } = useAuth(); const name = useId();
  const query = useGuidanceHelpPreferences(user?.id);
  const save = useSaveGuidanceHelpPreference(user?.id);
  const stored = query.data?.find(item => item.processKey === process);
  const mode = query.isSuccess ? stored?.mode ?? "detailed" : "detailed";
  return <div className="ez-help-preference">
    <details><summary>Anlatımı ayarla</summary><fieldset disabled={!query.isSuccess || save.isPending}>
      <legend>{processTitles[process]} anlatım tercihim</legend>
      {(["detailed", "compact", "adaptive"] as const).map(value => <label key={value} className="ez-guidance-inline">
        <input type="radio" name={name} checked={mode === value} onChange={() => {
          save.mutate({ processKey: process, mode: value, expectedRevision: stored?.revision ?? 0 });
        }} />{value === "detailed" ? "Ayrıntılı anlatım" : value === "compact" ? "Kısa anlatım" : "Uyarlanan anlatım"}
      </label>)}
    </fieldset>
    <p>Kısa anlatımda kaynaklı adımların uzun örnekleri isteğe bağlı açılır. İşlem, ön koşul, gerekçe, dayanak, düzeltme ve uzman yönlendirmeleri görünür kalır. Bu seçim yeterlilik veya işin tamamlandığı anlamına gelmez.</p>
    {query.isPending && <p>Tercihiniz yükleniyor; ayrıntılı anlatım gösteriliyor.</p>}
    {query.isError && <p role="alert">Tercihiniz yüklenemedi; ayrıntılı anlatım gösteriliyor. <button type="button" onClick={() => void query.refetch()}>Tercihi yeniden yükle</button></p>}
    {save.isError && <p role="alert">{save.error.message} <button type="button" onClick={() => { save.reset(); void query.refetch(); }}>Güncel tercihi yeniden yükle</button></p>}
    {save.isSuccess && <p role="status">Anlatım tercihiniz kaydedildi.</p>}
    {mode === "adaptive" && <p role="status">{adaptiveReady ? "Bu adımın güncel sürümünde kendi doğru alıştırma yanıtınız ve gerçek işlem kanıtınız var; uzun örneği istediğiniz zaman açabilirsiniz." : "Bu adımın güncel sürümünde kendi doğru alıştırma yanıtınız ve gerçek işlem kanıtınız birlikte doğrulanmadı; ayrıntılı örnek açık kalır."} Bu bir yeterlilik onayı değildir.</p>}
    </details>
    {children?.(mode === "compact" || (mode === "adaptive" && adaptiveReady))}
  </div>;
}

export function GuidanceHelpSettings() {
  const { user } = useAuth(); const [open, setOpen] = useState(false);
  const processes = Object.entries(processTitles).filter(([key]) => guidanceProcessPath(key, user?.role));
  const [selected, setSelected] = useState("daily");
  if (!processes.length) return null;
  return <details className="ez-panel" onToggle={event => setOpen(event.currentTarget.open)}>
    <summary>Anlatım tercihimi düzenle</summary>
    {open && <>
      <label>Süreç<select value={selected} onChange={event => setSelected(event.target.value)}>
        {processes.map(([key, title]) => <option key={key} value={key}>{title}</option>)}
      </select></label>
      <GuidanceHelpControl key={`${user?.id}:${selected}`} process={selected} />
    </>}
  </details>;
}
