import { useRef, useState } from "react";
import { ApiError } from "../../api/client";
import { useToast } from "../../layout/ToastProvider";
import { usePreviewImport, useImportEmployees, type ImportPreviewDto, type ImportResultDto } from "./queries";

/** Önizleme kayıt açmaz; gerçek sonuç aktarım yanıtıyla ayrıca gösterilir. */
export function ImportModal({ open, onClose }: { open: boolean; onClose: () => void }) {
  const { showToast } = useToast();
  const preview = usePreviewImport();
  const importer = useImportEmployees();
  const previewSequence = useRef(0);
  const importInFlight = useRef(false);
  const [file, setFile] = useState<File | null>(null);
  const [rapor, setRapor] = useState<ImportPreviewDto | null>(null);
  const [sonuc, setSonuc] = useState<ImportResultDto | null>(null);
  const [review, setReview] = useState(false);
  const [sampleChecked, setSampleChecked] = useState(false);
  const [contractChecked, setContractChecked] = useState(false);
  const [partialChecked, setPartialChecked] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [submitFailed, setSubmitFailed] = useState(false);

  if (!open) return null;

  const dosyaSecildi = async (secilen: File | null) => {
    if (importer.isPending) return;
    const sequence = ++previewSequence.current;
    setFile(secilen);
    setRapor(null);
    setSonuc(null);
    setReview(false);
    setSampleChecked(false);
    setContractChecked(false);
    setPartialChecked(false);
    setError(null);
    setSubmitFailed(false);
    if (!secilen) return;
    try {
      const next = await preview.mutateAsync(secilen);
      if (sequence === previewSequence.current) setRapor(next);
    } catch (e) {
      if (sequence === previewSequence.current) {
        setError(e instanceof ApiError ? e.message : "Dosya önizlemesi oluşturulamadı.");
      }
    }
  };

  const raporEksik = Boolean(rapor && (
    [rapor.toplamSatir, rapor.gecerliSatir, rapor.hataliSatir, rapor.mukerrerSatir]
      .some((value) => typeof value !== "number" || !Number.isInteger(value) || value < 0)
    || (rapor.gecerliSatir ?? 0) + (rapor.hataliSatir ?? 0) + (rapor.mukerrerSatir ?? 0) !== rapor.toplamSatir
  ));
  const ozet = rapor && !raporEksik ? {
    toplam: rapor.toplamSatir as number,
    gecerli: rapor.gecerliSatir as number,
    hatali: rapor.hataliSatir as number,
    mukerrer: rapor.mukerrerSatir as number,
    bilinmeyenDepartmanlar: rapor.bilinmeyenDepartmanlar ?? [],
    sorunlar: rapor.sorunlar ?? [],
  } : null;
  const partial = Boolean(ozet && (ozet.hatali > 0 || ozet.mukerrer > 0));
  const aktarilabilir = Boolean(file && ozet && ozet.gecerli > 0 && !preview.isPending && !importer.isPending);
  const onaylandi = aktarilabilir && sampleChecked && contractChecked && (!partial || partialChecked) && !submitFailed;

  const aktar = async () => {
    if (!onaylandi || !file || importInFlight.current) return;
    importInFlight.current = true;
    setError(null);
    try {
      const next = await importer.mutateAsync(file);
      setSonuc(next);
      setReview(false);
      if ((next.olusturulanSatir ?? 0) > 0) {
        showToast(String(next.olusturulanSatir) + " personel kaydı oluşturuldu.", "success");
      }
    } catch (e) {
      setSubmitFailed(true);
      setError((e instanceof ApiError ? e.message + " " : "")
        + "Aktarım sonucu doğrulanamadı. Yeniden göndermeden önce personel listesini kontrol edin; TC numarası boş satırlar tekrarda mükerrer oluşabilir. Kontrolden sonra dosyayı yeniden seçin.");
    } finally {
      importInFlight.current = false;
    }
  };

  const sonucFarkli = Boolean(sonuc && ozet && (
    sonuc.olusturulanSatir !== ozet.gecerli || sonuc.atlananSatir !== ozet.toplam - ozet.gecerli
  ));

  return (
    <div className="fullscreen-modal" style={{ display: "flex" }} role="dialog" aria-label="Excel'den personel içe aktar">
      <section className="card">
        <div className="card-header-clean">
          <div>
            <h4>{sonuc ? "İçe aktarma sonucu" : "Excel'den Personel İçe Aktar"}</h4>
            <p className="text-muted">Önizleme kayıt oluşturmaz. Sonuç, aktarım tamamlandıktan sonra ayrıca gösterilir.</p>
          </div>
          <button className="btn-icon-sm" onClick={onClose} disabled={importer.isPending} aria-label="Kapat">
            <i aria-hidden="true" className="fa-solid fa-xmark" />
          </button>
        </div>

        {!sonuc && <>
          <section className="ez-import-guide" aria-label="İçe aktarma rehberi">
            <strong>Ne yapıyoruz, neden kontrol ediyoruz?</strong>
            <p><a href="/api/employees/import/template">Excel şablonunu indirin</a> ve örnek personel satırını silin. Gerçek çalışanları doldurup dosyayı yükleyin. Önizleme yalnız satırları doğrular; personel kaydetmez.</p>
            <p>Bu şablonda sözleşme türü sütunu yoktur: aktarılan herkes belirsiz süreli olarak kaydedilir. Boş “Durum” hücresi aktif kabul edilir. Bu seçimler gerçek durumla uyuşmuyorsa toplu aktarımı kullanmayın; kayıtları tek tek oluşturun.</p>
            <p>Mükerrerlik kontrolü yalnız TC Kimlik No dolu satırlarda çalışır; boşsa aynı dosyanın tekrarı çift kayıt açabilir. Aktarım temel personel ve istihdam kaydını oluşturur; belge, ücret paketi ve uygulama dışındaki bildirimleri tamamlamaz.</p>
          </section>
          <div className="input-group">
            <label className="input-label" htmlFor="import-file">Excel dosyası (.xlsx)</label>
            <input
              id="import-file"
              className="input-control"
              type="file"
              accept=".xlsx"
              disabled={importer.isPending}
              onChange={(e) => {
                const selected = e.target.files?.[0] ?? null;
                e.target.value = "";
                void dosyaSecildi(selected);
              }}
            />
            {file && <small>Seçilen dosya: {file.name}. Düzeltme yaptıysanız dosyayı yeniden seçin.</small>}
          </div>

          {preview.isPending && <p className="pending-desc" role="status">Dosya doğrulanıyor…</p>}
          {error && <p className="form-error" role="alert">{error}</p>}
          {raporEksik && <p className="form-error" role="alert">Önizleme raporu eksik veya tutarsız; aktarım yapılamaz. Dosyayı yeniden seçin.</p>}

          {ozet && <>
            <div className="kpi-grid">
              <div className="kpi-card"><div className="kpi-content"><span className="kpi-label">Toplam</span><h3 className="kpi-value">{ozet.toplam}</h3></div></div>
              <div className="kpi-card"><div className="kpi-content"><span className="kpi-label">Geçerli</span><h3 className="kpi-value">{ozet.gecerli}</h3></div></div>
              <div className="kpi-card"><div className="kpi-content"><span className="kpi-label">Hatalı</span><h3 className="kpi-value">{ozet.hatali}</h3></div></div>
              <div className="kpi-card"><div className="kpi-content"><span className="kpi-label">Mükerrer</span><h3 className="kpi-value">{ozet.mukerrer}</h3></div></div>
            </div>
            {ozet.gecerli === 0 && <p role="status">Aktarılabilir satır yok. Dosyadaki sorunları düzeltip yeniden yükleyin.</p>}
            {partial && <p className="pending-desc">Bu raporla devam ederseniz yalnız {ozet.gecerli} geçerli satır aktarılır; {ozet.hatali + ozet.mukerrer} satır atlanır. Önizleme son kayıt garantisi değildir; sunucu aktarım anında tekrar doğrular.</p>}
            {ozet.bilinmeyenDepartmanlar.length > 0 && (
              <section className="surface">
                <strong>Sistemde olmayan departmanlar</strong>
                <ul>{ozet.bilinmeyenDepartmanlar.map((departman) => <li key={departman}>{departman}</li>)}</ul>
                <small>Bu departmanları önce oluşturun, sonra dosyayı tekrar yükleyin.</small>
              </section>
            )}
            {ozet.sorunlar.length > 0 && (
              <table className="data-table">
                <thead><tr><th>Satır</th><th>Alan</th><th>Sorun</th></tr></thead>
                <tbody>{ozet.sorunlar.map((sorun, index) => (
                  <tr key={String(sorun.satirNo) + "-" + String(sorun.alan) + "-" + index}>
                    <td>{sorun.satirNo}</td><td>{sorun.alan}</td><td>{sorun.mesaj}</td>
                  </tr>
                ))}</tbody>
              </table>
            )}
            {review && <section className="ez-import-review" role="region" aria-label="Aktarmadan önce kontrol edin">
              <h5>Aktarmadan önce kontrol edin</h5>
              <p><strong>Dosya:</strong> {file?.name} · <strong>Oluşturulabilecek:</strong> {ozet.gecerli} · <strong>Atlanabilecek:</strong> {ozet.toplam - ozet.gecerli}</p>
              <p>Önizleme yalnız sayıları ve sorunları bildirir; hangi geçerli kişilerin kaydedileceğini satır satır göstermez. Emin değilseniz dosyayı düzeltip yeniden yükleyin.</p>
              <label><input type="checkbox" checked={sampleChecked} onChange={(e) => setSampleChecked(e.target.checked)} /> Şablondaki örnek personel satırını sildim ve dosyanın gerçek çalışanlara ait olduğunu kontrol ettim.</label>
              <label><input type="checkbox" checked={contractChecked} onChange={(e) => setContractChecked(e.target.checked)} /> Tüm aktarılacak çalışanların belirsiz süreli sözleşmeyle kaydedilmesini ve boş durumun aktif sayılmasını doğruladım.</label>
              {partial && <label><input type="checkbox" checked={partialChecked} onChange={(e) => setPartialChecked(e.target.checked)} /> Hatalı ve mükerrer satırlar atlanırken yalnız geçerli satırların kaydedilmesini istiyorum.</label>}
              <p>Kaydetme sonrası yanlış kayıtları düzeltmek için personel kartlarını ayrıca incelemek gerekir. Önizleme ve aktarım arasında veri değişirse gerçek sonuç farklı olabilir.</p>
            </section>}
          </>}

          <div className="toolbar-actions">
            <button className="btn btn-secondary" onClick={onClose} disabled={importer.isPending}>Vazgeç</button>
            {review ? <>
              <button className="btn btn-secondary" onClick={() => setReview(false)} disabled={importer.isPending}>Dosyayı düzelt</button>
              <button className="btn btn-primary" onClick={aktar} disabled={!onaylandi}>Kontrol ettim, aktar</button>
            </> : <button className="btn btn-primary" onClick={() => setReview(true)} disabled={!aktarilabilir}>Aktarımı gözden geçir</button>}
          </div>
        </>}

        {sonuc && <section className="ez-import-outcome" role="status">
          <h5>Gerçek aktarım sonucu</h5>
          <p><strong>{sonuc.olusturulanSatir ?? 0}</strong> personel kaydı oluşturuldu; <strong>{sonuc.atlananSatir ?? 0}</strong> satır atlandı.</p>
          {sonucFarkli && <p>Gerçek sonuç önizlemeden farklı. Departman veya mükerrer kayıt durumu değişmiş olabilir; personel listesini ve sonuç raporunu kontrol edin.</p>}
          {(sonuc.sorunlar?.length ?? 0) > 0 && <table className="data-table">
            <thead><tr><th>Satır</th><th>Alan</th><th>Sorun</th></tr></thead>
            <tbody>{sonuc.sorunlar?.map((sorun, index) => (
              <tr key={String(sorun.satirNo) + "-" + String(sorun.alan) + "-" + index}>
                <td>{sorun.satirNo}</td><td>{sorun.alan}</td><td>{sorun.mesaj}</td>
              </tr>
            ))}</tbody>
          </table>}
          <p>Oluşan personel kartlarında sözleşme, işe giriş tarihi ve departmanı doğrulayın; belge ve ücret adımlarını ayrıca tamamlayın. Gerekli dış bildirimleri uygulama dışında takip edin.</p>
          <div className="toolbar-actions">
            {(sonuc.olusturulanSatir ?? 0) === 0 && <button className="btn btn-secondary" onClick={() => { setSonuc(null); setFile(null); setRapor(null); }}>Başka dosya seç</button>}
            <button className="btn btn-primary" onClick={onClose}>Personel listesine dön</button>
          </div>
        </section>}
      </section>
    </div>
  );
}
