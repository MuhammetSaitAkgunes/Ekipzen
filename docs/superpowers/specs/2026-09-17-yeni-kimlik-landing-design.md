# Ekipzen — Yeni görsel kimlik + çok sayfalı landing (Spec 1 / 2)

## Context

Ekipzen'in arayüzü işlevsel ama tasarımı zayıf. Domaine gelen herkes doğrudan login ekranını görüyor; tanıtım sitesi yok.

Kullanıcıyla netleşen kararlar:

| Konu | Karar |
|---|---|
| Sıralama | Önce ortak görsel dil. Bu spec (1) = yeni kimlik + landing + login. Sayfa sayfa uygulama revizyonu Spec 2'de yapılacak. |
| Kimlik | Tamamen yeni. **A · Derin indigo**: vurgu `#4338CA`, metin slate `#0F172A`, font **Plus Jakarta Sans** (Türkçe destekli). |
| His | "Sade kurumsal güven" (Personio / Deel / Remote / Lattice ailesi). |
| Sayfalar | Ana sayfa, Özellikler, Fiyatlandırma, Hakkımızda, Güvenlik & KVKK, İletişim/Demo. KVKK aydınlatma sayfası yasal zorunluluk olduğu için eklendi. |
| Demo formu | Backend'e gider, platform sahibine **e-posta** atılır. Hesabı kullanıcı manuel açar; self-signup yok (mevcut karar korunur). |
| URL | Aynı domain, temiz URL. Hash router yerine browser router, backend'e SPA fallback. |
| İçerik | Metinleri Claude yazar. Fiyat yerine "Teklif alın". Sahte logo/yorum yok; güven, gerçek özelliklerle kurulur (kiracı başına izole DB, KVKK dışa aktarım, rol bazlı yetki). |
| Görseller | Kodla (HTML/CSS) çizilmiş stilize UI kartları. |
| Altyapı | Düz CSS korunur. Yeni token seti + küçük paylaşılan React bileşen kütüphanesi. Yeni bağımlılık yok. |
| Logo | Basit SVG işaret + yazı logosu, favicon dahil. |
| Uygulamaya etkisi | Token'lar global olarak değişir (uygulama renk/font olarak yeni kimliğe geçer, yapı değişmez); login ve davet ekranları yeniden tasarlanır. |

### Referans analizi (özet)

Deel, Factorial, Remote, Lattice ve Kolay İK'nin ortak deseni:
- Sticky nav: logo, menü, sağda "Giriş" (ghost) ve "Demo iste" (primary).
- Hero: tek net vaat, alt metin, 2 CTA ve ürün görseli.
- Güven şeridi ve modül kartları (ikon + kısa açıklama + görsel).
- Metrik / fark bölümü, güvenlik ve sertifika bloğu, SSS.
- Tekrarlanan demo CTA bandı ve kapsamlı footer.
- Görsel dil: açık zemin, tek vurgu rengi, bol beyaz alan, kart tabanlı düzen.

Uygulamada `ui-ux-pro-max` ve `frontend-design` skill'leri kullanılacak (landing checklist'i, erişilebilirlik, tipografi ölçeği).

---

## Tasarım

### 1. Token sistemi (`frontend/src/styles/tokens.css`, yeni)

