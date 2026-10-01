import { useState } from "react";
import { useAuth } from "../../auth/AuthContext";
import { GuidanceStepExplanation } from "./GuidanceStepExplanation";
import { useGuidanceOperationSources } from "./operationQueries";

export function GuidanceOperationSource({ process, step, value, onChange, disabled = false, label = "Gerçek işlemde kullandığım rehber sürümü" }: {
  process: string; step: string; value: number | null; onChange: (id: number | null) => void; disabled?: boolean; label?: string;
}) {
  const { user } = useAuth(); const [open, setOpen] = useState(false);
  const query = useGuidanceOperationSources(process, user?.id, open);
  const current = query.data?.find(x => x.isCurrent && x.contentKey === `${process}-${step}` && x.body.stepKey === step);
  return <details className="ez-personnel-decision-check" onToggle={event => setOpen(event.currentTarget.open)}>
    <summary>{label}{value !== null ? " · seçildi" : ""}</summary>
    {open && <>
      <p>Bu işlemde kullandığınız açıklamayı açıp sürümünü seçebilirsiniz. İşlem sunucuda kaydedilirse yalnız sizin işlem kanıtınıza bağlanır; okuma, alıştırma veya yeterlilik onayı oluşturmaz.</p>
      {query.isPending ? <p>İşleme ait güncel rehber yükleniyor.</p> : query.isError ? <p role="alert">{query.error.message}</p>
        : !current ? <p>Bu işlem için güncel, incelenmiş adım yayımlanmamış. Rehber bağlantısı olmadan mevcut kontrollerle çalışabilirsiniz.</p>
          : <><p>{current.body.title} · Sürüm {current.contentVersion}</p>
            <button type="button" disabled={disabled} onClick={() => onChange(current.id)}>Bu rehber sürümünü işlemime bağla</button></>}
      <button type="button" disabled={disabled || query.isFetching} onClick={() => void query.refetch()}>İşlem rehberini yeniden yükle</button>
      {value !== null && <>
        <p role="status">Seçilen sürüm açıklaması aşağıdadır. Yeni yayın bu seçimi otomatik değiştirmez. İşlemde eski sürüm reddedilirse güncel sürümü açıkça seçin.</p>
        <GuidanceStepExplanation key={value} id={value} />
        <button type="button" disabled={disabled} onClick={() => onChange(null)}>Rehber sürümü bağlantısını kaldır</button>
      </>}
    </>}
  </details>;
}
