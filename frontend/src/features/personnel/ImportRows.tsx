import { useState } from "react";
import { Link } from "react-router-dom";
import { appPath } from "../../paths";
import type { ImportPreviewDto, ImportResultDto, ImportRowPreviewDto } from "./queries";

const count = (value: unknown): value is number => typeof value === "number" && Number.isInteger(value) && value >= 0;
const rowsValid = (rows: ImportRowPreviewDto[] | null | undefined, statuses: string[]) => Array.isArray(rows)
  && rows.every((row) => row !== null && typeof row === "object" && count(row.satirNo) && row.satirNo! >= 2
    && typeof row.adSoyad === "string" && typeof row.unvan === "string" && typeof row.departman === "string"
    && typeof row.aciklama === "string" && statuses.includes(row.durum ?? "") && Array.isArray(row.sorunlar)
    && (row.durum !== "created" || (count(row.employeeId) && row.employeeId! > 0)))
  && new Set(rows.map((row) => row.satirNo)).size === rows.length;

export const validImportPreview = (report: ImportPreviewDto) =>
  [report.toplamSatir, report.gecerliSatir, report.hataliSatir, report.mukerrerSatir].every(count)
  && Array.isArray(report.sorunlar) && Array.isArray(report.bilinmeyenDepartmanlar)
  && rowsValid(report.satirlar, ["ready", "invalid", "duplicate"])
  && report.satirlar!.length === report.toplamSatir
  && report.satirlar!.filter((row) => row.durum === "ready").length === report.gecerliSatir
  && report.satirlar!.filter((row) => row.durum === "invalid").length === report.hataliSatir
  && report.satirlar!.filter((row) => row.durum === "duplicate").length === report.mukerrerSatir
  && (report.toplamSatir === 0 || /^[A-F0-9]{64}$/.test(report.previewToken ?? ""));

export const validImportResult = (result: ImportResultDto, requestId: string, preview?: ImportPreviewDto | null) =>
  result.importRequestId === requestId && count(result.olusturulanSatir) && count(result.atlananSatir)
  && Array.isArray(result.sorunlar) && rowsValid(result.satirlar, ["created", "invalid", "duplicate"])
  && result.satirlar!.filter((row) => row.durum === "created").length === result.olusturulanSatir
  && new Set(result.satirlar!.filter((row) => row.durum === "created").map((row) => row.employeeId)).size === result.olusturulanSatir
  && result.satirlar!.length === result.olusturulanSatir! + result.atlananSatir!
  && (!preview || (result.olusturulanSatir === preview.gecerliSatir
    && result.satirlar!.length === preview.toplamSatir
    && result.satirlar!.every((row, index) => row.satirNo === preview.satirlar?.[index]?.satirNo
      && row.adSoyad === preview.satirlar?.[index]?.adSoyad
      && row.unvan === preview.satirlar?.[index]?.unvan && row.departman === preview.satirlar?.[index]?.departman
      && row.iseGirisTarihi === preview.satirlar?.[index]?.iseGirisTarihi
      && row.durum === (preview.satirlar?.[index]?.durum === "ready" ? "created" : preview.satirlar?.[index]?.durum))));

const labels: Record<string, string> = { ready: "Oluşturulacak", created: "Oluşturuldu", invalid: "Hatalı; atlanacak", duplicate: "Mevcut; atlanacak" };

export function ImportRows({ rows, completed = false }: { rows: ImportRowPreviewDto[]; completed?: boolean }) {
  const [filter, setFilter] = useState("all");
  const [page, setPage] = useState(0);
  const filtered = rows.filter((row) => filter === "all" || row.durum === filter);
  const pages = Math.max(1, Math.ceil(filtered.length / 25));
  const current = Math.min(page, pages - 1);
  return <section aria-label={completed ? "Satır bazında aktarım sonucu" : "Satır bazında önizleme"}>
    <label>Gösterilecek satırlar <select className="input-control" value={filter} onChange={(e) => { setFilter(e.target.value); setPage(0); }}>
      <option value="all">Tüm satırlar</option>
      <option value={completed ? "created" : "ready"}>{completed ? "Oluşturulanlar" : "Oluşturulacaklar"}</option>
      <option value="invalid">Hatalı satırlar</option><option value="duplicate">Mevcut kayıtlar</option>
    </select></label>
    <div className="ez-import-table-scroll"><table className="data-table">
      <caption>{completed ? "Gerçek sonuç: çalışan kartından inceleyin." : "Excel satırı ve yapılacak işlem: kişileri tek tek kontrol edin."}</caption>
      <thead><tr><th>Excel satırı</th><th>Personel</th><th>Departman / işe giriş</th><th>{completed ? "Sonuç" : "Planlanan işlem"}</th></tr></thead>
      <tbody>{filtered.slice(current * 25, current * 25 + 25).map((row) => <tr key={row.satirNo}>
        <td data-label="Excel satırı">{row.satirNo}</td><td data-label="Personel"><strong>{row.adSoyad || "Ad eksik"}</strong><br />{row.unvan}</td>
        <td data-label="Departman / işe giriş">{row.departman || "Departman eksik"}<br />{row.iseGirisTarihi || "Tarih okunamadı"}</td>
        <td data-label={completed ? "Sonuç" : "Planlanan işlem"}><strong>{completed && row.durum === "invalid" ? "Hatalı; atlandı" : completed && row.durum === "duplicate" ? "Mevcut; atlandı" : labels[row.durum ?? ""]}</strong>
          <p>{row.aciklama}</p>{row.sorunlar?.map((issue, index) => <p key={index}>{issue.alan}: {issue.mesaj}</p>)}
          {row.durum === "created" && <Link to={appPath("/personnel/" + row.employeeId)}>Çalışan kartını aç</Link>}
        </td>
      </tr>)}</tbody>
    </table></div>
    {filtered.length === 0 && <p>Bu grupta satır yok.</p>}
    <div className="ez-pagination"><span>{filtered.length} satır · Sayfa {current + 1} / {pages}</span><div>
      <button className="btn btn-secondary btn-sm" disabled={current === 0} onClick={() => setPage(current - 1)}>Önceki satırlar</button>
      <button className="btn btn-secondary btn-sm" disabled={current + 1 >= pages} onClick={() => setPage(current + 1)}>Sonraki satırlar</button>
    </div></div>
  </section>;
}