- **Ramp'ler:** `--indigo-50..900`, `--slate-0..950`. Semantik renkler: success emerald, warning amber, danger rose, info sky.
- **Amaç katmanı:** `--bg`, `--surface`, `--surface-2`, `--text`, `--text-2`, `--text-3`, `--border`, `--border-strong`, `--primary`, `--primary-hover`, `--primary-soft`, `--on-primary`, `--focus-ring`, ayrıca `--success`, `--warning`, `--danger` ve bunların `*-soft` sürümleri.
- **Tipografi:** `--font-sans: "Plus Jakarta Sans", system-ui…`. Tablolardaki sayılar için `--font-mono: "JetBrains Mono"` (Google Fonts) ve `font-variant-numeric: tabular-nums`.
- **Ölçek:** 12 / 14 / 16 / 18 / 20 / 24 / 30 / 36 / 48 / 60. Kök font 16px'e çekilir (UX raporu B9).
- **Diğer:** spacing 4px grid; radius `--r-sm 6`, `--r-md 10`, `--r-lg 14`, `--r-xl 20`; `--shadow-xs/sm/md/lg` (yumuşak, slate tonlu); motion `--dur-fast 120ms`, `--dur-base 200ms` ve `prefers-reduced-motion` desteği.
- **Karanlık mod:** `:root[data-theme="dark"]` yalnız amaç katmanını yeniden tanımlar. Landing yalnız açık tema kullanır (`.marketing` kökü temayı sabitler); uygulamadaki tema anahtarı çalışmaya devam eder.
- **Geriye uyum:** `main.css`'teki eski değişkenler (`--primary`, `--accent`, `--ink`, `--body`, …) silinmez, yeni token'lara alias olarak bağlanır. Böylece modül CSS'leri kırılmadan yeni kimliğe geçer. Petrol/altın hex hardcode'ları taranıp token'a çevrilir.
- **Font yükleme:** `frontend/index.html`'deki IBM Plex linki Plus Jakarta Sans + JetBrains Mono ile değiştirilir (CSP zaten `fonts.googleapis.com`'a izin veriyor).

### 2. Paylaşılan bileşenler (`frontend/src/ui/`, yeni)

`Button` (primary / secondary / ghost / danger; sm / md / lg; min 44px dokunma hedefi), `Card`, `Badge`, `Input` + `Field` (label, hint, error, aria bağları), `Textarea`, `Select`, `Checkbox`, `Logo` (SVG), `Container`, `Section`.

Stilleri `frontend/src/styles/ui.css` dosyasında, BEM benzeri `ez-` önekiyle. Her bileşen küçük ve testli. Spec 2 bu kütüphaneyi genişletecek.

### 3. Yönlendirme değişikliği

