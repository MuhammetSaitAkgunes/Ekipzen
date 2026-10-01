import { guidanceOperationsKey } from "../guidance/operationQueries";
import { GuidanceOperationSource } from "../guidance/GuidanceOperationSource";
import { useEffect, useRef, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { ApiError, apiFetch } from "../../api/client";
import type { components } from "../../api/schema";
import { PageError } from "../shared/PageState";

type SetupStatus = components["schemas"]["TenantSetupStatusDto"];

export function TenantSetupPanel({ onInitialized }: { onInitialized?: () => void }) {
  const queryClient = useQueryClient();
  const actionInFlight = useRef(false);
  const [guidanceVersion, setGuidanceVersion] = useState<number | null>(null);
  const [reviewing, setReviewing] = useState(false);
  const [acknowledged, setAcknowledged] = useState(false);
  const status = useQuery({ queryKey: ["tenant-setup"], queryFn: () => apiFetch<SetupStatus>("/setup") });
  const initialize = useMutation({
    mutationFn: () => apiFetch<SetupStatus>("/setup", { method: "POST", ...(guidanceVersion === null ? {} : { body: JSON.stringify({ guidanceContentRevisionId: guidanceVersion }) }) }),
    onSuccess: async (data) => {
      queryClient.setQueryData(["tenant-setup"], data);
      await Promise.all(["settings", "payroll", "leaves"].map((key) =>
        queryClient.invalidateQueries({ queryKey: [key] })));
      await queryClient.invalidateQueries({ queryKey: guidanceOperationsKey });
      onInitialized?.();
    },
  });

  useEffect(() => {
    setReviewing(false);
    setAcknowledged(false);
  }, [status.data]);

  const confirmInitialize = async () => {
    if (!reviewing || !acknowledged || !status.data?.needsInitialization
      || initialize.isPending || actionInFlight.current) return;
    actionInFlight.current = true;
    try {
      await initialize.mutateAsync();
    } catch {
      // Mutation error is rendered below; keep review visible for retry.
    } finally {
      actionInFlight.current = false;
    }
  };

  if (status.isError) return <PageError error={status.error} />;
  if (status.isPending) return <p role="status">Başlangıç kayıtları kontrol ediliyor…</p>;
  const data = status.data;
  return (
    <section id="settings-setup" className="set-card" aria-label="Şirket başlangıç kurulumu">
      <h3>Şirket başlangıç kurulumu</h3>
      <p>{data.needsInitialization ? "Eksik başlangıç kayıtları var." : "Başlangıç kayıtları mevcut."}</p>
      <p>
        {data.templateYear} şablonu: şirket profili, izin türleri, tam gün tatiller ve onaysız bordro taslağı.
        Mevcut kayıtlar korunur; örnek personel veya demo verisi eklenmez.
      </p>
      {data.needsInitialization && (
        <>
          <p>Eksik izin türü: {data.missingLeaveTypeCount} · Eksik tam gün tatil: {data.missingFullDayHolidayCount}</p>
          {reviewing && <section className="ez-setup-review" role="region" aria-label="Başlangıç kayıtlarını oluşturmadan önce kontrol edin">
            <h4>Başlangıç kayıtlarını oluşturmadan önce kontrol edin</h4>
            <p><strong>Şirket profili:</strong> {data.companyProfileExists ? "Mevcut, korunacak" : "Kiracı adıyla oluşturulacak"}</p>
            <p><strong>İzin türleri:</strong> {data.missingLeaveTypeCount} eksik tür eklenecek. <strong>Tam gün tatiller:</strong> {data.missingFullDayHolidayCount} eksik tarih eklenecek.</p>
            <p><strong>Bordro ayarı:</strong> {data.payrollSettingsExists ? "Mevcut set korunacak" : "Onaysız taslak oluşturulacak"}.</p>
            <p>Bu işlem mevcut kayıtları değiştirmez. Şablon yılı {data.templateYear} için başlangıç verisidir; hukuki doğrulama veya bordro onayı değildir. Kurulumdan sonra izin türlerini, tatilleri ve bordro kurallarını şirketinizin gerçek durumuyla karşılaştırın.</p>
            <GuidanceOperationSource process="company" step="initial-setup" value={guidanceVersion} onChange={(id) => { setGuidanceVersion(id); setAcknowledged(false); }} disabled={initialize.isPending} />
            <label><input type="checkbox" checked={acknowledged} onChange={(event) => setAcknowledged(event.target.checked)} /> Eksik kayıtların oluşturulacağını ve uzman kontrolünün ayrıca gerektiğini anladım.</label>
          </section>}
          {reviewing
            ? <div className="ez-setup-actions">
              <button className="btn btn-ghost" disabled={initialize.isPending} onClick={() => { setReviewing(false); setAcknowledged(false); }}>Geri dön</button>
              <button className="btn btn-primary" disabled={!acknowledged || initialize.isPending} onClick={() => void confirmInitialize()}>
                {initialize.isPending ? "Kurulum yapılıyor…" : "Kontrol ettim, kayıtları oluştur"}
              </button>
            </div>
            : <button className="btn btn-primary" disabled={initialize.isPending} onClick={() => setReviewing(true)}>
              Kurulumu gözden geçir
            </button>}
        </>
      )}
      {initialize.isError && <p role="alert">{initialize.error instanceof ApiError
        ? initialize.error.message : "Kurulum tamamlanamadı. Tekrar deneyebilirsiniz."}</p>}
      {!data.hasEffectiveApprovedPayrollSettings && <p>Yürürlükte onaylı bordro seti yok. Bordro ayarlarında uzman incelemesini tamamlayın.</p>}
      <p>Bu kontrol pilot onayı değildir. Yarım gün tatiller, yıllık izin hakları ve işyeri çalışma düzeni ayrıca doğrulanmalıdır.</p>
    </section>
  );
}
