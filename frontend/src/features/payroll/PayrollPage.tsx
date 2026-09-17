import { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import { ApiError } from "../../api/client";
import { appPath } from "../../paths";
import { useAuth } from "../../auth/AuthContext";
import { useToast } from "../../layout/ToastProvider";
import { PageError, PageLoading } from "../shared/PageState";
import { CalculatorTab } from "./CalculatorTab";
import { MyPayslipsView } from "./MyPayslipsView";
import { PeriodTab } from "./PeriodTab";
import { SettingsTab } from "./SettingsTab";
import { useCreatePayrollPeriod, usePayrollPeriods, useSubmitPayrollPeriod } from "./queries";

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
  const { showToast } = useToast();
  const periodsQ = usePayrollPeriods(true);
  const submitPeriod = useSubmitPayrollPeriod();
  const [periodId, setPeriodId] = useState<number | null>(null);
  const [createOpen, setCreateOpen] = useState(false);

  useEffect(() => {
    if (periodId === null && (periodsQ.data ?? []).length > 0) {
      setPeriodId(periodsQ.data![0].id ?? null);
    }
  }, [periodsQ.data, periodId]);

  if (periodsQ.isPending) return <PageLoading />;
  if (periodsQ.isError) return <PageError error={periodsQ.error} />;

  const periods = periodsQ.data;
  const selected = periods.find((p) => p.id === periodId) ?? null;

  const handleSubmitPeriod = async () => {
    if (periodId === null) {
      showToast("Önce bir bordro dönemi seçin.", "warning");
      return;
    }
    try {
      await submitPeriod.mutateAsync(periodId);
      showToast("Dönem bordrosu onaylandı ve kapatıldı.", "success");
    } catch (e) {
      showToast(e instanceof ApiError ? e.message : "Dönem gönderilemedi.", "error");
    }
  };

  return (
    <div id="payroll-screen">
      <div className="page-header">
        <div>
          <h2>Bordro</h2>
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
                onChange={(e) => setPeriodId(Number(e.target.value))}
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
          <button className="btn btn-primary" onClick={handleSubmitPeriod} disabled={submitPeriod.isPending}>
            <i aria-hidden="true" className="fa-solid fa-paper-plane" /> Onaya gönder
          </button>
        </div>
      </div>

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
        <CreatePeriodModal onClose={() => setCreateOpen(false)} onCreated={(id) => setPeriodId(id)} />
      )}
    </div>
  );
}

function CreatePeriodModal({ onClose, onCreated }: { onClose: () => void; onCreated: (id: number) => void }) {
  const { showToast } = useToast();
  const createPeriod = useCreatePayrollPeriod();
  const now = new Date();
  const [year, setYear] = useState(String(now.getFullYear()));
  const [month, setMonth] = useState(String(now.getMonth() + 1));
  const [error, setError] = useState<string | null>(null);

  const submit = async () => {
    setError(null);
    try {
      const period = await createPeriod.mutateAsync({ year: Number(year), month: Number(month) });
      showToast(`${period.name} bordro dönemi oluşturuldu.`, "success");
      if (period.id !== undefined) onCreated(period.id);
      onClose();
    } catch (e) {
      setError(e instanceof ApiError ? e.message : "Dönem oluşturulamadı.");
    }
  };

  return (
    <div className="modal-overlay" style={{ display: "flex" }}>
      <div className="modal-card scale-in">
        <div className="modal-head">
          <div>
            <h3>Yeni Bordro Dönemi</h3>
            <p>Aktif personel için girdi satırları otomatik oluşturulur.</p>
          </div>
          <button className="btn-icon-sm" onClick={onClose} title="Kapat" aria-label="Dönem penceresini kapat">
            <i aria-hidden="true" className="fa-solid fa-xmark" />
          </button>
        </div>
        <div className="modal-body-scroll">
          {error && <p className="form-error" role="alert">{error}</p>}
          <div className="form-grid-2">
            <div className="input-group">
              <label className="input-label" htmlFor="pp-year">Yıl</label>
              <input id="pp-year" type="number" className="input-control" value={year} onChange={(e) => setYear(e.target.value)} />
            </div>
            <div className="input-group">
              <label className="input-label" htmlFor="pp-month">Ay</label>
              <select id="pp-month" className="input-control" value={month} onChange={(e) => setMonth(e.target.value)}>
                {Array.from({ length: 12 }, (_, i) => (
                  <option key={i + 1} value={i + 1}>{i + 1}</option>
                ))}
              </select>
            </div>
          </div>
        </div>
        <div className="modal-footer">
          <button className="btn btn-ghost" onClick={onClose}>Vazgeç</button>
          <button className="btn btn-primary" onClick={submit} disabled={createPeriod.isPending}>
            <i aria-hidden="true" className="fa-solid fa-check" /> Oluştur
          </button>
        </div>
      </div>
    </div>
  );
}

export const PayrollPeriodPage = () => <PayrollPage tab="period" />;
export const PayrollCalculatorPage = () => <PayrollPage tab="calculator" />;
export const PayrollSettingsPage = () => <PayrollPage tab="settings" />;
