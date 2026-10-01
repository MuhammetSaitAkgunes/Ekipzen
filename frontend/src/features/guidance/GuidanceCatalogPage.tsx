import { useState } from "react";
import { Link, useSearchParams } from "react-router-dom";
import { useAuth } from "../../auth/AuthContext";
import { appPath } from "../../paths";
import type { Role } from "../../routes";
import { PageError, PageLoading } from "../shared/PageState";
import { guidanceSourceDrafts, guidanceSourceResearchDate } from "./sourceDrafts";
import { guidanceStepDrafts, guidanceStepTitles } from "./stepDrafts";
import { guidanceProcessPath } from "./processNavigation";
import { GuidanceReadingJourney } from "./GuidanceReadingJourney";
import { processTitles, sourceTitles, useGuidanceCatalog, useGuidanceCatalogVersion, useGuidanceEditorial,
  useGuidanceEditorialAccess, useSaveGuidanceContent, useGuidanceContentReadings, useRecordGuidanceReading,
  type GuidanceContent, type GuidanceContentBody, type GuidanceEditorial } from "./catalogQueries";
import "./GuidanceCatalogPage.css";

type TextField = Exclude<keyof GuidanceContentBody, "title" | "audience" | "sourceKind" | "effectiveFrom" | "effectiveTo" | "stepKey" | "practice">;
const fields: [TextField, string][] = [
  ["learningGoal", "Öğrenme hedefi"], ["prerequisites", "Başlamadan önce"], ["action", "Ne yapacağız?"],
  ["why", "Neden yapacağız?"], ["example", "Ayrıntılı örnek"], ["counterExample", "Hatalı örnek"],
  ["terms", "Terimler"], ["outcome", "İşlem sonucu"], ["completionEvidence", "Tamamlanma kanıtı"],
  ["nextStep", "Sonraki adım"], ["correction", "Hata düzeltme yolu"], ["expertEscalation", "Ne zaman uzmana danışmalı?"],
  ["sourceReference", "Kaynak bağlantısı veya belge referansı"], ["provision", "İlgili hüküm / dayanak"],
  ["applicability", "Hangi koşullarda uygulanır?"],
];
const roles: [Role, string][] = [["hr-admin", "İK yöneticisi"], ["manager", "Ekip yöneticisi"], ["employee", "Çalışan"]];
const stateTitles = { draft: "Taslak", "in-review": "İncelemede", published: "Yayımlanmış", retired: "Yayından kaldırılmış" };
const eventTitles: Record<string, string> = { created: "Taslak oluşturuldu", updated: "Taslak düzenlendi",
  submit: "İncelemeye gönderildi", return: "Gerekçeyle taslağa döndü", publish: "Yayımlandı", retire: "Yayından kaldırıldı" };
const today = () => new Intl.DateTimeFormat("sv-SE", { timeZone: "Europe/Istanbul" }).format(new Date());
const blankBody = (): GuidanceContentBody => ({
  title: "", audience: ["hr-admin"], learningGoal: "", prerequisites: "", action: "", why: "", example: "",
  counterExample: "", terms: "", outcome: "", completionEvidence: "", nextStep: "", correction: "",
  expertEscalation: "", sourceKind: "good-practice", sourceReference: "", provision: "", applicability: "",
  effectiveFrom: today(), effectiveTo: null,
});

