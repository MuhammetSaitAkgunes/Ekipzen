# Ekipzen landing v2 — Canlı ürün vitrini ve gelişen ürün anlatısı

Tarih: 18 Eylül 2026. Önceki spec: `2026-09-17-yeni-kimlik-landing-design.md` (kimlik, token'lar, sayfa iskeleti; `main`'de, `dd68c71`). Metin üslubu revizyonu: `6727423`.

## Context

İlk landing sürümü kullanıcı tarafından çok basit bulundu. Kullanıcının geri bildirimi:

- **Ana sayfa:** eğik tasarım kullanılmayacak; daha fazla görsel ve efekt olacak.
- **Özellikler ve Güvenlik:** sıradan duruyor, daha çekici olmalı; efekt kullanılabilir.
- **Fiyatlandırma:** mantık ve metin iyi; efekt eklenebilir.
- **Hakkımızda:** en önemli sayfa; çok iyi kurgulanmalı ve güven vermeli.

Yeni kural: Ekipzen gelişmekte olan bir üründür. Bugün ihtiyaçlara cevap verirken değer üretmeye devam eder ve kullanıcıya bir "yatırım hissi" verir.

Referans taraması (üslup ve desen için; birebir kopya yok):

| Referans | Alınan desen |
|---|---|
| Linear Features | Bento ızgara ve kaydırmayla ilerleyen ürün görselleri |
| Vanta ve Notion Security | Kontrollerin kategoriye göre gruplanması |
| PostHog Roadmap | Aşama bazlı, dürüst ilerleme modeli |
| Notion ve Basecamp About | Metrik olmadan, anlatı ve açıklıkla kurulan güven |

## Kararlar (kullanıcı onaylı)

| Konu | Karar |
|---|---|
| Anlatı | Modül durum etiketleri + tarihsiz yol haritası. Yatırım hissi süreçle kurulur (şeffaf yol haritası, pilotla birlikte geliştirme); **fiyat veya kapsam taahhüdü verilmez**. |
| Durumlar | Personel, İzin, İşe alım = **Kullanımda**; Mesai ve Puantaj, Bordro = **Pilotta**; Risk Merkezi = **Geliştiriliyor**. |
| Yol haritası | Aşağıdaki liste; tarih veya çeyrek yok. Notu: "Tarih taahhüdü değildir; öncelikler pilot geri bildirimleriyle şekillenir." |
| Hakkımızda | Kişisel bilgi yok. Misyon, ilkeler, geliştirme süreci, olgunluk, yol haritası, taahhütler. |
| Efekt altyapısı | Saf CSS + küçük React hook'ları (IntersectionObserver). Yeni npm bağımlılığı yok; CSP değişmez. |
| Yaklaşım | "A · Canlı ürün vitrini": açık kurumsal kimlik korunur. |

Dayanak: `raporlar/pilot-hazirlik-analizi-2026-09-07.md` §8 "canlı veriden otomatik ayrılma/tükenmişlik tahmini"ni bugün desteklenmeyen genel vaatler arasında sayar. Bu yüzden Risk Merkezi "Geliştiriliyor" durumundadır ve tahmin vaadi kaldırılır.

## İçerik (content.ts'e eklenecek, metinler bu haliyle kullanılır)

### Durum tipi
`export type ModuleStatus = "active" | "pilot" | "building";` — etiketler: `active` → "Kullanımda", `pilot` → "Pilotta", `building` → "Geliştiriliyor". Açıklamalar (tooltip / yardımcı metin):
- Kullanımda: "Genel kullanıma açık; müşteri ortamlarında kullanılabilir."
- Pilotta: "Pilot müşteride gerçek verilerle doğrulanıyor."
- Geliştiriliyor: "Aktif olarak geliştiriliyor; genel kullanıma açılmadan önce pilotta doğrulanacak."

`MarketingModule` alanına `status: ModuleStatus` eklenir (değerler yukarıdaki tabloya göre).

Risk Merkezi metni:
- summary: "Devamsızlık, fazla mesai, kullanılmayan izin ve çalışan nabzı verilerinden departman düzeyinde risk sinyalleri üretmeye yönelik modül."
- points: "Departman bazında risk skoru ve dönemsel eğilim", "Yönetici yükü ve çalışan nabzı görünümü", "Sinyale bağlı aksiyon önerileri".

