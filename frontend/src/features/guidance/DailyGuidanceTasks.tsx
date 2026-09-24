import { Link } from "react-router-dom";
import { appPath } from "../../paths";
import { PageError, PageLoading } from "../shared/PageState";
import { useDailyGuidanceTasks } from "./queries";

const dateLabel = (date: string): string => date.split("-").reverse().join(".");

export function DailyGuidanceTasks({ limit }: { limit?: number }) {
  const query = useDailyGuidanceTasks();
  if (query.isPending) return <PageLoading />;
  if (query.isError) return <PageError error={query.error} />;

  const tasks = limit ? query.data.slice(0, limit) : query.data;
  return <div className="ez-guidance-tasks">
    {tasks.map((task) => <article className="ez-guidance-task" key={task.key}>
      <span className="ez-overline">{task.kind === "leave-approval" ? "İZİN ONAYI" : "BELGE TAKİBİ"}</span>
      <h3>{task.title}</h3>
      <p><strong>Neden?</strong> {task.reason}</p>
      <p><strong>Ne yapmalı?</strong> {task.nextAction}</p>
      <small>{task.dueDate ? `Kayıtlı son tarih: ${dateLabel(task.dueDate)}` : task.dateNote}</small>
      <Link to={appPath(task.targetPath)}>Kaydı incele <span aria-hidden="true">→</span></Link>
    </article>)}
    {query.data.length === 0 && <div className="ez-empty"><h3>Bu alanlarda bekleyen iş yok</h3><p>Açık izin onayı veya eksik belge kaydı görünmüyor. Diğer işleri İş Kutusu'ndan kontrol edin.</p></div>}
    {limit && query.data.length > limit && <Link className="ez-section-link" to={appPath("/inbox?tab=daily")}>Tüm {query.data.length} işi gör <span aria-hidden="true">→</span></Link>}
  </div>;
}