function ContentArticle({ content }: { content: GuidanceContent }) {
  const { user } = useAuth();
  const processPath = guidanceProcessPath(content.processKey, user?.role);
  const body = content.body;
  const sourceUrl = /^https:\/\//i.test(body.sourceReference) ? body.sourceReference : null;
  return <article className="ez-panel ez-guidance-article">
    <span className="ez-overline">{processTitles[content.processKey]} · Sürüm {content.contentVersion} · {sourceTitles[body.sourceKind]}</span>
    <h2>{body.title}</h2>
    {body.stepKey && <p>Alt adım: {guidanceStepTitles[content.processKey]?.[body.stepKey] ?? body.stepKey}</p>}
    {!content.isCurrent && <p role="status">Bu sürüm şu anda güncel rehber olarak kullanılmıyor. Tarihî, ileri tarihli veya inceleme süresi geçmiş olabilir; işlem öncesinde güncel içeriği kontrol edin.</p>}
    <p>Yürürlük: {body.effectiveFrom}{body.effectiveTo ? ` — ${body.effectiveTo}` : " itibarıyla"}</p>
    {body.sourceKind === "company-policy" && <p>Bu merkezî içerik bir politika örneğidir. Kendi şirketinizde yürürlükte olan politikayı ayrıca doğrulayın.</p>}
    {fields.filter(([key]) => body[key]).map(([key, title]) => <section key={key}>
      <h3>{title}</h3>
      {key === "sourceReference" && sourceUrl ? <a href={sourceUrl} target="_blank" rel="noopener noreferrer">{body[key]}</a> : <p>{body[key]}</p>}
    </section>)}
    {body.practice && <section aria-label="Yayın incelemesindeki alıştırma"><h3>İnceleme kapsamındaki alıştırma</h3>
      <p>{body.practice.question}</p><ol>{body.practice.choices.map((choice, index) => <li key={index}>{choice}{index === body.practice!.correctChoice ? " · Doğru seçenek" : ""}</li>)}</ol>
      <p>{body.practice.explanation}</p><p>Soru, seçenekler, doğru yanıt ve açıklama bu içerik sürümünün yayın onayına dahildir.</p></section>}
    <p>Uzman incelemesi: {content.expertName} · {content.expertRole} · {content.reviewedOn}. Yeniden inceleme: {content.reviewDueOn}.</p>
    <p>Rehberi okumak bir işlemi tamamlamaz veya öğrenme yeterliliğini onaylamaz. Gerçek sonucu ilgili işlem ekranından doğrulayın.</p>
    <ReadingAcknowledgement key={content.id} content={content} />
    {content.isCurrent && processPath && <Link className="btn btn-secondary" to={appPath(processPath)}>İlgili süreç ekranını aç</Link>}
  </article>;
}

function ReadingAcknowledgement({ content }: { content: GuidanceContent }) {
  const { user } = useAuth();
  const readings = useGuidanceContentReadings(user?.id);
  const mutation = useRecordGuidanceReading();
  const [confirmed, setConfirmed] = useState(false);
  const saved = readings.data?.find(item => item.contentRevisionId === content.id) ?? mutation.data;
  return <section aria-label="İçerik inceleme beyanı">
    {saved ? <p role="status">Bu sürümü incelediğinizi {new Date(saved.firstReadAtUtc).toLocaleString("tr-TR")} tarihinde kaydettiniz.</p>
      : !content.isCurrent ? <p>Güncel olmayan sürüm için yeni inceleme beyanı kaydedilmez.</p>
      : <>
        <label><input type="checkbox" checked={confirmed} onChange={event => setConfirmed(event.target.checked)} />Bu içerik sürümünü inceledim; bu beyanın iş tamamlanması veya yeterlilik onayı olmadığını anlıyorum.</label>
        <button className="btn btn-primary" disabled={!confirmed || mutation.isPending || readings.isPending || readings.isError}
          onClick={() => mutation.mutate(content.id)}>İnceleme beyanımı kaydet</button>
      </>}
    {readings.isError && <p role="alert">Önceki inceleme kayıtları yüklenemedi. <button className="btn btn-secondary" onClick={() => void readings.refetch()}>İnceleme kaydını tekrar yükle</button></p>}
    {mutation.isError && <p role="alert">{mutation.error.message}</p>}
  </section>;
}

