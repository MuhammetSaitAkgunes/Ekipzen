import { useState } from "react";
import { GuidanceSourceLink } from "../guidance/GuidanceSourceLink";
import { GuidanceStepLessons } from "../guidance/GuidanceStepLessons";
import { useNavigate, useSearchParams } from "react-router-dom";
import { ApiError } from "../../api/client";
import { appPath } from "../../paths";
import { useAuth } from "../../auth/AuthContext";
import { useToast } from "../../layout/ToastProvider";
import { PageError, PageLoading } from "../shared/PageState";
import { CalculatorTab } from "./CalculatorTab";
import { MyPayslipsView } from "./MyPayslipsView";
import { PeriodTab } from "./PeriodTab";
import { SettingsTab } from "./SettingsTab";
import { usePayrollPeriod, usePayrollPeriods, useSubmitPayrollPeriod } from "./queries";
import { CreatePeriodModal, SubmitPeriodModal } from "./PayrollPeriodGuidance";

const TABS = [
  { tab: "period", path: appPath("/payroll"), icon: "fa-table-list", label: "Dönem Bordrosu" },
  { tab: "calculator", path: appPath("/payroll/calculator"), icon: "fa-calculator", label: "Tekil Hesaplama" },
  { tab: "settings", path: appPath("/payroll/settings"), icon: "fa-sliders", label: "Bordro Ayarları" },
] as const;

export function PayrollPage({ tab }: { tab: "period" | "calculator" | "settings" }) {
  const { user } = useAuth();
  const isAdmin = user?.role === "hr-admin";
  if (!isAdmin) return <MyPayslipsView />;
  return <PayrollAdminShell tab={tab} />;
}

function PayrollAdminShell({ tab }: { tab: "period" | "calculator" | "settings" }) {
  const navigate = useNavigate();
  const [searchParams, setSearchParams] = useSearchParams();
  const { showToast } = useToast();
  const periodsQ = usePayrollPeriods(true);
  const submitPeriod = useSubmitPayrollPeriod();
  const [createOpen, setCreateOpen] = useState(false);
  const [submitOpen, setSubmitOpen] = useState(false);
  const periods = periodsQ.data ?? [];
  const rawPeriodId = searchParams.get("periodId");
  const requestedPeriodId = rawPeriodId && /^[1-9]\d*$/.test(rawPeriodId) ? Number(rawPeriodId) : null;
  const periodId = periods.find((period) => period.id === requestedPeriodId)?.id ?? periods[0]?.id ?? null;
  const periodDetailQ = usePayrollPeriod(tab === "period" ? periodId : null);

  if (periodsQ.isPending) return <PageLoading />;
  if (periodsQ.isError) return <PageError error={periodsQ.error} />;

  const selected = periods.find((p) => p.id === periodId) ?? null;

  const selectedDetail = periodDetailQ.data?.id === periodId ? periodDetailQ.data : null;
  const canSubmit = selected?.status === "draft" || selected?.status === "control";

  const handleSubmitPeriod = async () => {
    if (periodId === null || !canSubmit) return;
    try {
      await submitPeriod.mutateAsync(periodId);
      setSubmitOpen(false);
      showToast("Dönem bordrosu onaylandı. Bu işlem ödeme veya dış bildirim yapmaz.", "success");
    } catch (e) {
      showToast(e instanceof ApiError ? e.message : "Dönem onaylanamadı.", "error");
    }
  };

  return (
    <div id="payroll-screen">
      <div className="page-header">
        <div>
          <h2>Bordro</h2>
          <GuidanceSourceLink processKey="payroll" />
          <p>
            {selected
              ? `${selected.name} dönemi için hesaplama, kontrol, onay ve tekil bordro senaryolarını yönetin.`
              : "Bordro dönemi oluşturarak hesaplama, kontrol ve onay akışını başlatın."}
          </p>
        </div>
        <div className="header-actions">
          {tab === "period" && (
            <>
              <label className="sr-only" htmlFor="payroll-period-select">Bordro dönemi</label>
              <select
                id="payroll-period-select"
                className="small-select"
                value={periodId ?? ""}
                onChange={(e) => { setSearchParams({ periodId: e.target.value }, { replace: true }); setSubmitOpen(false); }}
              >
                {periods.map((p) => (
                  <option key={p.id} value={p.id}>{p.name}</option>
                ))}
              </select>
              <button className="btn btn-secondary" onClick={() => setCreateOpen(true)}>
                <i aria-hidden="true" className="fa-solid fa-plus" /> Yeni Dönem
              </button>
            </>
          )}
          <button className="btn btn-secondary" onClick={() => navigate(appPath("/payroll/calculator"))}>
            <i aria-hidden="true" className="fa-solid fa-calculator" /> Tekil hesapla
          </button>
          {tab === "period" && canSubmit && (
            <button className="btn btn-primary" onClick={() => setSubmitOpen(true)} disabled={!selectedDetail || submitPeriod.isPending}>
              <i aria-hidden="true" className="fa-solid fa-paper-plane" /> Dönemi onayla
            </button>
          )}
        </div>
      </div>

      <GuidanceStepLessons process="payroll" />
      <div className="payroll-tabs">
        {TABS.map((item) => (
          <button
            key={item.tab}
            className={`payroll-tab ${tab === item.tab ? "active" : ""}`}
            onClick={() => navigate(item.path)}
          >
            <i aria-hidden="true" className={`fa-solid ${item.icon}`} /> {item.label}
          </button>
        ))}
      </div>

      {tab === "period" && (
        <section id="payroll-period" className="payroll-tab-content active">
          <PeriodTab periodId={periodId} />
        </section>
      )}
      {tab === "calculator" && (
        <section id="payroll-calculator" className="payroll-tab-content active"><CalculatorTab /></section>
      )}
      {tab === "settings" && (
        <section id="payroll-settings" className="payroll-tab-content active"><SettingsTab /></section>
      )}

      {createOpen && (
        <CreatePeriodModal latest={periods[0] ?? null} onClose={() => setCreateOpen(false)} onCreated={(id) => setSearchParams({ periodId: String(id) }, { replace: true })} />
      )}
      {submitOpen && selectedDetail && (
        <SubmitPeriodModal detail={selectedDetail} pending={submitPeriod.isPending} onClose={() => setSubmitOpen(false)} onSubmit={handleSubmitPeriod} />
      )}
    </div>
  );
}

export const PayrollPeriodPage = () => <PayrollPage tab="period" />;
export const PayrollCalculatorPage = () => <PayrollPage tab="calculator" />;
export const PayrollSettingsPage = () => <PayrollPage tab="settings" />;
