import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { ApiError, apiFetch } from "../../api/client";
import type { components } from "../../api/schema";
import { PageError } from "../shared/PageState";

type SetupStatus = components["schemas"]["TenantSetupStatusDto"];

export function TenantSetupPanel({ onInitialized }: { onInitialized?: () => void }) {
  const queryClient = useQueryClient();
  const status = useQuery({ queryKey: ["tenant-setup"], queryFn: () => apiFetch<SetupStatus>("/setup") });
  const initialize = useMutation({
    mutationFn: () => apiFetch<SetupStatus>("/setup", { method: "POST" }),
    onSuccess: async (data) => {
      queryClient.setQueryData(["tenant-setup"], data);
      await Promise.all(["settings", "payroll", "leaves"].map((key) =>
        queryClient.invalidateQueries({ queryKey: [key] })));
      onInitialized?.();
    },
  });

  if (status.isError) return <PageError error={status.error} />;
  if (status.isPending) return <p role="status">Başlangıç kayıtları kontrol ediliyor…</p>;
  const data = status.data;
  return (
    <section className="set-card" aria-label="Şirket başlangıç kurulumu">
      <h3>Şirket başlangıç kurulumu</h3>
      <p>{data.needsInitialization ? "Eksik başlangıç kayıtları var." : "Başlangıç kayıtları mevcut."}</p>
      <p>
        {data.templateYear} şablonu: şirket profili, izin türleri, tam gün tatiller ve onaysız bordro taslağı.
        Mevcut kayıtlar korunur; örnek personel veya demo verisi eklenmez.
      </p>
      {data.needsInitialization && (
        <>
          <p>Eksik izin türü: {data.missingLeaveTypeCount} · Eksik tam gün tatil: {data.missingFullDayHolidayCount}</p>
          <button className="btn btn-primary" disabled={initialize.isPending} onClick={() => initialize.mutate()}>
            {initialize.isPending ? "Kurulum yapılıyor…" : "Eksik başlangıç kayıtlarını oluştur"}
          </button>
        </>
      )}
      {initialize.isError && <p role="alert">{initialize.error instanceof ApiError
        ? initialize.error.message : "Kurulum tamamlanamadı. Tekrar deneyebilirsiniz."}</p>}
      {!data.hasEffectiveApprovedPayrollSettings && <p>Yürürlükte onaylı bordro seti yok. Bordro ayarlarında uzman incelemesini tamamlayın.</p>}
      <p>Bu kontrol pilot onayı değildir. Yarım gün tatiller, yıllık izin hakları ve işyeri çalışma düzeni ayrıca doğrulanmalıdır.</p>
    </section>
  );
}