function ContentForm({ initial, identity, onSaved, onDirtyChange }: {
  initial: GuidanceContentBody; identity: { id?: number; revision?: number; contentKey?: string; processKey?: string; basedOnId?: number };
  onSaved: (entry: GuidanceEditorial) => void;
  onDirtyChange?: (dirty: boolean) => void;
}) {
  const [body, setBody] = useState(initial);
  const [contentKey, setContentKey] = useState(identity.contentKey ?? "");
  const [processKey, setProcessKey] = useState(identity.processKey ?? "company");
  const mutation = useSaveGuidanceContent();
  const setField = (key: keyof GuidanceContentBody, value: unknown) => {
    onDirtyChange?.(true); setBody(prev => ({ ...prev, [key]: value }));
  };
  return <form className="ez-guidance-content-form" onSubmit={async event => {
    event.preventDefault();
    try {
      const result = await mutation.mutateAsync({ path: identity.id ? `/${identity.id}` : "", method: identity.id ? "PUT" : "POST",
        body: identity.id ? { expectedRevision: identity.revision, body } : { contentKey, processKey, basedOnId: identity.basedOnId ?? null, body } });
      onSaved(result);
    } catch { /* Form ve kullanıcının girişi korunur; API hatası aşağıda gösterilir. */ }
  }}>
    <label>İçerik kimliği<input value={contentKey} disabled={!!identity.contentKey} required maxLength={64}
      pattern="[a-z0-9]+(-[a-z0-9]+)*" onChange={event => setContentKey(event.target.value)} /></label>
    {!identity.contentKey && <p>Küçük harf ve tireyle sabit bir kimlik seçin; örneğin calisan-karti-ilk-kontrol.</p>}
    <label>Süreç<select value={processKey} disabled={!!identity.processKey} onChange={event => { setProcessKey(event.target.value); setField("stepKey", null); setField("practice", null); }}>
      {Object.entries(processTitles).map(([key, title]) => <option value={key} key={key}>{title}</option>)}
    </select></label>
    <label>Alt adım (isteğe bağlı)<select value={body.stepKey ?? ""} disabled={!!identity.contentKey} onChange={event => {
      const step = event.target.value; setField("stepKey", step || null); setField("practice", null);
      if (step) setContentKey(`${processKey}-${step}`);
    }}><option value="">Süreç içeriği</option>{Object.entries(guidanceStepTitles[processKey] ?? {}).map(([key, title]) => <option key={key} value={key}>{title}</option>)}</select></label>
    {body.stepKey && <fieldset><legend>Sürümlü alıştırma · yayın incelemesinin parçası</legend>
      <label className="ez-guidance-inline"><input type="checkbox" checked={!!body.practice} onChange={event => setField("practice", event.target.checked
        ? { question: "", choices: ["", "", ""], correctChoice: 0, explanation: "" } : null)} />Bu alt adıma alıştırma ekle</label>
      {body.practice && <>
        <label>Alıştırma sorusu<textarea maxLength={2000} value={body.practice.question} onChange={event => setField("practice", { ...body.practice!, question: event.target.value })} /></label>
        {body.practice.choices.map((choice, index) => <label key={index}>Seçenek {index + 1}<textarea maxLength={1000} value={choice} onChange={event => setField("practice", {
          ...body.practice!, choices: body.practice!.choices.map((item, i) => i === index ? event.target.value : item) })} /></label>)}
        <label>Doğru seçenek<select value={body.practice.correctChoice} onChange={event => setField("practice", { ...body.practice!, correctChoice: Number(event.target.value) })}>
          {body.practice.choices.map((_, index) => <option key={index} value={index}>Seçenek {index + 1}</option>)}</select></label>
        <label>Yanıt açıklaması<textarea maxLength={4000} value={body.practice.explanation} onChange={event => setField("practice", { ...body.practice!, explanation: event.target.value })} /></label>
      </>}
    </fieldset>}
    <label>İçerik başlığı<input value={body.title} required maxLength={200} onChange={event => setField("title", event.target.value)} /></label>
    <fieldset><legend>İçeriği görebilecek roller</legend>{roles.map(([role, name]) => <label className="ez-guidance-inline" key={role}>
      <input type="checkbox" checked={body.audience.includes(role)} onChange={event => setField("audience", event.target.checked
        ? [...body.audience, role] : body.audience.filter(item => item !== role))} />{name}</label>)}</fieldset>
    <label>Dayanak türü<select value={body.sourceKind} onChange={event => setField("sourceKind", event.target.value)}>
      {Object.entries(sourceTitles).map(([key, title]) => <option value={key} key={key}>{title}</option>)}
    </select></label>
    {fields.map(([key, title]) => <label key={key}>{title}<textarea value={body[key]} rows={3}
      maxLength={key === "sourceReference" || key === "provision" || key === "learningGoal" ? 2000 : 4000}
      onChange={event => setField(key, event.target.value)} /></label>)}
    <label>Yürürlük başlangıcı<input type="date" value={body.effectiveFrom} required onChange={event => setField("effectiveFrom", event.target.value)} /></label>
    <label>Yürürlük bitişi (isteğe bağlı)<input type="date" value={body.effectiveTo ?? ""} onChange={event => setField("effectiveTo", event.target.value || null)} /></label>
    <p>Taslak eksik alanlarla kaydedilebilir. İncelemeye gönderirken hedef, hazırlık, işlem, gerekçe, örnekler, sonuç, kanıt, düzeltme, uzman koşulu ve kaynak tamamlanmış olmalı.</p>
    {mutation.isError && <p role="alert">{mutation.error.message}</p>}
    <button className="btn btn-primary" type="submit" disabled={mutation.isPending}>{mutation.isPending ? "Kaydediliyor…" : "Taslağı kaydet"}</button>
  </form>;
}

