import { Link } from "react-router-dom";
import { appPath } from "../../paths";
import { PageError, PageLoading } from "../shared/PageState";
import { BackToRisk } from "./BackToRisk";
import { getLevelText } from "./format";
import { useBurnoutDetail } from "./queries";

export function BurnoutDetailPage() {
  const detail = useBurnoutDetail();
  if (detail.isPending) return <PageLoading />;
  if (detail.isError) return <PageError error={detail.error} />;

  const data = detail.data;
  const employees = [...(data.employees ?? [])].sort(
    (a, b) => (b.overtime ?? 0) + (b.unusedLeave ?? 0) - ((a.overtime ?? 0) + (a.unusedLeave ?? 0)),
  );

  return (
    <div className="detail-page">
      <div className="page-header">
        <div>
          <h2>Tükenmişlik Sinyali</h2>
        </div>
        <BackToRisk />
      </div>
      <div className="detail-kpi-grid">
        <div className="stat-box"><span className="sb-label">Yüksek Sinyal</span><strong className="sb-val text-red">{data.highCount ?? 0}</strong></div>
        <div className="stat-box"><span className="sb-label">Fazla Mesai Ort.</span><strong className="sb-val">{data.averageOvertime ?? 0}<small>%</small></strong></div>
        <div className="stat-box"><span className="sb-label">Kullanılmayan İzin</span><strong className="sb-val">{data.averageUnusedLeave ?? 0}<small>%</small></strong></div>
        <div className="stat-box"><span className="sb-label">Orta Sinyal</span><strong className="sb-val">{data.mediumCount ?? 0}</strong></div>
      </div>
      <div className="table-container">
        <table className="detail-table data-table">
          <thead>
            <tr><th>Personel</th><th>Departman</th><th>Fazla Mesai</th><th>Kullanılmayan İzin</th><th>Nabız</th><th>Seviye</th><th>Önerilen Aksiyon</th></tr>
          </thead>
          <tbody>
            {employees.length === 0 && <tr><td colSpan={7}>Bu kapsamda çalışan kaydı yok.</td></tr>}
            {employees.map((employee) => (
              <tr key={employee.employeeId}>
                <td>{employee.employeeId != null && employee.employeeId > 0 ? <Link className="ez-record-link" to={appPath(`/personnel/${employee.employeeId}`)}>{employee.name}</Link> : <strong>{employee.name}</strong>}<small>{employee.title}</small></td>
                <td>{employee.dept}</td>
                <td>{employee.overtime}%</td>
                <td>{employee.unusedLeave}%</td>
                <td>{employee.pulse}%</td>
                <td><span className={`risk-badge ${employee.burnoutRisk ?? ""}`}>{getLevelText(employee.burnoutRisk)}</span></td>
                <td>{employee.action}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
