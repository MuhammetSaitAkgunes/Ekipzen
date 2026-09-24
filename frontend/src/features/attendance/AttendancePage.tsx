import { useEffect, useRef, useState } from "react";
import { useToast } from "../../layout/ToastProvider";
import { AttendanceEntryModal } from "./AttendanceEntryModal";
import { downloadCsv, tableToCsvLines } from "../shared/csv";
import { PageError, PageLoading } from "../shared/PageState";
import { LIVE_STATUS_TEXT, formatBreak, formatTime, formatTsDate, minutesToHhMm, monthLabel } from "./format";
import {
  useAttendanceSummary, useEmployeeOptions, useLiveBoard, useTimesheet, type TimesheetRowDto,
} from "./queries";

type EntryTarget = { rowId: number | null; row?: TimesheetRowDto };

export function AttendancePage() {
  const { showToast } = useToast();
  const now = new Date();
  const [period, setPeriod] = useState({ year: now.getFullYear(), month: now.getMonth() + 1 });
  const [activeTab, setActiveTab] = useState<"live-view" | "timesheet-view">("live-view");
  const [employeeId, setEmployeeId] = useState<number | null>(null);
  const [entry, setEntry] = useState<EntryTarget | null>(null);
  const tableRef = useRef<HTMLTableElement>(null);

  const liveQ = useLiveBoard();
  const summaryQ = useAttendanceSummary(period.year, period.month);
  const employeesQ = useEmployeeOptions();
  const timesheetQ = useTimesheet(employeeId, period.year, period.month);

  useEffect(() => {
    if (employeeId === null && (employeesQ.data ?? []).length > 0) {
      setEmployeeId(employeesQ.data![0].id ?? null);
    }
  }, [employeesQ.data, employeeId]);

  if (liveQ.isPending || summaryQ.isPending || employeesQ.isPending) return <PageLoading />;
  if (liveQ.isError) return <PageError error={liveQ.error} />;
  if (summaryQ.isError) return <PageError error={summaryQ.error} />;
  if (employeesQ.isError) return <PageError error={employeesQ.error} />;

  const summary = summaryQ.data;
  const totalWorkedHours = Math.round(summary.reduce((t, s) => t + (s.totalWorkedMinutes ?? 0), 0) / 60);
  const totalOvertimeHours = Math.round(summary.reduce((t, s) => t + (s.totalOvertimeMinutes ?? 0), 0) / 60);
  const lateCount = summary.filter((s) => (s.lateDays ?? 0) > 0).length;
  const absentDays = summary.reduce((t, s) => t + (s.absentDays ?? 0), 0);

  const changeMonth = (step: number) =>
    setPeriod((current) => {
      const date = new Date(current.year, current.month - 1 + step, 1);
      return { year: date.getFullYear(), month: date.getMonth() + 1 };
    });

  const exportCsv = () => {
    if (!tableRef.current) {
      showToast("Dışa aktarılacak tablo bulunamadı.", "error");
      return;
    }
    downloadCsv(tableToCsvLines(tableRef.current), "puantaj-raporu");
    showToast("CSV raporu indirildi.", "success");
  };

  const rows = timesheetQ.data?.rows ?? [];

  return (
    <div id="attendance-screen">
      <div className="page-header">
        <div>
          <h2>Mesai & Puantaj Takibi</h2>
          <p>Giriş-çıkış saatleri, vardiya durumu ve aylık puantaj kontrolü.</p>
        </div>
        <div className="header-actions">
          <div className="date-picker-wrapper">
            <button className="btn-icon" aria-label="Önceki ay" title="Önceki ay" onClick={() => changeMonth(-1)}>
              <i aria-hidden="true" className="fa-solid fa-chevron-left" />
            </button>
            <span className="current-month">
              <i aria-hidden="true" className="fa-solid fa-calendar-days" /> <span id="attendance-month">{monthLabel(period.year, period.month)}</span>
            </span>
            <button className="btn-icon" aria-label="Sonraki ay" title="Sonraki ay" onClick={() => changeMonth(1)}>
              <i aria-hidden="true" className="fa-solid fa-chevron-right" />
            </button>
          </div>
          <button className="btn btn-primary" onClick={exportCsv}>
            <i aria-hidden="true" className="fa-solid fa-file-export" /> Rapor Al
          </button>
        </div>
      </div>

      <div className="stats-stripe">
        <div className="stat-box"><span className="sb-label">Toplam Çalışma</span><strong className="sb-val">{totalWorkedHours} <small>saat</small></strong></div>
        <div className="stat-box"><span className="sb-label">Fazla Mesai</span><strong className="sb-val text-orange">{totalOvertimeHours} <small>saat</small></strong></div>
        <div className="stat-box"><span className="sb-label">Geç Kalma</span><strong className="sb-val text-red">{lateCount} <small>kişi</small></strong></div>
        <div className="stat-box"><span className="sb-label">Devamsızlık</span><strong className="sb-val">{absentDays} <small>gün</small></strong></div>
      </div>

      <section className="ez-panel ez-attendance-guide" aria-labelledby="attendance-guide-title">
        <span className="ez-overline">PUANTAJ İŞ AKIŞI</span>
        <h3 id="attendance-guide-title">Bir kaydı nasıl kontrol ederim?</h3>
        <ol>
          <li>Ayı ve çalışanı seçip Aylık Puantaj satırlarını gerçek giriş-çıkış kaydıyla karşılaştırın. Canlı İzleme yalnız o günün görünümüdür.</li>
          <li>Eksik günü manuel girin; yanlış saati mevcut satırdan düzeltin. Aynı çalışan için aynı güne ikinci kayıt açılamaz.</li>
          <li>Kaydettikten sonra süre ve fazla mesai sonucunu yeniden okuyun. Bu uygulama hesabıdır; izin veya yasal fazla çalışma kararı yerine geçmez.</li>
          <li>Bordro dönemi önceden açıldıysa, ilgili bordro girdisini ayrıca kontrol edin.</li>
        </ol>
        <button className="btn btn-ghost" onClick={() => setActiveTab("timesheet-view")}>Aylık puantajı aç</button>
      </section>

      <div className="att-content surface">
        <div className="att-tabs">
          <button className={`att-tab ${activeTab === "live-view" ? "active" : ""}`} onClick={() => setActiveTab("live-view")}>
            <i aria-hidden="true" className="fa-solid fa-video" /> Canlı İzleme
          </button>
          <button className={`att-tab ${activeTab === "timesheet-view" ? "active" : ""}`} onClick={() => setActiveTab("timesheet-view")}>
            <i aria-hidden="true" className="fa-solid fa-table" /> Aylık Puantaj
          </button>
        </div>

        <div id="live-view" className={`att-section ${activeTab === "live-view" ? "active" : ""}`}>
          <div className="live-grid">
            {liveQ.data.map((card) => (
              <div key={card.employeeId} className={`live-card ${card.status ?? ""}`}>
                <div className="lc-header">
                  <span className={`lc-badge ${card.status ?? ""}`}>{LIVE_STATUS_TEXT[card.status ?? ""] ?? card.status}</span>
                  <span className="lc-time"><i aria-hidden="true" className="fa-regular fa-clock" /> {formatTime(card.checkIn)}</span>
                </div>
                <div className="lc-body">
                  <div className="lc-avatar">{card.initials}</div>
                  <div>
                    <h4>{card.name}</h4>
                    <p>{card.department}</p>
                  </div>
                </div>
              </div>
            ))}
            {/* div: button elementi tarayıcı çerçevesi getirir (parite) */}
            <div className="live-card ghost" role="button" tabIndex={0} onClick={() => setEntry({ rowId: null })}
              onKeyDown={(e) => { if (e.key === "Enter" || e.key === " ") setEntry({ rowId: null }); }}>
              <i aria-hidden="true" className="fa-solid fa-plus" />
              <span>Manuel giriş ekle</span>
            </div>
          </div>
        </div>

        <div id="timesheet-view" className={`att-section ${activeTab === "timesheet-view" ? "active" : ""}`}>
          <div className="ts-filter">
            <label className="sr-only" htmlFor="ts-user-select">Personel seç</label>
            <select
              id="ts-user-select"
              className="user-select"
              value={employeeId ?? ""}
              onChange={(e) => setEmployeeId(Number(e.target.value))}
            >
              {(employeesQ.data ?? []).map((emp) => (
                <option key={emp.id} value={emp.id}>{emp.name} ({emp.department})</option>
              ))}
            </select>
            <button className="btn btn-ghost btn-sm" onClick={() => setEntry({ rowId: null })}>Eksik günü kaydet</button>
            <div className="legend">
              <span className="leg-item"><span className="dot ok" /> Normal</span>
              <span className="leg-item"><span className="dot warn" /> Geç/Erken</span>
              <span className="leg-item"><span className="dot danger" /> Eksik</span>
            </div>
          </div>

          <div className="table-wrapper">
            <table className="att-table" ref={tableRef}>
              <thead>
                <tr>
                  <th>Tarih</th><th>Tip</th><th>Giriş</th><th>Çıkış</th><th>Mola</th><th>Net Süre</th><th>Durum</th><th className="csv-skip"></th>
                </tr>
              </thead>
              <tbody>
                {rows.map((row) => (
                  <tr key={row.id}>
                    <td><span className="date-cell">{formatTsDate(row.workDate)}</span></td>
                    <td>
                      {row.type === "Tam"
                        ? <span className="type-badge reg">Normal</span>
                        : row.type === "Mesai"
                          ? <span className="type-badge over">Fazla mesai</span>
                          : <span className="type-badge abs">İzin/Rapor</span>}
                    </td>
                    <td className="mono">{formatTime(row.checkIn)}</td>
                    <td className="mono">{formatTime(row.checkOut)}</td>
                    <td className="mono">{formatBreak(row.breakMinutes)}</td>
                    <td><strong className="mono">{minutesToHhMm(row.workedMinutes)}</strong></td>
                    <td>
                      {row.status === "late"
                        ? <span className="warn-text"><i aria-hidden="true" className="fa-solid fa-triangle-exclamation" /> Geç</span>
                        : row.status === "overtime"
                          ? <span className="success-text"><i aria-hidden="true" className="fa-solid fa-star" /> +{Math.round((row.overtimeMinutes ?? 0) / 60)}s mesai</span>
                          : row.status === "absent"
                            ? <span className="danger-text">Gelmedi</span>
                            : <span className="ok-text"><i aria-hidden="true" className="fa-solid fa-check" /> Uygun</span>}
                    </td>
                    <td className="text-right csv-skip">
                      <button className="btn-icon-sm" title="Kaydı düzenle" aria-label={`${formatTsDate(row.workDate)} puantaj kaydını düzenle`} onClick={() => setEntry({ rowId: row.id ?? null, row })}>
                        <i aria-hidden="true" className="fa-solid fa-pen" />
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
              <tfoot>
                <tr>
                  <td colSpan={5} className="text-right"><strong>Aylık Toplam:</strong></td>
                  <td><strong className="text-blue mono">{minutesToHhMm(timesheetQ.data?.totalWorkedMinutes)}</strong></td>
                  <td colSpan={2}></td>
                </tr>
              </tfoot>
            </table>
          </div>
        </div>
      </div>

      {entry && (
        <AttendanceEntryModal
          rowId={entry.rowId}
          initial={entry.row}
          defaultEmployeeId={employeeId}
          onClose={() => setEntry(null)}
        />
      )}
    </div>
  );
}
