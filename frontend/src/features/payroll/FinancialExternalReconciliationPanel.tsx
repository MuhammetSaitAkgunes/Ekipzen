import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { ApiError, apiFetch } from "../../api/client";
import type { components } from "../../api/schema";
import { ExternalActionForm, type ExternalActionInput } from "../personnel/OffboardingExternalReconciliationPanel";
import { dailyTasksKey } from "../guidance/queries";
import { guidanceOperationsKey } from "../guidance/operationQueries";
import type { OffboardingExternalAction } from "../personnel/queries";

type Review = { originalTotal: number; canRecord: boolean; sourceToken: string;
  payment: OffboardingExternalAction | null; notification: OffboardingExternalAction | null;
  history: OffboardingExternalAction[] };
type Record = components["schemas"]["OffboardingExternalActionDto"];

export function FinancialExternalReconciliationPanel({ source, parentId, resourceId }: {
  source: "payroll" | "offboarding"; parentId: number; resourceId: number;
}) {
  const client = useQueryClient();
  const path = source === "payroll" ? `/payroll/periods/${parentId}/rows/${resourceId}/external-reconciliation`
    : `/employees/${parentId}/offboarding/${resourceId}/external-reconciliation`;
  const key = ["financial-external", source, parentId, resourceId];
  const query = useQuery({ queryKey: key, queryFn: () => apiFetch<Review>(path) });
  const mutation = useMutation({
    mutationFn: (input: ExternalActionInput) => apiFetch<Record>(path, { method: "POST",
      body: JSON.stringify({ ...input, confirmRecordedStatus: true, expectedSourceToken: query.data?.sourceToken }) }),
    onSuccess: () => Promise.all([client.invalidateQueries({ queryKey: key }),
      client.invalidateQueries({ queryKey: dailyTasksKey }), client.invalidateQueries({ queryKey: guidanceOperationsKey })]),
  });
  const data = query.data;
  return <section className="ez-compensation-review" aria-label="İlk hesabın dış işlem takibi">
    <h4>İlk hesabın dış ödeme ve bildirim kanıtları</h4>
    <p>Uygulama onayı ödeme veya resmî gönderim yapmaz. İki işlemin sonucunu ayrı belgelerle doğrulayın; hatalı kanıtı gerekçeli yeni kayıtla düzeltin.</p>
    {query.isPending && <p>Hesap kaynağı yükleniyor…</p>}
    {query.isError && <p role="alert">{query.error instanceof ApiError ? query.error.message : "Kaynak okunamadı."}</p>}
    <button type="button" className="btn btn-ghost" disabled={query.isFetching || mutation.isPending} onClick={() => void query.refetch()}>Güncel dış kanıtları yeniden incele</button>
    {data && <>
      <p>İlk hesap toplamı: {new Intl.NumberFormat("tr-TR", { style: "currency", currency: "TRY" }).format(data.originalTotal)}. Bu tutar gerçek ödemeyi kanıtlamaz.</p>
      {!data.canRecord && <p role="status">İlk hesap yeni dış kanıt kaydı için uygun değil. Dönem/dosya durumunu veya geçerli parasal düzeltme sürümünü inceleyin; eski kanıtlar korunur.</p>}
      <ExternalActionForm key={`payment-${data.payment?.id ?? 0}`} kind="payment" title="Dış ödeme" current={data.payment} canRecord={data.canRecord} pending={mutation.isPending} onRecord={mutation.mutateAsync} />
      <ExternalActionForm key={`notification-${data.notification?.id ?? 0}`} kind="notification" title="Dış bildirim" current={data.notification} canRecord={data.canRecord} pending={mutation.isPending} onRecord={mutation.mutateAsync} />
      <details><summary>Korunan kanıt geçmişi ({data.history.length})</summary><ul>{data.history.map(x => <li key={x.id}>{x.kind === "payment" ? "Ödeme" : "Bildirim"} · {x.outcome === "completed" ? "Kanıtlı tamamlandı" : x.outcome === "not-required" ? "İşlem gerekmiyor" : "Bekliyor"} · {x.responsibleName} · {x.evidenceReference || "Kanıt yok"} · {x.notes}</li>)}</ul></details>
    </>}
  </section>;
}
