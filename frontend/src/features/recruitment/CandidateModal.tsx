import { GuidanceDecisionCheck } from "../guidance/GuidanceDecisionCheck";
import { useState } from "react";
import { ApiError } from "../../api/client";
import { useToast } from "../../layout/ToastProvider";
import { useCreateCandidate, usePositions, type CreateCandidateCommand } from "./queries";
import { formatCandidateScore } from "./format";

type CandidateForm = {
  name: string; appliedRole: string; positionId: string; score: string; location: string;
  experienceYears: string; summary: string; skills: string;
};

export function CandidateModal({ onClose, onCreated }: {
  onClose: () => void; onCreated: (id: number) => void;
}) {
  const { showToast } = useToast();
  const createCandidate = useCreateCandidate();
  const positionsQ = usePositions();
  const [form, setForm] = useState<CandidateForm>({
    name: "", appliedRole: "", positionId: "", score: "", location: "", experienceYears: "0", summary: "", skills: "",
  });
  const [review, setReview] = useState<CreateCandidateCommand | null>(null);
  const [error, setError] = useState<string | null>(null);
  const selectedPosition = form.positionId
    ? positionsQ.data?.find((position) => String(position.id) === form.positionId)
    : null;

  const set = (key: keyof CandidateForm) =>
    (e: React.ChangeEvent<HTMLInputElement | HTMLTextAreaElement>) => {
      setReview(null);
      setError(null);
      setForm((current) => ({ ...current, [key]: e.target.value }));
    };

  const prepareReview = () => {
    setError(null);
    const name = form.name.trim();
    const appliedRole = form.appliedRole.trim();
    if (!name || !appliedRole) {
      setError("Ad Soyad ve pozisyon zorunludur.");
      return;
    }
    if (form.positionId && (!selectedPosition || !selectedPosition.isOpen || (selectedPosition.openCount ?? 0) < 1)) {
      setError("Seçilen pozisyon artık açık değil. Pozisyonu yeniden seçin.");
      return;
    }
    if (name.length > 200 || appliedRole.length > 128 || form.summary.trim().length > 2000) {
      setError("Ad en fazla 200, pozisyon 128 ve başvuru özeti 2000 karakter olabilir.");
      return;
    }
    if (form.score !== "" && (!/^(0|[1-9]\d{0,2})$/.test(form.score) || Number(form.score) > 100)) {
      setError("Aday puanını boş bırakın veya 0-100 arasında tam sayı olarak girin.");
      return;
    }
    if (!/^(0|[1-9]\d{0,2})$/.test(form.experienceYears) || Number(form.experienceYears) > 60) {
      setError("Deneyimi 0-60 arasında tam yıl olarak girin.");
      return;
    }
    setReview({
      name, appliedRole, positionId: selectedPosition?.id ?? null, score: form.score === "" ? null : Number(form.score),
      location: form.location.trim() || null,
      experienceYears: Number(form.experienceYears),
      summary: form.summary.trim() || null,
      skills: form.skills.split(",").map((skill) => skill.trim()).filter(Boolean),
    });
  };

  const submit = async () => {
    if (!review || createCandidate.isPending) return;
    setError(null);
    try {
      if (review.positionId) {
        const latest = await positionsQ.refetch();
        const position = latest.data?.find((item) => item.id === review.positionId);
        if (latest.isError || !position?.isOpen || (position.openCount ?? 0) < 1 || position.title !== review.appliedRole) {
          setReview(null);
          setError("Pozisyonun güncel kaydı değişti veya okunamadı. Seçimi yeniden kontrol edin.");
          return;
        }
      }
      const candidate = await createCandidate.mutateAsync(review);
      showToast(candidate.name + " aday havuzuna eklendi.", "success");
      if (candidate.id !== undefined) onCreated(candidate.id);
      onClose();
    } catch (e) {
      setError(e instanceof ApiError ? e.message : "Aday eklenemedi.");
    }
  };

  return (
    <div className="modal-overlay" style={{ display: "flex" }}>
      <div className="modal-card scale-in">
        <div className="modal-head">
          <div>
            <h3>Yeni Aday</h3>
            <p>Aday havuzuna manuel kayıt ekleyin.</p>
          </div>
          <button className="btn-icon-sm" onClick={onClose} title="Kapat" aria-label="Aday penceresini kapat">
            <i aria-hidden="true" className="fa-solid fa-xmark" />
          </button>
        </div>
        <div className="modal-body-scroll">
          {error && <p className="form-error" role="alert">{error}</p>}
          <section className="ez-candidate-create-guide" aria-label="Aday kaydı rehberi">
            <p><strong>Ne yapıyoruz?</strong> Başvuruyu aday havuzuna kaydediyoruz. Aday “Yeni” aşamasında açılır; bu işlem çalışan kaydı oluşturmaz veya adaya mesaj göndermez.</p>
            <p><strong>Neden kontrol ediyoruz?</strong> Kimlik, başvurulan rol ve değerlendirme puanı sonraki aşama kararlarında görünür. Örneğin farklı bir pozisyon için yapılan başvuruyu yanlış role kaydetmek değerlendirmeyi yanıltır.</p>
            <p><strong>Puanın kaynağı:</strong> Buradaki 0-100 puanı siz girersiniz; sistem AI ile hesaplamaz. Genel puanın dayanağı henüz yoksa alanı boş bırakın. Boş alan “Henüz puan verilmedi”, 0 ise verilmiş sıfır puandır. Not veya ölçüt değerlendirmesi genel puanı otomatik hesaplamaz.</p>
            <p>Açık pozisyon seçerseniz aday o kadroya bağlanır ve rol adı pozisyondan alınır. İlan yayımlamadan gelen başvuruyu pozisyon bağlantısı olmadan da kaydedebilirsiniz.</p>
            <GuidanceDecisionCheck process="recruitment" step="candidate-create" operationName="aday işlemi" />
          </section>
          <div className="form-grid-2">
            <div className="input-group">
              <label className="input-label" htmlFor="cand-name">Ad Soyad</label>
              <input id="cand-name" className="input-control" value={form.name} onChange={set("name")} />
            </div>
            <div className="input-group">
              <label className="input-label" htmlFor="cand-position">Açık pozisyon kaydı</label>
              <select id="cand-position" className="input-control" value={form.positionId}
                onChange={(e) => {
                  const position = positionsQ.data?.find((item) => String(item.id) === e.target.value);
                  setForm((current) => ({ ...current, positionId: e.target.value, appliedRole: position?.title ?? "" }));
                  setReview(null);
                  setError(null);
                }}>
                <option value="">Pozisyona bağlama</option>
                {(positionsQ.data ?? []).filter((position) => position.isOpen && (position.openCount ?? 0) > 0).map((position) => (
                  <option key={position.id} value={position.id}>{position.title} · {position.openCount} açık kadro</option>
                ))}
              </select>
              {positionsQ.isError && <small role="alert">Pozisyon listesi yüklenemedi; adayı yalnız rol adıyla kaydedebilirsiniz.</small>}
            </div>
            <div className="input-group">
              <label className="input-label" htmlFor="cand-role">Başvurulan rol</label>
              <input id="cand-role" className="input-control" value={form.appliedRole} onChange={set("appliedRole")} disabled={!!form.positionId} />
            </div>
            <div className="input-group">
              <label className="input-label" htmlFor="cand-score">Elle verilen aday puanı (0-100)</label>
              <input id="cand-score" type="number" min="0" max="100" step="1" className="input-control" value={form.score} onChange={set("score")} />
              <small>İsteğe bağlı. Henüz puan vermediyseniz boş bırakın; 0 yazmayın.</small>
            </div>
            <div className="input-group">
              <label className="input-label" htmlFor="cand-exp">Deneyim (yıl)</label>
              <input id="cand-exp" type="number" min="0" max="60" step="1" className="input-control" value={form.experienceYears} onChange={set("experienceYears")} />
            </div>
            <div className="input-group">
              <label className="input-label" htmlFor="cand-location">Lokasyon</label>
              <input id="cand-location" className="input-control" value={form.location} onChange={set("location")} />
            </div>
            <div className="input-group">
              <label className="input-label" htmlFor="cand-skills">Yetenekler (virgülle)</label>
              <input id="cand-skills" className="input-control" value={form.skills} onChange={set("skills")} />
            </div>
            <div className="input-group">
              <label className="input-label" htmlFor="cand-summary">Başvuru özeti</label>
              <textarea id="cand-summary" className="input-control" rows={3} value={form.summary} onChange={set("summary")} />
            </div>
          </div>
          {review && <section className="ez-candidate-create-review" aria-labelledby="candidate-create-review-title" aria-live="polite">
            <h4 id="candidate-create-review-title">Kaydetmeden önce son kontrol</h4>
            <p><strong>Aday:</strong> {review.name} · <strong>Başvurulan rol:</strong> {review.appliedRole}</p>
            <p><strong>Pozisyon bağlantısı:</strong> {review.positionId ? selectedPosition?.title || "Seçilen pozisyon" : "Bağlanmadı"}</p>
            <p><strong>Elle verilen puan:</strong> {formatCandidateScore(review.score)} · <strong>Deneyim:</strong> {review.experienceYears} yıl · <strong>Lokasyon:</strong> {review.location || "Belirtilmedi"}</p>
            <p><strong>Yetenekler:</strong> {review.skills?.length ? review.skills.join(", ") : "Belirtilmedi"}</p>
            <p><strong>Başvuru özeti:</strong> {review.summary || "Belirtilmedi"}</p>
            <p><strong>Sonuç:</strong> Aday “Yeni” aşamasında açılacak. Pozisyon bağlantısını ve aynı kişinin zaten kayıtlı olup olmadığını kontrol edin.</p>
            <p>İşe alınmamış adayın temel bilgileri sonradan gerekçeli düzeltmeyle değiştirilebilir. İşe alınmış başvuru kilitlidir; çalışan kartı ayrı düzeltilir. Aday silme işlemi sunulmuyor.</p>
          </section>}
        </div>
        <div className="modal-footer">
          <button className="btn btn-ghost" onClick={onClose}>Vazgeç</button>
          {review && <button className="btn btn-ghost" onClick={() => setReview(null)}>Bilgileri düzelt</button>}
          <button className="btn btn-primary" onClick={review ? submit : prepareReview} disabled={createCandidate.isPending}>
            <i aria-hidden="true" className="fa-solid fa-check" /> {review ? "Adayı oluştur" : "Bilgileri kontrol et"}
          </button>
        </div>
      </div>
    </div>
  );
}
