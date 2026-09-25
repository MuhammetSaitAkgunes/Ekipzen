import { useState } from "react";
import { ApiError } from "../../api/client";
import { useToast } from "../../layout/ToastProvider";
import { useCreateCandidate, type CreateCandidateCommand } from "./queries";

type CandidateForm = {
  name: string; appliedRole: string; score: string; location: string;
  experienceYears: string; summary: string; skills: string;
};

export function CandidateModal({ onClose, onCreated }: {
  onClose: () => void; onCreated: (id: number) => void;
}) {
  const { showToast } = useToast();
  const createCandidate = useCreateCandidate();
  const [form, setForm] = useState<CandidateForm>({
    name: "", appliedRole: "", score: "", location: "", experienceYears: "0", summary: "", skills: "",
  });
  const [review, setReview] = useState<CreateCandidateCommand | null>(null);
  const [error, setError] = useState<string | null>(null);

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
    if (name.length > 200 || appliedRole.length > 128 || form.summary.trim().length > 2000) {
      setError("Ad en fazla 200, pozisyon 128 ve başvuru özeti 2000 karakter olabilir.");
      return;
    }
    if (!/^(0|[1-9]\d{0,2})$/.test(form.score) || Number(form.score) > 100) {
      setError("Aday puanını 0-100 arasında tam sayı olarak açıkça girin.");
      return;
    }
    if (!/^(0|[1-9]\d{0,2})$/.test(form.experienceYears) || Number(form.experienceYears) > 60) {
      setError("Deneyimi 0-60 arasında tam yıl olarak girin.");
      return;
    }
    setReview({
      name, appliedRole, score: Number(form.score),
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
            <p><strong>Puanın kaynağı:</strong> Buradaki 0-100 puanı siz girersiniz; sistem AI ile hesaplamaz. Değerlendirmeniz yoksa rastgele puan vermeyin. Mevcut kayıt sözleşmesi puansız adayı ayrı gösteremiyor.</p>
          </section>
          <div className="form-grid-2">
            <div className="input-group">
              <label className="input-label" htmlFor="cand-name">Ad Soyad</label>
              <input id="cand-name" className="input-control" value={form.name} onChange={set("name")} />
            </div>
            <div className="input-group">
              <label className="input-label" htmlFor="cand-role">Başvurulan pozisyon</label>
              <input id="cand-role" className="input-control" value={form.appliedRole} onChange={set("appliedRole")} />
            </div>
            <div className="input-group">
              <label className="input-label" htmlFor="cand-score">Elle verilen aday puanı (0-100)</label>
              <input id="cand-score" type="number" min="0" max="100" step="1" className="input-control" value={form.score} onChange={set("score")} />
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
            <p><strong>Elle verilen puan:</strong> {review.score}/100 · <strong>Deneyim:</strong> {review.experienceYears} yıl · <strong>Lokasyon:</strong> {review.location || "Belirtilmedi"}</p>
            <p><strong>Yetenekler:</strong> {review.skills?.length ? review.skills.join(", ") : "Belirtilmedi"}</p>
            <p><strong>Başvuru özeti:</strong> {review.summary || "Belirtilmedi"}</p>
            <p><strong>Sonuç:</strong> Aday “Yeni” aşamasında açılacak. Bu form mevcut pozisyon kaydına bağlantı kurmaz; aynı kişinin zaten kayıtlı olup olmadığını aday listesinde kontrol edin.</p>
            <p>Mevcut API aday kaydını düzenleme veya silme sunmuyor. Hatalı bilgiyi bu aşamada düzeltin.</p>
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
