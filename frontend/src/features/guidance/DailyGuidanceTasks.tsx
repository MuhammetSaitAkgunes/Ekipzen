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
    <p className="ez-guidance-task-scope">Bu liste şu anda yetkiniz kapsamındaki bekleyen izin onaylarını; İK yöneticileri için eksik/süresi yaklaşan uyum belgelerini, açık ayrılış taslaklarını, yürürlükte ücret paketi bulunmayan etkin çalışanları, açık bordro dönemlerini ve Yeni aşamasındaki adayları gösterir. Diğer İK işleri burada henüz üretilmez.</p>
    {tasks.map((task) => <article className="ez-guidance-task" key={task.key}>
      <span className="ez-overline">{task.kind === "leave-approval" ? "İZİN ONAYI" : task.kind === "payroll-period" ? "BORDRO DÖNEMİ" : task.kind === "candidate-review" ? "ADAY İNCELEMESİ" : task.kind === "employee-compensation" ? "ÜCRET HAZIRLIĞI" : task.kind === "offboarding-case" ? "AYRILIŞ TASLAĞI" : "BELGE TAKİBİ"}</span>
      <h3>{task.title}</h3>
      <p><strong>Neden?</strong> {task.reason}</p>
      <p><strong>Ne yapmalı?</strong> {task.nextAction}</p>
      <small>{task.dueDate ? `Kayıtlı son tarih: ${dateLabel(task.dueDate)}` : task.dateNote}</small>
      <Link to={appPath(task.targetPath)}>Kaydı incele <span aria-hidden="true">→</span></Link>
    </article>)}
    {query.data.length === 0 && <div className="ez-empty"><h3>Bu alanlarda bekleyen iş yok</h3><p>Açık izin onayı, eksik belge, ayrılış taslağı, ücret hazırlığı, açık bordro dönemi veya Yeni aşamasında aday görünmüyor. Diğer işleri İş Kutusu'ndan kontrol edin.</p></div>}
    {limit && query.data.length > limit && <Link className="ez-section-link" to={appPath("/inbox?tab=daily")}>Tüm {query.data.length} işi gör <span aria-hidden="true">→</span></Link>}
  </div>;
}
