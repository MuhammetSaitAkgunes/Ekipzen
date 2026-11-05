import { GuidanceDrawer } from "../guidance/GuidanceDrawer";
import { GuidanceSourceLink } from "../guidance/GuidanceSourceLink";
import { useEffect, useState } from "react";
import { useSearchParams } from "react-router-dom";
import { PageError, PageLoading } from "../shared/PageState";
import { CandidateDetail } from "./CandidateDetail";
import { CandidateModal } from "./CandidateModal";
import { PositionModal } from "./PositionModal";
import { formatTimeAgo, formatCandidateScore, scoreClass, statusTagClass } from "./format";
import { useCandidates, usePositions, useRecruitmentFunnel, type PositionDto, type RecruitmentFunnelDto } from "./queries";

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
  const [searchParams, setSearchParams] = useSearchParams();
  const [createOpen, setCreateOpen] = useState(false);
  const [positionEditor, setPositionEditor] = useState<PositionDto | "new" | null>(null);

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
  const rawCandidateId = searchParams.get("candidateId");
  const requestedCandidateId = rawCandidateId && /^[1-9]\d*$/.test(rawCandidateId) ? Number(rawCandidateId) : null;
  const activeId = candidates.find((candidate) => candidate.id === requestedCandidateId)?.id ?? candidates[0]?.id ?? null;

  return (
    <div className="ez-recruitment-page">
      <div className="page-header">
        <h1>İşe Alım</h1>
        <div className="header-actions">
          <GuidanceDrawer title="İşe alım rehberi">
            <GuidanceSourceLink processKey="recruitment" />
            <p>Aşama sayıları güncel kayıtları gösterir.</p>
            {STAGES.map(stage => <section key={stage.status}><h3>{stage.label}</h3><p>{stage.explanation}</p></section>)}
          </GuidanceDrawer>
          <button className="btn btn-secondary" onClick={() => setPositionEditor("new")}>Yeni pozisyon</button>
          <button className="btn btn-primary" onClick={() => setCreateOpen(true)}>Yeni Aday</button>
        </div>
      </div>
      <section className="ez-recruitment-funnel" aria-labelledby="recruitment-funnel-title">
        <h2 className="sr-only" id="recruitment-funnel-title">Aday aşamaları</h2>
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

      </section>
      <div id="ats-container">
      <aside className="ats-sidebar">
        <div className="sidebar-header">
          <div>
            <h3>Aday Havuzu <span className="badge-count">{candidates.length}</span></h3>
          </div>
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
          {!funnelQ.data && <div className="filter-tabs">
            {FILTER_TABS.map(([value, label]) => (
              <button
                key={label}
                className={`ft-btn ${statusFilter === value ? "active" : ""}`}
                onClick={() => setStatusFilter(value)}
              >
                {label}
              </button>
            ))}
          </div>}
        </div>
        <div className="candidate-list">
          {candidates.map((candidate) => (
            <div
              key={candidate.id}
              className={`candidate-item ${candidate.id === activeId ? "active" : ""}`}
              role="button"
              tabIndex={0}
              aria-pressed={candidate.id === activeId}
              onClick={() => setSearchParams({ candidateId: String(candidate.id) }, { replace: true })}
              onKeyDown={(event) => { if (event.key === "Enter" || event.key === " ") { event.preventDefault(); setSearchParams({ candidateId: String(candidate.id) }, { replace: true }); } }}
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
                  <span className={`score-text ${candidate.score == null ? "" : scoreClass(candidate.score)}`}>{formatCandidateScore(candidate.score)}</span>
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
      </div>

      <details className="ez-recruitment-positions">
        <summary>Pozisyonlar · {positionsQ.data?.length ?? "—"}</summary>
        <div className="ez-recruitment-positions-head">
          <div>
            <h2 id="recruitment-positions-title">Pozisyonlar</h2>
            <p>Pozisyonu açın, ardından adayı ilgili kayda bağlayın. Liste açık ve kapanmış kadroları gösterir.</p>
          </div>
        </div>
        {positionsQ.isPending && <p className="pending-desc">Pozisyonlar yükleniyor.</p>}
        {positionsQ.isError && <p className="form-error" role="alert">Pozisyonlar yüklenemedi. Adayı pozisyona bağlamadan önce listeyi yeniden yükleyin.</p>}
        {positionsQ.data?.length === 0 && <p className="pending-desc">Henüz pozisyon yok. İlk açık kadroyu “Yeni pozisyon” ile oluşturun.</p>}
        {positionsQ.data && positionsQ.data.length > 0 && <div className="ez-recruitment-position-list">
          {positionsQ.data.map((position) => (
            <div className="ez-recruitment-position-card" key={position.id}>
              <strong>{position.title}</strong>
              <span>{position.department || "Departman seçilmedi"}</span>
              <span>{position.isOpen ? "Açık" : "Kapalı"} · {position.openCount ?? 0} kalan kadro · {position.candidateCount ?? 0} bağlı aday</span>
              <button type="button" className="btn btn-secondary"
                onClick={() => setPositionEditor(position)}>{position.title} kaydını düzelt</button>
            </div>
          ))}
        </div>}
      </details>

      {createOpen && (
        <CandidateModal onClose={() => setCreateOpen(false)} onCreated={(id) => { setStatusFilter(""); setSearch(""); setSearchParams({ candidateId: String(id) }, { replace: true }); }} />
      )}
      {positionEditor && <PositionModal key={positionEditor === "new" ? "new" : positionEditor.id}
        position={positionEditor === "new" ? undefined : positionEditor}
        onClose={() => setPositionEditor(null)} />}
    </div>
  );
}