### Yol haritası (`roadmap`)
- **Şimdi — pilotta:**
  - "Mesai ve puantaj için çalışma düzeni profilleri": çalışma haftası, mola ve fazla mesai kurallarının şirket düzenine göre tanımlanması.
  - "Bordro için paralel doğrulama dönemi": ilk bordroların mevcut bordroyla paralel hesaplanıp uzman kabulünden geçirilmesi.
- **Sıradaki:**
  - "Canlı İK verisinden risk sinyalleri": Risk Merkezi'nin şirketin kendi izin, mesai ve devamsızlık kayıtlarıyla çalışması.
  - "Yedekleme ve geri dönüş güvencesi": düzenli yedek, sunucu dışı kopya ve denenmiş geri dönüş prosedürü.
- **Planlanan:**
  - "Çok faktörlü kimlik doğrulama": girişte ikinci doğrulama adımı.
  - "Genişleyen mevzuat kapsamı": farklı çalışan statüleri ve bordro senaryoları için desteğin artırılması.
  - "Ek raporlar": İK ve yönetim için hazır rapor setleri.

### Ürünü nasıl geliştiriyoruz (`devProcess`)
1. **İhtiyaç**: İK ekiplerinden gelen gerçek iş akışları ve geri bildirimler.
2. **Geliştirme**: küçük, gözden geçirilebilir adımlar.
3. **Otomatik testler**: her değişiklik birim ve entegrasyon testlerinden geçer.
4. **Pilotta doğrulama**: gerçek verilerle, kontrollü bir müşteri ortamında.
5. **Uzman kabulü**: bordro gibi hesaplamalar alan uzmanının onayından geçer.
6. **Genel kullanım**: doğrulanan modül tüm müşterilere açılır.

### İlkeler (`principles`)
- **Önce doğruluk**: hesaplamalar doğrulanmadan canlı kullanım için önerilmez.
- **Veri ayrımı**: her şirketin verisi kendi veritabanında tutulur.
- **Açık iletişim**: neyin hazır, neyin geliştirilmekte olduğunu açıkça söyleriz.
- **Sadelik**: her ekran belirli bir işi tamamlamaya hizmet eder.

### Taahhütler (`commitments`)
1. **Hazır olanla geliştirileni ayırırız.** Her modülün durumu sitede ve üründe açıkça belirtilir.
2. **Verinizin sahibi sizsiniz.** Verileriniz şirketinize ait ayrı bir veritabanında tutulur; her kullanıcı kendi verisini dışa aktarabilir.
3. **Hesaplamalar doğrulanmadan kullanıma alınmaz.** Bordro, uzman kabulünden önce ödeme veya beyan için esas alınmaz.

### Çalışanın yolculuğu (`journeySteps`)
İşe alım → Özlük → İzin → Puantaj → Bordro. Her adım için tek cümle:
- İşe alım: "Aday işe alındığında davetle sisteme dahil edilir."
- Özlük: "Personel kartı ve belgeler tek kayıtta toplanır."
- İzin: "Talepler aynı kayıt üzerinden onaylanır, bakiye güncellenir."
- Puantaj: "Günlük mesai kayıtları dönem puantajına dönüşür."
- Bordro: "Bordro, dönemin her iş gününün puantaj ya da onaylı izinle karşılandığını kontrol eder."

### Güvenlik
- **`securityLayers`** (dıştan içe; her birinin başlığı ve açıklaması var):
  1. Şifreli bağlantı: HTTPS, HSTS, CSP.
  2. Kimlik doğrulama: kısa ömürlü oturum anahtarı, parola kuralları, giriş denemesi sınırı.
  3. Rol bazlı yetki: arayüzde ve sunucuda.
  4. Şirkete ait veritabanı: veri ayrımı.
  5. Denetim izi: değişikliği yapan kişi ve zaman.
- **`securityCategories`**: Veri, Erişim, İşletim, Gizlilik (KVKK). Her kategori mevcut `securityPillars` maddelerini gruplar.
  - İşletim kategorisinde "Giriş denemesi sınırlaması" yer alır; ayrıca şu madde eklenir: "Yayın öncesi otomatik testler" — her değişiklik birim ve entegrasyon testlerinden geçer.
- **`transparency`**:
  - Bugün sağlanan: ayrı veritabanı, rol bazlı yetki, şifreli bağlantı, denetim izi, kişisel veri dışa aktarımı, giriş denemesi sınırı.
  - Üzerinde çalışılan: çok faktörlü kimlik doğrulama; yedekleme ve geri dönüş güvencesi.

## Sayfa tasarımları

