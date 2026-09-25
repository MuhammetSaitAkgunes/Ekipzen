import { useEffect, useState } from "react";
import { PageError, PageLoading } from "../shared/PageState";
import { CandidateDetail } from "./CandidateDetail";
import { CandidateModal } from "./CandidateModal";
import { PositionModal } from "./PositionModal";
import { formatTimeAgo, scoreClass, statusTagClass } from "./format";
import { useCandidates, usePositions, useRecruitmentFunnel, type RecruitmentFunnelDto } from "./queries";

const STAGES: { status: string; label: string; count: keyof RecruitmentFunnelDto; explanation: string }[] = [
  { status: "Yeni", label: "Yeni", count: "new", explanation: "Başvurular ilk inceleme sırasındadır; bu sayı tek başına gecikme veya yapılmış kontrol anlamına gelmez." },
  { status: "Mülakat", label: "Mülakat", count: "interview", explanation: "Adaylar değerlendirme aşamasındadır; randevu veya davetin gönderildiğini bu etiket kanıtlamaz." },
  { status: "Teklif", label: "Teklif", count: "offer", explanation: "Adaylar teklif aşamasındadır; teklif belgesinin hazırlanıp gönderildiğini ayrıca doğrulayın." },
  { status: "Red", label: "Red", count: "rejected", explanation: "Adaylar red aşamasındadır; aday iletişiminin tamamlandığını bu etiket göstermez." },
  { status: "İşe Alındı", label: "İşe Alındı", count: "hired", explanation: "Adaylar çalışan kaydına dönüştürülmüştür; kalan işe giriş adımlarını personel kartında kontrol edin." },
];
const FILTER_TABS: [string, string][] = [["", "Tümü"], ...STAGES.map((stage): [string, string] => [stage.status, stage.label])];

