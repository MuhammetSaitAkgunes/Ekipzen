import { ApiError, apiDownload } from "../../api/client";
import { useToast } from "../../layout/ToastProvider";
import { PageError, PageLoading } from "../shared/PageState";
import { formatPayrollMoney, payrollStatusClass } from "./format";
import { useMyPayslips } from "./queries";

export function MyPayslipsView() {
  const { showToast } = useToast();
  const slipsQ = useMyPayslips();

  if (slipsQ.isPending) return <PageLoading />;
  if (slipsQ.isError) return <PageError error={slipsQ.error} />;

  const slips = slipsQ.data;

  const handleDownload = async (periodId: number | undefined, rowId: number | undefined,
    periodName: string, original = false) => {
    if (periodId === undefined || rowId === undefined) return;
    try {
      const { blob, fileName } = await apiDownload(
        `/payroll/periods/${periodId}/rows/${rowId}/payslip${original ? "?original=true" : ""}`);
      const link = document.createElement("a");
      link.href = URL.createObjectURL(blob);
      link.download = fileName ?? `bordro-${periodName}.pdf`;
      document.body.appendChild(link);
      link.click();
      link.remove();
      URL.revokeObjectURL(link.href);
      showToast(original ? "İlk onaylı pusula indirildi." : "Bordro pusulası indirildi.", "success");
    } catch (e) {
      showToast(e instanceof ApiError ? e.message : "Pusula indirilemedi.", "error");
    }
  };

  return (
    <div id="payroll-screen" className="ez-my-payslips">
      <div className="page-header">
        <div>
          <h1>Bordrolarım</h1>
          <p>Onaylı pusulalarınız · {slips.length} dönem</p>
        </div>
      </div>

      <ul className="ez-payslip-list" aria-label="Bordro dönemleri">
        {slips.map((slip) => (
          <li key={slip.rowId} className="ez-payslip-card">
            <div className="ez-payslip-period">
              <h2>{slip.periodName}</h2>
              <span className={`status-pill ${payrollStatusClass(slip.approvalStatus)}`}>
                {slip.approvalStatus}
              </span>
            </div>
            <dl>
              <div><dt>Brüt</dt><dd>{formatPayrollMoney(slip.grossEarnings)}</dd></div>
              <div><dt>Kesinti</dt><dd>{formatPayrollMoney(slip.totalDeductions)}</dd></div>
              <div className="ez-payslip-net"><dt>Net tutar</dt><dd>{formatPayrollMoney(slip.netPay)}</dd></div>
            </dl>
            {slip.hasRevision && <p className="ez-payslip-revision">Geçerli düzeltme sürümü · dış ödeme ve bildirim bu ekranda doğrulanmaz</p>}
            <div className="ez-payslip-downloads">
              <button
                className="btn btn-secondary btn-sm"
                title="Pusulayı indir"
                aria-label={`${slip.periodName} bordro pusulasını indir`}
                onClick={() => handleDownload(slip.periodId, slip.rowId, slip.periodName ?? "")}
              >
                <i aria-hidden="true" className="fa-solid fa-file-pdf" /> PDF
              </button>
              {slip.hasRevision && <button className="btn btn-secondary btn-sm"
                aria-label={`${slip.periodName} ilk onaylı bordro pusulasını indir`}
                onClick={() => handleDownload(slip.periodId, slip.rowId, slip.periodName ?? "", true)}>
                İlk onay PDF
              </button>}
            </div>
          </li>
        ))}
      </ul>
      {slips.length === 0 && <section className="ez-panel ez-guide-empty"><i className="fa-regular fa-file-lines" aria-hidden="true" /><h2>Henüz bordro kaydınız yok.</h2><p>Onaylı pusulalarınız burada listelenecek.</p></section>}
    </div>
  );
}