### Ana sayfa
1. **Hero** — düz yerleşim, eğim yok.
   - Sol: mevcut başlık ve alt metin, iki CTA ve güven maddeleri.
   - Sağ: katmanlı ürün paneli. Ana ekranın üzerinde iki kart süzülür (yavaş dikey salınım).
   - Arka planda yavaş hareket eden gradyan ışık katmanı.
2. **Canlı ürün vitrini** — `ProductShowcase`.
   - Sekmeler: Personel · İzin · Puantaj · Bordro · İşe alım · Risk. Her sekmede durum etiketi.
   - Seçili modülün örnek ekranı kayarak ve solarak geçer.
   - Otomatik ilerleme 6 sn'dir. Fare vitrinin üzerindeyken, odak içindeyken ya da kullanıcı bir sekmeye tıkladığında durur.
   - `role="tablist"`; ok, Home ve End tuşlarıyla gezinilir.
3. **Bir çalışanın yolculuğu** — `EmployeeJourney`, yapışkan kaydırma anlatısı.
   - Solda yapışkan bir personel kartı; alanları adım adım dolar.
   - Sağda beş adım; görünüme girdikçe aktifleşir.
   - Mobilde yapışkanlık kalkar; adımlar kartla birlikte dikey akar.
4. **Modüller** — bento ızgara; her kartta durum etiketi.
5. **Gelişim şeridi** — "Ekipzen gelişmeye devam ediyor".
   - Üç sütun: şimdi, sıradaki, planlanan. Her sütunda ilk bir-iki madde gösterilir.
   - "Yol haritasının tamamı" bağlantısı → `/hakkimizda#yol-haritasi`.
6. **Güvenlik özeti** — koyu bant.
7. **SSS**.
8. **Kapanış bandı**.

Eski "Risk Merkezi öne çıkan" bölümü kaldırılır.

### Özellikler
1. **Hero ve modül çipleri** — her çipte durum noktası.
2. **Yapışkan kaydırma vitrini:**
   - Solda `StickyModuleNav`: altı modül; görünen modül vurgulanır ve bir ilerleme çizgisi ilerler.
   - Sağda her modül bir blok: başlık, durum etiketi, özet, üç yetenek, kimler için, animasyonlu örnek ekran.
   - Mobilde menü üstte yatay kayan bir çubuğa dönüşür.
3. **Roller bento ızgarası** — İK yöneticisi geniş kart.
4. **Veri akışı şeması** — `DataFlowDiagram`, SVG: modüller ortadaki "Personel kaydı" düğümüne bağlanır; çizgiler görünüme girince çizilir.
5. **Kapanış bandı**.

### Güvenlik
1. **Hero**.
2. **`SecurityLayers`** — beş katmanlı etkileşimli şema:
   - Masaüstünde iç içe halkalar/katmanlar ve bir açıklama paneli.
   - Seçilen katman vurgulanır; klavyeyle erişilebilir düğmeler.
   - Mobilde akordeon.
3. **Kategori ızgarası** — Veri · Erişim · İşletim · Gizlilik.
4. **Şeffaflık tablosu** — iki sütun: "Bugün sağlanan" (✓) ve "Üzerinde çalışılan" (durum etiketi "Geliştiriliyor").
5. **KVKK paragrafı** ve aydınlatma metni bağlantısı.
6. **Kapanış bandı**.

### Hakkımızda (`id="yol-haritasi"` anchor içerir)
1. **Manifesto hero** — editoryal, büyük tipografi; yavaşça çizilen çizgi arka planı. Metin: "İK yazılımını, İK ekiplerinin günlük gerçekliğinden başlayarak geliştiriyoruz."
2. **Neden varız** — üç sahne, kodla çizilmiş görseller, kaydırmayla sırayla belirir:
   - Dağınık tablolar ve yazışmalar
   - Aynı bilginin tekrar tekrar girilmesi
   - Tek bir kayıt düzeni
3. **İlkeler** — dört kart.
4. **Ürünü nasıl geliştiriyoruz** — `ProcessTimeline`, altı adım; kaydırmayla dolan süreç çizgisi.
5. **Ekipzen bugün** — `ModuleMaturity`: altı modül, durumlarına göre üç kademede.
6. **Yol haritası** — `RoadmapBoard`: üç sütunlu pano ve tarih notu.
7. **Taahhütlerimiz** — koyu bant, üç madde.
8. **Kapanış** — "Ekipzen'in gelişimine erken dahil olun" → Demo talep edin.

