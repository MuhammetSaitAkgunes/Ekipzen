import { PageError, PageLoading } from "../shared/PageState";
import { BackToRisk } from "./BackToRisk";
import { getLevelText } from "./format";
import { useEmployeeVoice } from "./queries";

export function EmployeeVoicePage() {
  const voice = useEmployeeVoice();
  if (voice.isPending) return <PageLoading />;
  if (voice.isError) return <PageError error={voice.error} />;

  const data = voice.data;
  const departments = data.departments ?? [];
  // Backend riskTeams sunmuyor; riskli ekipler level != low departmanlardan türetilir.
  const riskTeams = departments.filter((d) => d.level !== "low").slice(0, 3);

  return (
    <div className="detail-page">
      <div className="page-header">
        <div>
          <h2>Çalışan Sesleri / Nabız Analitiği</h2>
        </div>
        <BackToRisk />
      </div>

      <div className="detail-kpi-grid">
        <div className="stat-box"><span className="sb-label">Nabız Skoru</span><strong className="sb-val text-orange">{data.pulseScore ?? 0}<small>/100</small></strong></div>
        <div className="stat-box"><span className="sb-label">eNPS</span><strong className="sb-val">{(data.eNps ?? 0) >= 0 ? "+" : ""}{data.eNps ?? 0}</strong></div>
        <div className="stat-box"><span className="sb-label">Katılım Oranı</span><strong className="sb-val">{data.participationRate ?? 0}<small>%</small></strong></div>
        <div className="stat-box"><span className="sb-label">Düşen Takım</span><strong className="sb-val text-red">{data.decliningTeams ?? 0}</strong></div>
      </div>

      <div className="voice-layout">
        <div className="table-container">
          <table className="detail-table data-table">
            <thead>
              <tr><th>Departman</th><th>Ruh Hali</th><th>Nabız</th><th>eNPS</th><th>Katılım</th><th>Öne Çıkan Sinyal</th><th>Seviye</th></tr>
            </thead>
            <tbody>
            {departments.length === 0 && <tr><td colSpan={7}>Bu kapsamda departman kaydı yok.</td></tr>}
              {departments.map((department) => (
                <tr key={department.departmentId}>
                  <td><strong>{department.dept}</strong></td>
                  <td>{department.mood}</td>
                  <td>{department.pulse}/100</td>
                  <td>{(department.eNps ?? 0) > 0 ? "+" : ""}{department.eNps}</td>
                  <td>{department.participation}%</td>
                  <td>{department.driver}</td>
                  <td><span className={`risk-badge ${department.level ?? ""}`}>{getLevelText(department.level)}</span></td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>

        <aside className="card insight-panel">
          <div className="card-header-clean">
            <div>
              <h4>Riskli Ekipler</h4>
              <p className="text-muted">Sessiz memnuniyetsizlik ve bağlılık kırılmaları.</p>
            </div>
          </div>
          <div className="action-list">
            {riskTeams.map((team) => (
              <div key={team.departmentId} className={`risk-action ${team.level ?? ""}`}>
                <div className="action-priority">{team.mood}</div>
                <strong>{team.dept}</strong>
                <p>{team.driver}</p>
              </div>
            ))}
          </div>
        </aside>
      </div>

      <div className="detail-support-grid">
        <section className="card">
          <div className="card-header-clean"><h4>Son Nabız Sinyalleri</h4></div>
          <div className="signal-list">
            {(data.signals ?? []).map((signal) => (
              <div key={signal} className="signal-note"><i aria-hidden="true" className="fa-solid fa-circle-info" /><span>{signal}</span></div>
            ))}
          </div>
        </section>
        <section className="card">
          <div className="card-header-clean"><h4>Önerilen Aksiyonlar</h4></div>
          <div className="signal-list">
            {(data.recommendedActions ?? []).map((action) => (
              <div key={action} className="signal-note action"><i aria-hidden="true" className="fa-solid fa-check" /><span>{action}</span></div>
            ))}
          </div>
        </section>
      </div>
    </div>
  );
}
