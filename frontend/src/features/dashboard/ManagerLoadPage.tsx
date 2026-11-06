import { PageError, PageLoading } from "../shared/PageState";
import { BackToRisk } from "./BackToRisk";
import { getRiskLevel } from "./format";
import { useManagerLoad } from "./queries";

export function ManagerLoadPage() {
  const load = useManagerLoad();
  if (load.isPending) return <PageLoading />;
  if (load.isError) return <PageError error={load.error} />;

  const data = load.data;

  return (
    <div className="detail-page">
      <div className="page-header">
        <div>
          <h2>Yönetici Yükü Detayı</h2>
        </div>
        <BackToRisk />
      </div>
      <div className="detail-kpi-grid">
        <div className="stat-box"><span className="sb-label">Yük Endeksi</span><strong className="sb-val text-orange">{data.managerLoadIndex ?? 0}<small>/100</small></strong></div>
        <div className="stat-box"><span className="sb-label">Kritik Yönetici</span><strong className="sb-val">{data.criticalManagerCount ?? 0}</strong></div>
        <div className="stat-box"><span className="sb-label">Bekleyen Onay</span><strong className="sb-val">{data.pendingApprovals ?? 0}</strong></div>
        <div className="stat-box"><span className="sb-label">Açık Aksiyon</span><strong className="sb-val">{data.openActions ?? 0}</strong></div>
      </div>
      <div className="table-container">
        <table className="detail-table data-table">
          <thead>
            <tr><th>Yönetici</th><th>Ekip</th><th>Onay</th><th>Aksiyon</th><th>Fazla Mesai</th><th>Ekip Nabzı</th><th>Yük</th><th>Öneri</th></tr>
          </thead>
          <tbody>
            {!data.managers?.length && <tr><td colSpan={8}>Bu kapsamda yönetici kaydı yok.</td></tr>}
            {(data.managers ?? []).map((manager) => (
              <tr key={manager.employeeId}>
                <td><strong>{manager.name}</strong><small>{(manager.load ?? 0) > 70 ? "Kritik takip" : "Normal takip"}</small></td>
                <td>{manager.team} kişi</td>
                <td>{manager.approvals}</td>
                <td>{manager.actions}</td>
                <td>{manager.overtime}%</td>
                <td>{manager.pulse}%</td>
                <td><span className={`risk-badge ${getRiskLevel(manager.load ?? 0)}`}>{manager.load}/100</span></td>
                <td>{(manager.load ?? 0) > 70 ? "Aksiyon devri ve kapasite görüşmesi" : "Haftalık takip yeterli"}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
