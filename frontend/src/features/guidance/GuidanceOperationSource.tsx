import { useId, useLayoutEffect, useRef, useState, type ReactNode } from "react";
import { useAuth } from "../../auth/AuthContext";
import { Button } from "../../ui";
import { Link } from "react-router-dom";
import { appPath } from "../../paths";
import { GuidanceStepExplanation, GuidanceStepSummary } from "./GuidanceStepExplanation";
import { useGuidanceOperationSources } from "./operationQueries";
import { processTitles } from "./catalogQueries";
import { guidanceStepTitles } from "./stepDefinitions";
import { useGuidanceWorkspace } from "./GuidanceWorkspace";

export function GuidanceOperationSource({ process, step, value, onChange, disabled = false, fallback, label = "Gerçek işlemde kullandığım rehber sürümü" }: {
  process: string; step: string; value: number | null; onChange: (id: number | null) => void; disabled?: boolean; label?: string; fallback?: ReactNode;
}) {
  const { user } = useAuth(); const [localOpen, setOpen] = useState(true); const id = useId();
  const ref = useRef<HTMLElement>(null); const [visible, setVisible] = useState(true);
  const workspace = useGuidanceWorkspace(); const register = workspace?.register;
  useLayoutEffect(() => {
    if (!register) return;
    let element: HTMLElement | null = ref.current; let shown = !!element;
    while (element && shown) {
      if (element.hidden || getComputedStyle(element).display === "none" || (element.tagName === "DETAILS" && !(element as HTMLDetailsElement).open)) shown = false;
      element = element.parentElement;
    }
    setVisible(shown);
  });
  useLayoutEffect(() => {
    if (!user?.id || !visible || !register) return;
    let element: HTMLElement | null = ref.current; let priority = 0;
    while (element) {
      if (element.matches('[role="dialog"],.modal-overlay,.fullscreen-modal')) priority += 100;
      if (element.classList.contains('ez-workbench')) priority--;
      element = element.parentElement;
    }
    return register(id, priority);
  }, [register, id, user?.id, visible]);
  const open = workspace ? workspace.active === id : localOpen;
  const query = useGuidanceOperationSources(process, user?.id, true);
  if (!user?.id) return fallback ?? null;
  const current = query.data?.find(x => x.isCurrent && x.contentKey === `${process}-${step}` && x.body.stepKey === step);
  const shown = value ?? current?.id;
  return <aside ref={ref} className="ez-context-guide" data-guide-id={id} aria-label={label}>
    <div className="ez-context-guide-heading"><div><span className="ez-overline">İK REHBERİ · {processTitles[process]}</span>
      <h3>{guidanceStepTitles[process]?.[step] ?? "İşlem rehberi"}</h3></div>
      <Button variant="ghost" size="sm" aria-expanded={open} aria-controls={id} onClick={() => workspace ? workspace.activate(open ? null : id) : setOpen(x => !x)}>{open ? "Küçült" : "Rehberi aç"}</Button></div>
    {value !== null && query.isSuccess && current?.id !== value && <p className="ez-guide-warning" role="status">Bu işleme bağlı rehber güncel yayınla aynı değil. Seçiminiz korunuyor; güncel sürümü kullanmadan önce kontrol edin.</p>}
    {!open && shown !== undefined && <GuidanceStepSummary id={shown} />}
    {!open && query.isError && <p className="ez-guide-warning" role="status">Rehber doğrulanamadı. Açıklamayı yeniden yüklemek için rehberi açın.</p>}
    <div id={id} hidden={!open}>
      {query.isPending ? <p role="status">İşleme ait güncel rehber yükleniyor.</p> : query.isError ? <div role="alert"><p>Rehber yüklenemedi. Formdaki bilgileriniz korunuyor.</p><Button variant="secondary" size="sm" onClick={() => void query.refetch()}>İşlem rehberini yeniden yükle</Button></div>
        : !current && value === null ? <div className="ez-guide-empty"><strong>İncelenmiş içerik bekleniyor</strong><p>Bu işlem için güncel, incelenmiş adım yayımlanmamış. Rehber bağlantısı olmadan mevcut kontrollerle çalışabilirsiniz.</p></div> : null}
      {!current && value === null && fallback}
      {shown !== undefined && <GuidanceStepExplanation key={shown} id={shown} />}
      {shown !== undefined && <Link className="ez-guide-reader-link" to={appPath(`/guidance/catalog?version=${shown}`)} target="_blank" rel="noopener noreferrer">Tam açıklamayı ayrı sekmede aç ↗</Link>}
      {current && <div className="ez-guide-binding"><p>{value === current.id ? "Seçtiğiniz rehber bu kayda bağlanacak." : "Bu açıklamayı işlem kaydına bağlamak için açıkça seçin."}</p>
        <Button variant="secondary" size="sm" disabled={disabled || value === current.id} onClick={() => onChange(current.id)}>{value === current.id ? "Rehber seçildi" : "Bu rehber sürümünü işlemime bağla"}</Button></div>}
      <details className="ez-guide-metadata"><summary>{label}{value !== null ? " · seçildi" : ""}</summary>
        {current && <p>{current.body.title} · Sürüm {current.contentVersion}</p>}
        <p>Yeni yayın seçiminizi otomatik değiştirmez. Eski sürüm reddedilirse güncel sürümü açıkça seçin.</p>
        <Button variant="secondary" size="sm" disabled={disabled || query.isFetching} onClick={() => void query.refetch()}>İşlem rehberini yeniden yükle</Button>
        {value !== null && <Button variant="ghost" size="sm" disabled={disabled} onClick={() => onChange(null)}>Rehber sürümü bağlantısını kaldır</Button>}
      </details>
      <p className="ez-guide-footnote">Rehberi açmak kayıt oluşturmaz. İşlem kanıtı, gerçek işlem sunucuda kaydedildiğinde oluşur.</p>
    </div>
  </aside>;
}