export function RecruitmentPage() {
  const [search, setSearch] = useState("");
  const [debouncedSearch, setDebouncedSearch] = useState("");
  const [statusFilter, setStatusFilter] = useState("");
  const [selectedId, setSelectedId] = useState<number | null>(null);
  const [createOpen, setCreateOpen] = useState(false);
  const [positionOpen, setPositionOpen] = useState(false);

  // Personel sayfasındaki server-side arama debounce deseni.
  useEffect(() => {
    const timer = setTimeout(() => setDebouncedSearch(search.trim()), 300);
    return () => clearTimeout(timer);
  }, [search]);

  const candidatesQ = useCandidates(debouncedSearch, statusFilter);
  const positionsQ = usePositions();
  const funnelQ = useRecruitmentFunnel();

  if (candidatesQ.isPending) return <PageLoading />;
  if (candidatesQ.isError) return <PageError error={candidatesQ.error} />;

  const candidates = candidatesQ.data;
  const activeId = candidates.find((candidate) => candidate.id === selectedId)?.id ?? candidates[0]?.id ?? null;
  const activeStage = STAGES.find((stage) => stage.status === statusFilter);

  return (
    <div className="ez-recruitment-page">
      <section className="ez-recruitment-positions" aria-labelledby="recruitment-positions-title">
        <div className="ez-recruitment-positions-head">
          <div>
            <h2 id="recruitment-positions-title">Açık pozisyonlar</h2>
            <p>Pozisyonu açın, ardından adayı ilgili kayda bağlayın. Liste açık ve kapanmış kadroları gösterir.</p>
          </div>
          <button className="btn btn-secondary" onClick={() => setPositionOpen(true)}>Yeni pozisyon</button>
        </div>
        {positionsQ.isPending && <p className="pending-desc">Pozisyonlar yükleniyor.</p>}
        {positionsQ.isError && <p className="form-error" role="alert">Pozisyonlar yüklenemedi. Adayı pozisyona bağlamadan önce listeyi yeniden yükleyin.</p>}
        {positionsQ.data?.length === 0 && <p className="pending-desc">Henüz pozisyon yok. İlk açık kadroyu “Yeni pozisyon” ile oluşturun.</p>}
        {positionsQ.data && positionsQ.data.length > 0 && <div className="ez-recruitment-position-list">
          {positionsQ.data.map((position) => (
            <div className="ez-recruitment-position-card" key={position.id}>
              <strong>{position.title}</strong>
              <span>{position.department || "Departman seçilmedi"}</span>
              <span>{position.isOpen ? "Açık" : "Kapalı"} · {position.openCount ?? 0} açık kadro · {position.candidateCount ?? 0} bağlı aday</span>
            </div>
          ))}
        </div>}
      </section>
      <section className="ez-recruitment-funnel" aria-labelledby="recruitment-funnel-title">
        <h2 id="recruitment-funnel-title">Aday aşamaları</h2>
        <p>Bu sayılar adayların şu anki aşamalarını gösterir; tarihsel geçiş veya işe alım dönüşüm oranı değildir.</p>
        {funnelQ.isPending && <p className="pending-desc">Aşama sayıları yükleniyor.</p>}
        {funnelQ.isError && <p className="form-error" role="alert">Aşama sayıları yüklenemedi. Aday listesindeki filtreleri kullanabilirsiniz.</p>}
        {funnelQ.data && <div className="ez-recruitment-funnel-list">
          <button className="ez-recruitment-funnel-card" aria-label={"Tümü: " + (funnelQ.data.total ?? 0) + " aday"} aria-pressed={statusFilter === ""} onClick={() => setStatusFilter("")}>
            <strong>Tümü</strong><span>{funnelQ.data.total ?? 0} aday</span>
          </button>
          {STAGES.map((stage) => (
            <button key={stage.status} className="ez-recruitment-funnel-card" aria-label={stage.label + ": " + (funnelQ.data?.[stage.count] ?? 0) + " aday"} aria-pressed={statusFilter === stage.status}
              onClick={() => setStatusFilter(stage.status)}>
              <strong>{stage.label}</strong><span>{funnelQ.data?.[stage.count] ?? 0} aday</span>
            </button>
          ))}
        </div>}
        {activeStage && <p className="ez-recruitment-stage-explanation" role="status">{activeStage.explanation}</p>}
      </section>
      <div id="ats-container">
      <aside className="ats-sidebar">
        <div className="sidebar-header">
          <div>
            <h3>Aday Havuzu <span className="badge-count">{candidates.length}</span></h3>
            <p>Adaylar ve başvuru aşamaları</p>
          </div>
          <button className="btn btn-primary btn-sm" onClick={() => setCreateOpen(true)}>
            <i aria-hidden="true" className="fa-solid fa-plus" /> Yeni Aday
          </button>
          <div className="search-wrap">
            <i aria-hidden="true" className="fa-solid fa-magnifying-glass" />
            <label className="sr-only" htmlFor="candidate-search">Aday ara</label>
            <input
              id="candidate-search"
              type="text"
              placeholder="Aday ara"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
            />
          </div>
          <div className="filter-tabs">
            {FILTER_TABS.map(([value, label]) => (
              <button
                key={label}
                className={`ft-btn ${statusFilter === value ? "active" : ""}`}
                onClick={() => setStatusFilter(value)}
              >
                {label}
              </button>
            ))}
          </div>
        </div>
        <div className="candidate-list">
          {candidates.map((candidate) => (
            <div
              key={candidate.id}
              className={`candidate-item ${candidate.id === activeId ? "active" : ""}`}
              role="button"
              tabIndex={0}
              aria-pressed={candidate.id === activeId}
              onClick={() => setSelectedId(candidate.id ?? null)}
              onKeyDown={(event) => { if (event.key === "Enter" || event.key === " ") { event.preventDefault(); setSelectedId(candidate.id ?? null); } }}
            >
              <div className="ci-avatar" aria-hidden="true">{candidate.initials}</div>
              <div className="ci-info">
                <div className="ci-header">
                  <h4>{candidate.name}</h4>
                  <span className="ci-time">{formatTimeAgo(candidate.appliedAtUtc)}</span>
                </div>
                <p>{candidate.appliedRole}</p>
                <div className="ci-meta">
                  <span className={`status-tag ${statusTagClass(candidate.status)}`}>{candidate.status}</span>
                  <span className={`score-text ${scoreClass(candidate.score)}`}>{candidate.score}/100 puan</span>
                </div>
              </div>
            </div>
          ))}
          {candidates.length === 0 && (
            <p className="pending-desc">Henüz aday yok. "Yeni Aday" ile ilk adayı ekleyin.</p>
          )}
        </div>
      </aside>

      <main className="ats-detail">
        {activeId !== null ? (
          <CandidateDetail id={activeId} />
        ) : (
          <div className="card"><p className="pending-desc">Görüntülenecek aday yok.</p></div>
        )}
      </main>

      {createOpen && (
        <CandidateModal onClose={() => setCreateOpen(false)} onCreated={(id) => { setStatusFilter(""); setSearch(""); setSelectedId(id); }} />
      )}
      {positionOpen && <PositionModal onClose={() => setPositionOpen(false)} />}
      </div>
    </div>
  );
}