- `frontend/src/App.tsx`: `createHashRouter` yerine `createBrowserRouter`.
- **Eski hash linkleri:** `main.tsx`'te router kurulmadan önce `location.hash` `#/` ile başlıyorsa `history.replaceState` ile yola çevrilir. Böylece e-postalarda gitmiş `/#/accept-invite?...` davetleri ve yer imleri çalışmaya devam eder.
- **Rota ağacı** (`frontend/src/routes.tsx`):

  | Yol | Sayfa |
  |---|---|
  | `/` | Ana sayfa (`MarketingLayout` altında; oturum açık kullanıcıya "Uygulamaya git" CTA'sı gösterilir, zorla yönlendirme yapılmaz) |
  | `/ozellikler` | Özellikler |
  | `/fiyatlandirma` | Fiyatlandırma |
  | `/hakkimizda` | Hakkımızda |
  | `/guvenlik` | Güvenlik & KVKK |
  | `/iletisim` | İletişim / Demo |
  | `/kvkk-aydinlatma` | KVKK aydınlatma metni |
  | `/giris` | Login (`/login` → `/giris` yönlendirmesi) |
  | `/davet` | Davet kabul (`/accept-invite` → `/davet`, query korunur) |
  | `/app/*` | Mevcut tüm uygulama rotaları |
  | `*` | 404 sayfası (marketing layout) |

- **`/app` öneki:** `appRoutes` path'leri `/app` önekini alır (örn. `/app/dashboard`). `roleHomeFor`, `guards.tsx`, `AppShell.tsx`, `RiskCenterPage.tsx`, `BackToRisk.tsx`, `OverviewPage.tsx` ve `PayrollPage.tsx`'teki ~20 mutlak link güncellenir. İlgili testler de güncellenir. Eski kökteki uygulama yolları (`/dashboard` vb.) `/app/...`'e yönlendirilir.
- **Sayfa meta:** küçük bir `usePageMeta(title, description)` hook'u (`document.title` + meta description) ile sayfa başına başlık/açıklama. `index.html`'e OG etiketleri; `public/robots.txt` ve `public/sitemap.xml` eklenir.

### 4. Landing (`frontend/src/marketing/`, yeni)

- **Yapı:** `MarketingLayout` (sticky header + mobil menü, footer), `pages/*.tsx`, `content.ts` (tüm metinler tek dosyada, kolay düzenleme), `visuals/*.tsx` (kodla çizilmiş UI kartları: risk skoru, izin takvimi, bordro özeti, aksiyon listesi). Stiller `frontend/src/styles/marketing.css` dosyasında.
- **Ana sayfa bölümleri:**
  1. Hero: "Ekibinizi tek yerden, güvenle yönetin" + alt metin, "Demo iste" / "Özellikleri incele" CTA'ları ve katmanlı UI kart kompozisyonu.
  2. Güven şeridi: KVKK uyumlu · Kiracı başına izole veritabanı · Rol bazlı yetki · Türkiye bordro mevzuatı.
  3. Modül ızgarası (6 kart): Risk Merkezi, Personel, İzin, Mesai & Puantaj, Bordro, İşe Alım.
  4. "Nasıl çalışır" (3 adım): demo → hesabınız açılır → ekibinizi davet edin.
  5. Öne çıkan: Risk Merkezi (ayrılma / tükenmişlik sinyalleri) + görsel.
  6. Güvenlik bloğu.
  7. SSS (erişilebilir `<details>`).
  8. CTA bandı.
- **Özellikler:** modül başına anchor'lı bölümler (alternatif sol/sağ görsel), rol bazlı kullanım (İK yöneticisi / yönetici / çalışan).
- **Fiyatlandırma:** 3 paket (Başlangıç / Büyüyen / Kurumsal). Rakam yok, "Teklif alın"; özellik karşılaştırma tablosu (mobilde yatay kaydırma) ve SSS.
- **Hakkımızda:** misyon, değerler ve neden Ekipzen. Uydurma kişi ya da rakam kullanılmaz.
- **Güvenlik & KVKK:** mimari güvenceler (izole DB, şifreli bağlantı, HSTS/CSP, rol yetkisi, denetim kaydı, KVKK veri dışa aktarımı). Yalnız kodda gerçekten bulunanlar yazılır.
- **İletişim/Demo:** form alanları ad soyad, iş e-postası, şirket, çalışan sayısı (select), telefon (opsiyonel), mesaj (opsiyonel) ve zorunlu KVKK onay kutusu (aydınlatma linkiyle). Görünmez honeypot alanı eklenir. Başarı ve hata durumları inline gösterilir, başarıda teşekkür ekranı açılır. `@tanstack/react-query` mutation kullanılır, istek `api/client.ts` üzerinden gider.
- **Erişilebilirlik ve duyarlılık:** skip-link, `aria-current` nav, odak halkaları, kontrast AA; 375 / 768 / 1280 kırılımları.
- **İletişim bilgileri:** yer tutucu olarak `content.ts`'te işaretli durur, kullanıcı sonra düzeltir.

### 5. Login ve davet ekranı

- `auth/LoginPage.tsx`: yeni split düzen. Sol tarafta form (logo, "Hesabınıza giriş yapın", `Field` + `Button`, "Hesabınız yok mu? Demo isteyin → /iletisim"). Sağ tarafta indigo zeminli, küçük UI kart kompozisyonu ve güven maddeleri. Mobilde tek kolon. Siteye dönüş linki eklenir.
- `auth/AcceptInvitePage.tsx` aynı kabuğu kullanır. `auth.css` yeni kimliğe göre yeniden yazılır.
- Mevcut davranış (demo kimlik bilgileri dev'de dolu, hata mesajları, yönlendirme) korunur.

### 6. Uygulama kabuğu — yalnız token etkisi

`layout.css` ve `main.css` yeni token'ları kullanır. Sidebar/header yapısı değişmez; logo `Logo` bileşeniyle, FA ikonları olduğu gibi kalır. Görsel regresyon kontrolü: tüm ana sayfalar açık ve koyu temada gezilir. Spec 2'de kabuk ve sayfalar ayrıca ele alınacak.

### 7. Backend — demo talebi

- **Application:** `Features/DemoRequests/Commands/SubmitDemoRequestCommand.cs` (MediatR + FluentValidation: ad ≤100, e-posta formatı, şirket ≤150, çalışan aralığı enum/string whitelist, telefon ≤30, mesaj ≤2000, `KvkkConsent == true`). Handler, `IEmailSender` ile `DemoRequests:NotifyEmail` adresine düz metin e-posta atar (içerik HTML encode edilmez, çünkü düz metin; konu satırında CR/LF temizlenir). DB kaydı yok (kullanıcı kararı).
- **API:** `Controllers/PublicController.cs` → `POST /api/public/demo-requests`, `[AllowAnonymous]`, `[EnableRateLimiting("public-form")]`. Honeypot doluysa sessizce 202 döner. Başarıda 202, doğrulama hatasında mevcut ProblemDetails 400.
- **Rate limit:** `Program.cs`'e yeni `public-form` politikası (IP başına, varsayılan 5/saat, `RateLimiting:PublicFormPermitPerHour`).
- **SPA fallback:** `Program.cs`'te `MapControllers` sonrasına `app.MapFallbackToFile("index.html")` eklenir. `/api/*` ve `/health` hariç tutulur (API 404'leri HTML dönmemeli).
- **Davet linki:** `IdentityService.cs:468` link biçimi `{appUrl}/davet?email=…&token=…` olur.
- **Yapılandırma:** `appsettings.json`'a `DemoRequests:NotifyEmail` eklenir. `ProductionConfiguration.GetErrors` üretimde bu alanı zorunlu ve geçerli e-posta olarak doğrular.
- `npm run gen:api` ile `schema.d.ts` yenilenir.

### 8. Dokümantasyon

- Spec, `docs/superpowers/specs/2026-09-17-yeni-kimlik-landing-design.md` olarak repoya yazılır ve commitlenir.
- `docs/rehberler/04-frontend-derinlemesine.md` router, token ve `ui/` bölümleriyle güncellenir.
- Merge sonrası, canonical checkout'tan Project Memory'de UI conventions (token/palet) girdisi güncellenir. Worktree'den memory yazılmaz. Kullanıcının otomatik hafızasındaki "petrol/evergreen" girdisi de yeni kimliğe göre güncellenir.

---

## Uygulama sırası (küçük, gözden geçirilebilir commitler)

1. Spec dokümanı → commit. Ardından `superpowers:writing-plans` ile ayrıntılı görev planı.
2. `tokens.css` + font + eski değişken alias'ları → uygulama yeni renklerle açılır.
3. `ui/` bileşenleri + testler.
4. Browser router + `/app` öneki + hash uyumluluğu + yönlendirmeler + testler.
5. Backend: SPA fallback, davet linki, demo talebi uç noktası + testler.
6. Marketing layout + ana sayfa.
7. Özellikler, Fiyatlandırma, Hakkımızda, Güvenlik, İletişim (form bağlantısı), KVKK, 404.
8. Login ve davet yeniden tasarımı.
9. Uygulama içi token regresyon düzeltmeleri, meta/robots/sitemap, doküman.

Her batch sonunda AGENTS.md'ye uygun senkronizasyon onayı istenecek (refs, commitler, dosyalar, doğrulama sonuçları).

## Doğrulama

- **Frontend:** `npm --prefix frontend run lint`, `npm --prefix frontend test` (mevcut testler + yeni bileşen, router ve form testleri), `npm --prefix frontend run build`.
- **Backend:** `dotnet test backend/Ekipzen.sln`. Yeni testler:
  - demo talebi → outbox'a e-posta düşer
  - doğrulama 400
  - KVKK onayı yok → 400
  - honeypot → 202 ve e-posta yok
  - rate limit → 429
  - fallback `/ozellikler` → index.html
  - `/api/olmayan` → 404 JSON
  - davet linki biçimi
- **Tarayıcı** (`ekipzen-api` + `ekipzen-frontend` launch config'leri, in-app browser):
  - `/` landing'i gösterir, tüm nav linkleri çalışır, mobil menü açılır.
  - Demo formu gönderilince teşekkür ekranı çıkar; dev outbox'ta e-posta görünür.
  - `/giris` ile demo hesabı girişi `/app/dashboard`'a gider.
  - `/#/accept-invite?...` → `/davet?...`; `/dashboard` → `/app/dashboard`.
  - Uygulama sayfaları açık ve koyu temada bozulmadan açılır.
  - 375 / 768 / 1280 genişliklerde ekran görüntüleri; konsolda hata yok.
- **Üretim CSP kontrolü:** yeni fontlar ve inline stiller mevcut politikayla engellenmiyor.
