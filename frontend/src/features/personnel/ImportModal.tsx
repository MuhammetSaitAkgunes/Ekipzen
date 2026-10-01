import { useRef, useState } from "react";
import { ApiError, apiDownload, apiFetch } from "../../api/client";
import { useToast } from "../../layout/ToastProvider";
import { ImportRows, validImportPreview, validImportResult } from "./ImportRows";
import { usePreviewImport, useImportEmployees, type ImportPreviewDto, type ImportResultDto } from "./queries";

/** Önizleme ve sonuç ayrı tutulur; belirsiz yanıtta aynı gönderim kimliği korunur. */
export function ImportModal({ open, onClose }: { open: boolean; onClose: () => void }) {
  const { showToast } = useToast();
  const preview = usePreviewImport();
  const importer = useImportEmployees();
  const previewSequence = useRef(0);
  const importInFlight = useRef(false);
  const requestId = useRef(crypto.randomUUID());
  const [file, setFile] = useState<File | null>(null);
  const [rapor, setRapor] = useState<ImportPreviewDto | null>(null);
  const [sonuc, setSonuc] = useState<ImportResultDto | null>(null);
  const [review, setReview] = useState(false);
  const [sampleChecked, setSampleChecked] = useState(false);
  const [contractChecked, setContractChecked] = useState(false);
  const [partialChecked, setPartialChecked] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [uncertain, setUncertain] = useState(false);
  const [checking, setChecking] = useState(false);
  const [downloading, setDownloading] = useState(false);

  if (!open) return null;
  const busy = importer.isPending || checking;
  const clearChecks = () => { setReview(false); setSampleChecked(false); setContractChecked(false); setPartialChecked(false); };
  const dosyaSecildi = async (secilen: File | null) => {
    if (busy || uncertain) return;
    const sequence = ++previewSequence.current;
    requestId.current = crypto.randomUUID();
    setFile(secilen); setRapor(null); setSonuc(null); clearChecks(); setError(null);
    if (!secilen) return;
    if (!secilen.name.toLowerCase().endsWith(".xlsx") || secilen.size > 5 * 1024 * 1024) {
      setError("En fazla 5 MB boyutunda bir .xlsx dosyası seçin."); return;
    }
    try {
      const next = await preview.mutateAsync(secilen);
      if (sequence === previewSequence.current) setRapor(next);
    } catch (e) {
      if (sequence === previewSequence.current) setError(e instanceof ApiError ? e.message : "Dosya önizlemesi oluşturulamadı.");
    }
  };
  const raporEksik = Boolean(rapor && !validImportPreview(rapor));
  const ozet = rapor && !raporEksik ? rapor : null;
  const partial = Boolean(ozet && (ozet.hataliSatir! > 0 || ozet.mukerrerSatir! > 0));
  const aktarilabilir = Boolean(file && ozet?.previewToken && ozet.gecerliSatir! > 0 && !preview.isPending && !busy);
  const onaylandi = aktarilabilir && sampleChecked && contractChecked && (!partial || partialChecked);

  const showResult = (next: ImportResultDto) => {
    if (!validImportResult(next, requestId.current, rapor)) throw new Error("Aktarım yanıtı eksik veya önizlemeyle tutarsız.");
    setSonuc(next); setReview(false); setUncertain(false); setError(null);
    showToast(String(next.olusturulanSatir) + " personel kaydı oluşturuldu.", "success");
  };
  const kayitliSonucuAc = async () => {
    if (busy) return;
    setChecking(true);
    try {
      const saved = await apiFetch<ImportResultDto | null>("/employees/import/results/" + requestId.current);
      if (saved) showResult(saved);
      else setError("Kayıtlı sonuç henüz bulunamadı. Bu ekranı açık tutun; aynı gönderimi güvenle tekrar deneyebilirsiniz.");
    } catch (e) { setError(e instanceof ApiError ? e.message : "Kayıtlı sonuç sorgulanamadı; gönderim kimliği korundu."); }
    finally { setChecking(false); }
  };
  const aktar = async () => {
    if (!onaylandi || !file || !ozet?.previewToken || importInFlight.current) return;
    importInFlight.current = true; setError(null);
    try {
      showResult(await importer.mutateAsync({ file, importRequestId: requestId.current, expectedPreviewToken: ozet.previewToken }));
    } catch (e) {
      if (e instanceof ApiError && e.status === 409) {
        setUncertain(true);
        try {
          const saved = await apiFetch<ImportResultDto | null>("/employees/import/results/" + requestId.current);
          if (saved) showResult(saved);
          else { setUncertain(false); setRapor(null); clearChecks(); setError(e.message + " Dosyayı yeniden seçin."); }
        } catch { setError("Aktarım sonucu doğrulanamadı. Kayıtlı sonucu sorgulayın; aynı gönderim kimliği korundu."); }
      } else {
        setUncertain(true);
        setError((e instanceof ApiError ? e.message + " " : "") + "Aktarım sonucu doğrulanamadı. Önce kayıtlı sonucu sorgulayın veya aynı gönderimi güvenle tekrar deneyin. Bu ekranı açık tutun.");
      }
    } finally { importInFlight.current = false; }
  };
  const sablonIndir = async () => {
    setDownloading(true); setError(null);
    try {
      const download = await apiDownload("/employees/import/template");
      const url = URL.createObjectURL(download.blob);
      const anchor = document.createElement("a"); anchor.href = url; anchor.download = download.fileName ?? "ekipzen-personel-sablonu.xlsx";
      anchor.click(); setTimeout(() => URL.revokeObjectURL(url), 1000);
    } catch (e) { setError(e instanceof ApiError ? e.message : "Şablon indirilemedi."); }
    finally { setDownloading(false); }
  };

  return <div className="fullscreen-modal ez-import-modal" style={{ display: "flex" }} role="dialog" aria-modal="true" aria-label="Excel'den personel içe aktar">
    <section className="card">
      <div className="card-header-clean"><div>
        <h4>{sonuc ? "İçe aktarma sonucu" : "Excel'den Personel İçe Aktar"}</h4>
        <p className="text-muted">Önizleme kayıt oluşturmaz. Kaydetmeden önce her satırın sonucunu kontrol edin.</p>
      </div><button className="btn-icon-sm" onClick={onClose} disabled={busy || uncertain} aria-label="Kapat"><i aria-hidden="true" className="fa-solid fa-xmark" /></button></div>
      {!sonuc && <>
        <section className="ez-import-guide" aria-label="İçe aktarma rehberi">
          <strong>Ne yapıyoruz, neden kontrol ediyoruz?</strong>
          <p><button className="btn btn-secondary btn-sm" onClick={sablonIndir} disabled={downloading}>{downloading ? "Şablon indiriliyor…" : "Excel şablonunu indirin"}</button> ve örnek personel satırını silin. Tek sayfalı dosyada gerçek çalışanları doldurun (en fazla 1.000 kişi, 5 MB). Önizleme personel kaydetmez.</p>
          <p>Bu şablonda sözleşme ve çalışma şekli sütunu yoktur: aktarılan herkes belirsiz süreli olarak kaydedilir; çalışma şekli tam zamanlıdır. Yalnız aktif personel oluşturulur; boş “Durum” aktif kabul edilir. Diğer sözleşme veya çalışma şekilleri için kayıtları tek tek oluşturun.</p>
          <p>TC doluysa mevcut kayıtlar değişmeden atlanır. TC boşken aynı dosyayı yeni bir aktarım olarak yüklemek çift kayıt açabilir; bağlantı hatasında bu ekrandaki aynı gönderimi tekrar kullanın. Belge, ücret ve dış bildirim adımlarını ayrıca tamamlayın.</p>
        </section>
        <div className="input-group"><label className="input-label" htmlFor="import-file">Excel dosyası (.xlsx)</label>
          <input id="import-file" className="input-control" type="file" accept=".xlsx" disabled={busy || uncertain} onChange={(e) => { const selected = e.target.files?.[0] ?? null; e.target.value = ""; void dosyaSecildi(selected); }} />
          {file && <small>Seçilen dosya: {file.name}. Düzeltme yaptıysanız dosyayı yeniden seçin.</small>}
        </div>
        {preview.isPending && <p role="status">Dosya doğrulanıyor…</p>}
        {error && <p className="form-error" role="alert">{error}</p>}
        {uncertain && <section className="surface"><p>Gönderim: <code>{requestId.current}</code>. Sonuç bulunana kadar dosyayı değiştirmeyin veya ekranı kapatmayın.</p><button className="btn btn-secondary" onClick={kayitliSonucuAc} disabled={busy}>Kayıtlı sonucu sorgula</button></section>}
        {raporEksik && <p className="form-error" role="alert">Önizleme raporu eksik veya tutarsız; aktarım yapılamaz. Dosyayı yeniden seçin.</p>}
        {ozet && <>
          <div className="kpi-grid">{[["Toplam", ozet.toplamSatir], ["Geçerli", ozet.gecerliSatir], ["Hatalı", ozet.hataliSatir], ["Mükerrer", ozet.mukerrerSatir]].map(([label, value]) => <div className="kpi-card" key={label}><div className="kpi-content"><span className="kpi-label">{label}</span><h3 className="kpi-value">{value}</h3></div></div>)}</div>
          {ozet.gecerliSatir === 0 && <p role="status">Aktarılabilir satır yok. Dosyadaki sorunları düzeltip yeniden yükleyin.</p>}
          {partial && <p className="pending-desc">Yalnız {ozet.gecerliSatir} geçerli satır aktarılır; {ozet.hataliSatir! + ozet.mukerrerSatir!} satır atlanır. Kaynak değişirse aktarım durur ve yeniden önizleme gerekir.</p>}
          {ozet.bilinmeyenDepartmanlar!.length > 0 && <section className="surface"><strong>Sistemde olmayan departmanlar</strong><ul>{ozet.bilinmeyenDepartmanlar!.map((department) => <li key={department}>{department}</li>)}</ul><small>Önce departmanı oluşturun, sonra dosyayı yeniden yükleyin.</small></section>}
          {ozet.satirlar!.length > 0 && <ImportRows key={requestId.current} rows={ozet.satirlar!} />}
          {ozet.toplamSatir === 0 && ozet.sorunlar!.map((issue, index) => <p className="form-error" key={index}>Satır {issue.satirNo} · {issue.alan}: {issue.mesaj}</p>)}
          {review && <section className="ez-import-review" role="region" aria-label="Aktarmadan önce kontrol edin"><h5>Aktarmadan önce kontrol edin</h5>
            <p><strong>Dosya:</strong> {file?.name} · <strong>Oluşturulacak:</strong> {ozet.gecerliSatir} · <strong>Atlanacak:</strong> {ozet.toplamSatir! - ozet.gecerliSatir!}</p>
            <label><input type="checkbox" checked={sampleChecked} disabled={busy || uncertain} onChange={(e) => setSampleChecked(e.target.checked)} /> Şablondaki örnek personel satırını sildim ve satır bazındaki gerçek çalışanları kontrol ettim.</label>
            <label><input type="checkbox" checked={contractChecked} disabled={busy || uncertain} onChange={(e) => setContractChecked(e.target.checked)} /> Tüm aktarılacak çalışanların belirsiz süreli sözleşmeyle, tam zamanlı ve aktif kaydedilmesini doğruladım.</label>
            {partial && <label><input type="checkbox" checked={partialChecked} disabled={busy || uncertain} onChange={(e) => setPartialChecked(e.target.checked)} /> Hatalı ve mükerrer satırlar atlanırken yalnız geçerli satırların kaydedilmesini istiyorum.</label>}
            <p>Geçerli satırlar tek işlemde kaydedilir. Oluşan kartlardaki yanlış bilgileri çalışan kartından düzeltin; işten ayrılış için ilgili süreci izleyin.</p>
          </section>}
        </>}
        <div className="toolbar-actions"><button className="btn btn-secondary" onClick={onClose} disabled={busy || uncertain}>Vazgeç</button>
          {review ? <><button className="btn btn-secondary" onClick={() => setReview(false)} disabled={busy || uncertain}>Dosyayı düzelt</button><button className="btn btn-primary" onClick={aktar} disabled={!onaylandi}>{uncertain ? "Aynı gönderimi tekrar dene" : "Kontrol ettim, aktar"}</button></>
            : <button className="btn btn-primary" onClick={() => setReview(true)} disabled={!aktarilabilir}>Aktarımı gözden geçir</button>}
        </div>
      </>}
      {sonuc && <section className="ez-import-outcome"><h5>Gerçek aktarım sonucu</h5>
        <p role="status"><strong>{sonuc.olusturulanSatir}</strong> personel kaydı oluşturuldu; <strong>{sonuc.atlananSatir}</strong> satır atlandı.</p>
        <p>Gönderim: <code>{sonuc.importRequestId}</code>. Aynı gönderimi tekrarlamak yeni kayıt oluşturmaz.</p>
        <ImportRows key={"result-" + requestId.current} rows={sonuc.satirlar!} completed />
        <p>Oluşan kartlarda sözleşme, işe giriş ve departmanı kontrol edin; belge ve ücret adımlarını ayrıca tamamlayın. Atlanan hatalı satırları düzeltip yeni dosyada yalnız eksikleri yükleyin. Dış bildirimleri ayrıca takip edin.</p>
        <div className="toolbar-actions"><button className="btn btn-primary" onClick={onClose}>Personel listesine dön</button><button className="btn btn-secondary" onClick={() => void dosyaSecildi(null)}>Yeni dosya hazırla</button></div>
      </section>}
    </section>
  </div>;
}
