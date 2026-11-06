import { Link } from "react-router-dom";
import { appPath } from "../../paths";
import { PageError, PageLoading } from "../shared/PageState";
import { BackToRisk } from "./BackToRisk";
import { getLevelText } from "./format";
import { useAttritionDetail } from "./queries";

export function AttritionDetailPage() {
  const detail = useAttritionDetail();
  if (detail.isPending) return <PageLoading />;
  if (detail.isError) return <PageError error={detail.error} />;

  const data = detail.data;
  const employees = [...(data.employees ?? [])].sort((a, b) => (b.riskScore ?? 0) - (a.riskScore ?? 0));

  return (
    <div className="detail-page">
      <div className="page-header">
        <div>
          <h2>Ayrılma Riski Detayı</h2>
        </div>
        <BackToRisk />
      </div>
      <div className="detail-kpi-grid">
        <div className="stat-box"><span className="sb-label">Yüksek Risk</span><strong className="sb-val text-red">{data.highCount ?? 0}</strong></div>
        <div className="stat-box"><span className="sb-label">Orta Risk</span><strong className="sb-val text-orange">{data.mediumCount ?? 0}</strong></div>
        <div className="stat-box"><span className="sb-label">Kritik Rol Riski</span><strong className="sb-val">{data.criticalRoleCount ?? 0}</strong></div>
        <div className="stat-box"><span className="sb-label">Ortalama Nabız</span><strong className="sb-val">{data.averagePulse ?? 0}<small>%</small></strong></div>
      </div>
      <div className="table-container">
        <table className="detail-table data-table">
          <thead>
            <tr><th>Personel</th><th>Departman</th><th>Yönetici</th><th>Risk</th><th>Son Sinyal</th><th>Önerilen Aksiyon</th></tr>
          </thead>
          <tbody>
            {employees.length === 0 && <tr><td colSpan={6}>Bu kapsamda çalışan kaydı yok.</td></tr>}
            {employees.map((employee) => (
              <tr key={employee.employeeId}>
                <td>{employee.employeeId != null && employee.employeeId > 0 ? <Link className="ez-record-link" to={appPath(`/personnel/${employee.employeeId}`)}>{employee.name}</Link> : <strong>{employee.name}</strong>}<small>{employee.title}</small></td>
                <td>{employee.dept}</td>
                <td>{employee.manager}</td>
                <td><span className={`risk-badge ${employee.attritionRisk ?? ""}`}>{getLevelText(employee.attritionRisk)}</span></td>
                <td>{employee.trend}</td>
                <td>{employee.action}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
