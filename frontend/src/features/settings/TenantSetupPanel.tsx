import { dailyTasksKey } from "../guidance/queries";
import { guidanceOperationsKey } from "../guidance/operationQueries";
import { GuidanceOperationSource } from "../guidance/GuidanceOperationSource";
import { useEffect, useRef, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { ApiError, apiFetch } from "../../api/client";
import type { components } from "../../api/schema";
import { PageError } from "../shared/PageState";

type SetupPreview = { status: SetupStatus; token: string; companyName: string | null; leaveTypes: { code: string; name: string; deductsFromAnnualBalance: boolean; requiresApproval: boolean }[]; holidays: { date: string; name: string }[]; payrollDraft: { sourceReference: string; effectiveFrom: string; minWageGross: number } | null };

type SetupStatus = components["schemas"]["TenantSetupStatusDto"];

export function TenantSetupPanel({ onInitialized }: { onInitialized?: () => void }) {
  const queryClient = useQueryClient();
  const actionInFlight = useRef(false);
  const [guidanceVersion, setGuidanceVersion] = useState<number | null>(null);
  const [reviewing, setReviewing] = useState(false);
  const [acknowledged, setAcknowledged] = useState(false);
  const status = useQuery({ queryKey: ["tenant-setup"], queryFn: () => apiFetch<SetupStatus>("/setup") });
  const preview = useQuery({ queryKey: ["tenant-setup", "preview"], enabled: reviewing, queryFn: async () => {
    const data = await apiFetch<SetupPreview>("/setup/preview");
    if (!data.token || !Array.isArray(data.leaveTypes) || !Array.isArray(data.holidays) || !data.status) throw new Error("Kurulum önizlemesi doğrulanamadı.");
    return data;
  } });
  const initialize = useMutation({
    mutationFn: () => apiFetch<SetupStatus>("/setup", { method: "POST", body: JSON.stringify({ expectedPreviewToken: preview.data?.token, ...(guidanceVersion === null ? {} : { guidanceContentRevisionId: guidanceVersion }) }) }),
    onSuccess: async (data) => {
      queryClient.setQueryData(["tenant-setup"], data);
      await Promise.all(["settings", "payroll", "leaves"].map((key) =>
        queryClient.invalidateQueries({ queryKey: [key] })));
      await queryClient.invalidateQueries({ queryKey: guidanceOperationsKey });
      await queryClient.invalidateQueries({ queryKey: dailyTasksKey });
      onInitialized?.();
    },
  });

  useEffect(() => { setAcknowledged(false); }, [preview.data]);

  useEffect(() => {
    setReviewing(false);
    setAcknowledged(false);
  }, [status.data]);

  const confirmInitialize = async () => {
    if (!reviewing || !acknowledged || !preview.isSuccess || preview.isFetching || !status.data?.needsInitialization
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
  const reviewed = preview.data?.status ?? data;
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
          {reviewing && <section className="ez-setup-review ez-workbench" role="region" aria-label="Başlangıç kayıtlarını oluşturmadan önce kontrol edin">
          <div className="ez-workbench-layout">
<div className="ez-workbench-guide" role="group" aria-label="İşlem açıklamaları" tabIndex={0}><GuidanceOperationSource process="company" step="initial-setup" value={guidanceVersion} onChange={(id) => { setGuidanceVersion(id); setAcknowledged(false); }} disabled={initialize.isPending} /></div>
          <div className="ez-workbench-main">

            <h4>Başlangıç kayıtlarını oluşturmadan önce kontrol edin</h4>
            {preview.isPending && <p>Tek tek eklenecek kayıtlar yükleniyor.</p>}
            {preview.isError && <p role="alert">Önizleme yüklenemedi. <button type="button" onClick={() => { setAcknowledged(false); void preview.refetch(); }}>Önizlemeyi tekrar yükle</button></p>}
            <button type="button" disabled={preview.isFetching || initialize.isPending} onClick={() => { setAcknowledged(false); void preview.refetch(); }}>Önizlemeyi yenile</button>
            {preview.isSuccess && <div>
              {preview.data.companyName && <p>Oluşturulacak şirket adı: {preview.data.companyName}</p>}
              <ul>{preview.data.leaveTypes.map(type => <li key={type.code}>{type.name} ({type.code}) · {type.deductsFromAnnualBalance ? "Yıllık bakiyeden düşer" : "Yıllık bakiyeden düşmez"} · {type.requiresApproval ? "Onay gerekir" : "Onay gerektirmez"}</li>)}</ul>
              <ul>{preview.data.holidays.map(day => <li key={day.date}>{day.date} · {day.name}</li>)}</ul>
              {preview.data.payrollDraft && <p>Eklenecek onaysız bordro taslağı: yürürlük {preview.data.payrollDraft.effectiveFrom}, brüt alt tutar {preview.data.payrollDraft.minWageGross}. Kaynak: {preview.data.payrollDraft.sourceReference}. Değerler uzman incelemesi gerektirir.</p>}
            </div>}
            <p><strong>Şirket profili:</strong> {reviewed.companyProfileExists ? "Mevcut, korunacak" : "Kiracı adıyla oluşturulacak"}</p>
            <p><strong>İzin türleri:</strong> {reviewed.missingLeaveTypeCount} eksik tür eklenecek. <strong>Tam gün tatiller:</strong> {reviewed.missingFullDayHolidayCount} eksik tarih eklenecek.</p>
            <p><strong>Bordro ayarı:</strong> {reviewed.payrollSettingsExists ? "Mevcut set korunacak" : "Onaysız taslak oluşturulacak"}.</p>
            <p>Bu işlem mevcut kayıtları değiştirmez. Şablon yılı {data.templateYear} için başlangıç verisidir; hukuki doğrulama veya bordro onayı değildir. Kurulumdan sonra izin türlerini, tatilleri ve bordro kurallarını şirketinizin gerçek durumuyla karşılaştırın.</p>

            <label><input type="checkbox" checked={acknowledged} disabled={!preview.isSuccess || preview.isFetching} onChange={(event) => setAcknowledged(event.target.checked)} /> Eksik kayıtların oluşturulacağını ve uzman kontrolünün ayrıca gerektiğini anladım.</label>
          </div></div></section>}
          {reviewing
            ? <div className="ez-setup-actions">
              <button className="btn btn-ghost" disabled={initialize.isPending} onClick={() => { setReviewing(false); setAcknowledged(false); }}>Geri dön</button>
              <button className="btn btn-primary" disabled={!acknowledged || initialize.isPending || !preview.isSuccess || preview.isFetching} onClick={() => void confirmInitialize()}>
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
