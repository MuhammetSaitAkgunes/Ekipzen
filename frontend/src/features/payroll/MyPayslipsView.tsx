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
    <div id="payroll-screen">
      <div className="page-header">
        <div>
          <h2>Bordro</h2>
          <p>Onaylanan bordro pusulalarınızı görüntüleyin ve indirin.</p>
        </div>
      </div>

      <section className="table-container payroll-table-card">
        <div className="payroll-table-header">
          <div>
            <h4>Bordrolarım</h4>
            <p className="text-muted">Dönem bazında brüt, kesinti ve net ödeme özetiniz.</p>
          </div>
        </div>
        <table className="data-table payroll-table">
          <thead>
            <tr>
              <th>Dönem</th><th>Brüt</th><th>Kesinti</th><th>Net</th><th>Durum</th><th></th>
            </tr>
          </thead>
          <tbody>
            {slips.map((slip) => (
              <tr key={slip.rowId}>
                <td><strong>{slip.periodName}</strong>{slip.hasRevision && <small>Geçerli düzeltme sürümü</small>}</td>
                <td>{formatPayrollMoney(slip.grossEarnings)}</td>
                <td>{formatPayrollMoney(slip.totalDeductions)}</td>
                <td><strong>{formatPayrollMoney(slip.netPay)}</strong></td>
                <td>
                  <span className={`status-pill ${payrollStatusClass(slip.approvalStatus)}`}>
                    {slip.approvalStatus}
                  </span>
                </td>
                <td className="text-right">
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
                </td>
              </tr>
            ))}
          </tbody>
        </table>
        {slips.length === 0 && <p className="pending-desc">Henüz bordro kaydınız yok.</p>}
      </section>
    </div>
  );
}