function EditorialEntry({ entry, onSaved, onFork }: {
  entry: GuidanceEditorial; onSaved: (entry: GuidanceEditorial) => void; onFork: () => void;
}) {
  const mutation = useSaveGuidanceContent();
  const [reason, setReason] = useState("");
  const [draftDirty, setDraftDirty] = useState(false);
  const [review, setReview] = useState({ name: "", role: "", reference: "", reviewedOn: today(), reviewDueOn: "", confirmed: false });
  const transition = async (action: string) => {
    try { onSaved(await mutation.mutateAsync({ path: `/${entry.content.id}/${action}`, method: "POST",
      body: { expectedRevision: entry.revision, reason: reason || null, review: action === "publish" ? review : null } })); }
    catch { /* Yenilemeden ve tekrar incelemeden eski onay otomatik gönderilmez. */ }
  };
  return <section className="ez-panel ez-guidance-editorial">
    <h2>{entry.content.body.title} · Sürüm {entry.content.contentVersion}</h2>
    <p>{stateTitles[entry.content.state]} · Hazırlayanlar: {entry.contributors.map(item => item.name).join(", ")}</p>
    {entry.content.state === "draft" ? <>
      <ContentForm key={`${entry.content.id}:${entry.revision}`} initial={entry.content.body}
        identity={{ id: entry.content.id, revision: entry.revision, contentKey: entry.content.contentKey, processKey: entry.content.processKey }} onSaved={onSaved} onDirtyChange={setDraftDirty} />
      <p>İncelemeye göndermek ekrandaki kaydedilmemiş değişiklikleri kaydetmez. Önce taslağı kaydedin; kaydedilmiş sürümü göndereceksiniz.</p>
      <button className="btn btn-secondary" disabled={mutation.isPending || draftDirty} onClick={() => void transition("submit")}>Kaydedilmiş taslağı incelemeye gönder</button>
    </> : <ContentArticle content={entry.content} />}
    {entry.content.state === "in-review" && <>
      <p>İncelemedeki metin değiştirilemez. Düzenlemek için gerekçeyle taslağa döndürün. Hazırlayan ve metni düzenleyen kişiler bu sürümü yayımlayamaz.</p>
      {entry.canPublish ? <form className="ez-guidance-content-form" onSubmit={event => { event.preventDefault(); void transition("publish"); }}>
        <h3>Uzman incelemesi ve yayın onayı</h3>
        <label>Uzmanın adı<input required maxLength={200} value={review.name} onChange={event => setReview(prev => ({ ...prev, name: event.target.value }))} /></label>
        <label>Uzmanlık / rol<input required maxLength={200} value={review.role} onChange={event => setReview(prev => ({ ...prev, role: event.target.value }))} /></label>
        <label>İnceleme belgesi referansı<textarea required maxLength={2000} value={review.reference} onChange={event => setReview(prev => ({ ...prev, reference: event.target.value }))} /></label>
        <label>Uzman inceleme tarihi<input type="date" required value={review.reviewedOn} onChange={event => setReview(prev => ({ ...prev, reviewedOn: event.target.value }))} /></label>
        <label>Yeniden inceleme tarihi<input type="date" required value={review.reviewDueOn} onChange={event => setReview(prev => ({ ...prev, reviewDueOn: event.target.value }))} /></label>
        <label className="ez-guidance-inline"><input type="checkbox" required checked={review.confirmed} onChange={event => setReview(prev => ({ ...prev, confirmed: event.target.checked }))} />Uzman incelemesi bu içerik sürümünü ve dayanaklarını kapsıyor.</label>
        <button className="btn btn-primary" disabled={mutation.isPending}>İncelenen sürümü yayımla</button>
      </form> : <p>Bu sürümü hazırladığınız için başka bir yetkili Ekipzen İK yöneticisi yayımlamalı.</p>}
    </>}
    {(entry.content.state === "in-review" || entry.content.state === "published") && <>
      <label className="ez-guidance-reason">İşlem gerekçesi<textarea maxLength={2000} value={reason} onChange={event => setReason(event.target.value)} /></label>
      <button className="btn btn-secondary" disabled={mutation.isPending || !reason.trim()} onClick={() => void transition(entry.content.state === "in-review" ? "return" : "retire")}>
        {entry.content.state === "in-review" ? "Gerekçeyle taslağa döndür" : "Gerekçeyle yayından kaldır"}</button>
    </>}
    {(entry.content.state === "published" || entry.content.state === "retired") && <button className="btn btn-secondary" onClick={onFork}>Bu kaydı temel alan yeni sürüm hazırla</button>}
    {mutation.isError && <p role="alert">{mutation.error.message}</p>}
    {entry.expertReference && <p>Uzman inceleme belgesi: {entry.expertReference}</p>}
    <details><summary>İnceleme ve sürüm geçmişi</summary><ol>{entry.events.map(event => <li key={event.revision}>
      {eventTitles[event.action] ?? event.action} · {event.actorName} · {new Date(event.atUtc).toLocaleString("tr-TR")}{event.reason && ` · ${event.reason}`}
    </li>)}</ol></details>
  </section>;
}