### Fiyatlandırma
- Yapı ve metin korunur.
- Kartlar sırayla belirir.
- Öne çıkan kartın kenarında yavaş hareket eden bir gradyan olur.
- Kartların üzerine gelindiğinde hafif yükselme ve ışık efekti olur.
- Karşılaştırma tablosunda satırlar üzerine gelindiğinde vurgulanır.
- Kapanıştan önce "Ürün nasıl gelişiyor?" bağlantısı yer alır (→ `/hakkimizda#yol-haritasi`). Fiyat taahhüdü içermez.

## Teknik yapı

- **`frontend/src/marketing/motion/`:**
  - `usePrefersReducedMotion.ts`
  - `useInView.ts` (IntersectionObserver; `once` seçeneği)
  - `Reveal.tsx` (`as`, `delay`, `className`; hareket azaltılmışsa doğrudan görünür render eder)
  - `useSpotlight.ts` (kart ışığı için pointer koordinatlarını `--mx` / `--my` değişkenlerine yazar)
- **`frontend/src/marketing/StatusBadge.tsx`**: `StatusBadge({ status })` → mevcut `Badge` (success / warning / primary tonları) ve `title` açıklaması.
- **Yeni bileşenler** (`frontend/src/marketing/` altında, her biri kendi dosyasında):
  - `ProductShowcase`, `EmployeeJourney`, `StickyModuleNav`, `DataFlowDiagram`
  - `SecurityLayers`, `ProcessTimeline`, `ModuleMaturity`, `RoadmapBoard`
- **`visuals.tsx` eklemeleri:** `PersonnelListCard`, `TimesheetGridCard`, `RecruitmentBoardCard`. `ModuleVisual` eşlemesi modül başına güncellenir. Hero'daki `rotate` / `translateY` eğimi kaldırılır.
- **Stiller:** `frontend/src/styles/marketing-motion.css` dosyası reveal, glow, spotlight, float, showcase, journey ve layer stillerini içerir. `@media (prefers-reduced-motion: reduce)` altında bütün hareket kapanır. `marketing.css` yalnız düzen için güncellenir.
- **Kısıtlar:**
  - Yalnız `--ez-*` token'ları ve ramp'ler kullanılır.
  - Dokunma hedefleri ≥ 44px; odak halkası görünür.
  - 375px'te yatay taşma olmaz.
  - Yeni bağımlılık eklenmez.
  - Metinler `ekipzen-metin-uslubu` kurallarına uyar.

## Test ve doğrulama

- **Birim testleri:**
  - `Reveal` hareket azaltılmışken içeriği anında görünür render eder.
  - `useInView` gözlemciyi bağlar ve kaldırır (IntersectionObserver jsdom'da stub'lanır).
  - `StatusBadge` üç durumu doğru etiket ve tona eşler.
  - `ProductShowcase`:
    - `tablist`, `tab` ve `tabpanel` ARIA bağları doğru kurulur.
    - Ok, Home ve End tuşları sekmeyi değiştirir.
    - Tıklamadan sonra otomatik ilerleme durur (sahte zamanlayıcıyla).
  - `SecurityLayers`: bir katman seçildiğinde açıklaması görünür; `aria-expanded` / `aria-pressed` doğru değişir.
  - `RoadmapBoard` üç sütunu ve tarih notunu render eder.
  - `ModuleMaturity` her modülü kendi kademesinde gösterir.
  - Hakkımızda sayfasında `#yol-haritasi` anchor'ı vardır.
- **İçerik koruma testleri:**
  - Her modülün geçerli bir `status` değeri vardır.
  - `building` durumundaki modülün metni "tahmin", "otomatik tahmin" ya da "kullanımda" içermez.
  - Üslup testi yeni dosyaları da kapsar.
  - Taahhüt ve yol haritası metinleri tarih ya da fiyat ifadesi içermez. Kontrol edilen kalıplar: `/\b20\d{2}\b/`, `/çeyrek/i`, `/₺|TL/`.
- **Tarayıcı kontrolü:**
  - 375 / 768 / 1280px'te yatay taşma yok.
  - Hareket azaltma tercihi emüle edildiğinde içerik tam görünür.
  - Konsolda hata yok.
  - Vitrin klavyeyle kullanılabilir.

## Kapsam dışı
- Uygulama içi sayfalar.
- Gerçek ekran görüntüleri; görseller kodla çizilmeye devam eder.
- Fiyat bilgisi, yeni npm bağımlılığı ve kişisel/ekip bilgisi.
- KVKK metninin hukuki içeriği.