export function GuidanceCatalogPage() {
  const { user } = useAuth();
  const [params, setParams] = useSearchParams();
  const versionId = /^\d+$/.test(params.get("version") ?? "") ? Number(params.get("version")) : null;
  const processFilter = params.get("process") ?? "";
  const catalog = useGuidanceCatalog(user?.role);
  const version = useGuidanceCatalogVersion(versionId, user?.role);
  const access = useGuidanceEditorialAccess(user?.id);
  const [manage, setManage] = useState(false);
  const [selected, setSelected] = useState<GuidanceEditorial | null>(null);
  const [creation, setCreation] = useState<{ body: GuidanceContentBody; contentKey?: string; processKey?: string; basedOnId?: number } | null>(null);
  const [message, setMessage] = useState("");
  const [reloadSequence, setReloadSequence] = useState(0);
  const editorial = useGuidanceEditorial(manage && access.data?.canManage === true, user?.id);
  const selectedEntry = selected;
  const saved = (entry: GuidanceEditorial) => { setCreation(null); setSelected(entry); setMessage("İçerik durumu sunucuda kaydedildi."); };
  return <div className="ez-guidance-catalog">
    <div className="page-header"><div><span className="ez-overline">İK REHBERİ</span><h1>Rehber içerikleri</h1>
      <p>Süreçlerin gerekçelerini, örneklerini ve incelenmiş dayanaklarını okuyun.</p></div>
      {user?.role !== "employee" && <Link className="btn btn-secondary" to={appPath("/guidance")}>Öğrenme yolculuğum</Link>}
    </div>
    {access.data?.canManage && <button className="btn btn-secondary" onClick={() => setManage(prev => !prev)}>{manage ? "Okur görünümüne dön" : "İçerik yayınını yönet"}</button>}
    {!manage && <>
      {catalog.isPending ? <PageLoading /> : catalog.isError ? <PageError error={catalog.error} /> : <>
        <label>Süreç filtresi<select value={processFilter} onChange={event => setParams(event.target.value ? { process: event.target.value } : {})}>
          <option value="">Tüm süreçler</option>{Object.entries(processTitles).map(([key, title]) => <option key={key} value={key}>{title}</option>)}
        </select></label>
        {!!processFilter && !processTitles[processFilter] && <p role="alert">Bu süreç bulunamadı. Tüm süreçler seçeneğine dönün.</p>}
        {!catalog.data.some(item => !processFilter || item.processKey === processFilter) && <p>Rolünüz ve seçili süreç için yayımlanmış ve incelemesi güncel bir içerik henüz yok. Bu durum süreçte yapılacak iş olmadığı anlamına gelmez.</p>}
        <ul>{catalog.data.filter(item => !processFilter || item.processKey === processFilter).map(content => <li key={content.id}><button className="btn btn-secondary" onClick={() => setParams({ ...(processFilter ? { process: processFilter } : {}), version: String(content.id) })}>
          {processTitles[content.processKey]} · {content.body.title} · Sürüm {content.contentVersion}</button></li>)}</ul>
      </>}
      {versionId !== null && (version.isPending ? <PageLoading /> : version.isError ? <PageError error={version.error} /> : <ContentArticle content={version.data} />)}
      <GuidanceReadingJourney />
    </>}
    {manage && access.data?.canManage && <>
      <p>Merkezî yayın Ekipzen ekibince yönetilir. Müşteri şirketlerinin İK rolü yayın yetkisi vermez. Taslak ve inceleme kayıtları okurlara açılmaz.</p>
      <button className="btn btn-primary" onClick={() => { setCreation({ body: blankBody() }); setSelected(null); setMessage(""); }}>Yeni içerik taslağı</button>
      <details><summary>On süreç için kaynaklı başlangıç taslakları</summary>
        <p>Kaynak araştırması: {guidanceSourceResearchDate}. Bu başlangıç metinleri henüz uzman onaylı değildir. Kaynak, koşul, yürürlük ve örnekleri inceleyin; kaydetmek yayınlamaz. Mevcut içerikte yeni sürüm akışını kullanın.</p>
        <ul>{guidanceSourceDrafts.map(draft => <li key={draft.contentKey}>
          <button className="btn btn-secondary" disabled={!editorial.isSuccess || editorial.data.some(item => item.content.contentKey === draft.contentKey)}
            onClick={() => { setCreation({ ...draft, body: { ...draft.body, audience: [...draft.body.audience] } }); setSelected(null); setMessage(""); }}>
            {processTitles[draft.processKey]} başlangıç taslağını hazırla</button>
        </li>)}</ul>
      </details>
      <details><summary>Alt adım ve alıştırma taslakları</summary>
        <p>İşe girişte altı, işe alımda iki ve izin kararında iki alt adım. Metin, kaynak ve soru birlikte bağımsız incelemeye sunulur; hazırlamak veya kaydetmek yayımlamaz.</p>
        <ul>{guidanceStepDrafts.map(draft => <li key={draft.contentKey}><button type="button" className="btn btn-secondary"
          disabled={!editorial.isSuccess || editorial.data.some(item => item.content.contentKey === draft.contentKey)}
          onClick={() => { setCreation({ ...draft, body: structuredClone(draft.body) }); setSelected(null); setMessage(""); }}>
          {processTitles[draft.processKey]} · {draft.body.title} taslağını hazırla</button></li>)}</ul>
      </details>
      {message && <p role="status">{message}</p>}
      {editorial.isPending ? <PageLoading /> : editorial.isError ? <PageError error={editorial.error} /> : <ul>{editorial.data?.map(entry => <li key={entry.content.id}>
        <button className="btn btn-secondary" onClick={() => { setSelected(entry); setCreation(null); setMessage(""); }}>
          {entry.content.body.title} · Sürüm {entry.content.contentVersion} · {stateTitles[entry.content.state]}</button>
      </li>)}</ul>}
      {creation && <section className="ez-panel"><h2>{creation.basedOnId ? "Yeni içerik sürümü" : "Yeni içerik"}</h2>
        <ContentForm key={creation.basedOnId ?? creation.contentKey ?? "new"} initial={creation.body} identity={creation} onSaved={saved} /></section>}
      {selectedEntry && !creation && <>
        <button className="btn btn-secondary" onClick={async () => {
          const result = await editorial.refetch();
          const current = result.data?.find(entry => entry.content.id === selectedEntry.content.id);
          if (current) { setSelected(current); setReloadSequence(prev => prev + 1); setMessage("Güncel kayıt yüklendi; yeniden inceleyin."); }
        }}>Güncel kaydı yeniden yükle</button>
        <EditorialEntry key={`${selectedEntry.content.id}:${selectedEntry.revision}:${reloadSequence}`} entry={selectedEntry} onSaved={saved}
          onFork={() => { setCreation({ body: selectedEntry.content.body, contentKey: selectedEntry.content.contentKey,
            processKey: selectedEntry.content.processKey, basedOnId: selectedEntry.content.id }); setMessage(""); }} />
      </>}
    </>}
  </div>;
}
