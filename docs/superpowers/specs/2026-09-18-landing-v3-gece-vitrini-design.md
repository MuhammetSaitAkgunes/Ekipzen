# Landing v3 — "Gece vitrini": koyu, dokulu, ürün merkezli tanıtım sitesi

Tarih: 2026-09-18 · Durum: onaylandı (bölüm bölüm) · Önceki: `2026-09-18-landing-v2-canli-vitrin-design.md`

## Bağlam

v2 (canlı vitrin) teknik olarak tamamlandı; ancak kullanıcı geri bildirimi:

- "Beyaz renk çok fazla."
- "Çok sade ve boş duruyor, sıradan SaaS sitesi gibi."

Görsel yön seçimi tarayıcıda örneklerle yapıldı. İlk dört yön (koyu teknolojik, renk blokları, koyu–açık ritim, sıcak editoryal) reddedildi. İkinci turda **"1 · Ürün sahnede" + "4 · Derinlik ve doku"** birleşimi seçildi ve birleşik ana sayfa taslağı onaylandı. Taslak: `.superpowers/brainstorm/847-1789755635/content/birlesik-yon.html` (git dışı).

Onaylanan yaklaşım **B**: görsel katman yeniden kurulur; içerik, dürüstlük kuralları, etkileşim mantığı ve testler korunur.

## Kapsam

- **Dahil:** Tüm tanıtım sayfaları (`/`, `/ozellikler`, `/fiyatlandirma`, `/guvenlik`, `/hakkimizda`, `/iletisim`, `/kvkk-aydinlatma`, 404) ile giriş (`/giris`) ve davet (`/davet`) ekranları.
- **Hariç:** Giriş sonrası uygulama (`/app/*`) hiç değişmez; Spec 2'de ele alınır.
- **Değişmeyenler:**
  - `content.ts` metinleri, modül durumları (Kullanımda / Pilotta / Geliştiriliyor) ve fiyat taahhüdü olmaması.
  - Anchor adresleri (`#personel`, `#yol-haritasi` …), bölüm `aria-labelledby` başlıkları.
  - Demo formu ve giriş davranışı.

## 1. Tema ve tasarım katmanı

- **Kapsam sınıfı:** `.ez-light` yerine `.ez-night` sınıfı. `MarketingLayout` kökü (`ez-site`) ve `AuthLayout` kökü (`ez-auth`) bu sınıfı alır. `.ez-night` mevcut `--ez-*` amaç token'larını koyu değerlerle yeniden tanımlar. Paylaşılan `ui/` bileşenleri (Button, Field, Badge …) böylece kendiliğinden koyu görünür; uygulama (`:root`) etkilenmez.
- **Zemin tonları:**
  - `--ez-bg`: `#070b18`
  - İkinci bölüm tonu: `#0b1122`
  - Üçüncü ton: `#0e1530`
  - Cam yüzey: `rgb(255 255 255 / .045)`, kenar `rgb(255 255 255 / .11)`
- **Vurgu:**
  - İndigo ana renk (`--indigo-400/500`), yanında turkuaz (`--sky-300/400`) ve mor (`#c026d3` / `#a855f7`).
  - Birincil buton turkuaz→indigo degrade, beyaz metin.
  - Degrade başlık vurgusu: turkuaz → indigo → pembe.
- **Durum renkleri:** Açık tonlar, düşük opaklıklı zemin üzerinde (yeşil `#6ee7b7`, amber `#fcd34d`, pembe `#f9a8d4`).
- **Doku ve ışık** (yalnız CSS, bulanıklık filtresi yok):
  - Giriş bölümlerinde 32px ızgara çizgileri.
  - Site genelinde tek bir küçük SVG gren (`feTurbulence`), opaklık ≈ .12–.14, `pointer-events: none`.
  - Bölüme göre konumlanan radyal degrade ışık bulutları.
  - Kartlarda `backdrop-filter: blur()` cam etkisi ve mevcut spotlight efekti.
- **Tipografi:** Plus Jakarta Sans. Giriş başlığı `clamp()` ile en fazla 60px, kalın, sıkı harf aralığı.
- **Erişilebilirlik:**
  - Tüm metin/zemin çiftleri WCAG AA (≥4.5:1); `contrast.test.ts` koyu değerlerle genişletilir.
  - Odak halkası turkuaz ve görünür.
  - `prefers-reduced-motion` açıkken ışık animasyonları ve süzülme durur, tüm içerik anında görünür.

## 2. Ürün ekranları

`marketing/visuals.tsx` yeniden yazılır; artık kullanılmayan küçük `MockCard` kartları silinir.

- **`AppFrame`** — koyu uygulama penceresi.
  - Üst çubukta pencere noktaları ve adres alanı; solda Ekipzen kenar menüsü (Genel bakış, Personel, İzinler, Mesai ve puantaj, Bordro, İşe alım, Risk Merkezi); sağda içerik.
  - Prop: `active` (menüde seçili öğe), `children`.
  - Mobilde (≤640px) kenar menüsü gizlenir.
- **Modül ekranları** (değerler örnek):
  - `OverviewScreen`: özet kutuları, aylık grafik, son işlemler.
  - `PersonnelScreen`: arama çubuklu tablo; avatar, ad, departman, durum.
  - `LeaveScreen`: aylık takvim ve bekleyen onaylar.
  - `TimesheetScreen`: haftalık saat çizelgesi ve dönem özeti.
  - `PayrollScreen`: dönem özeti (brüt, SGK, net) ve çalışan satırları.
  - `RecruitmentScreen`: Başvuru → Mülakat → Teklif panosu.
  - `RiskScreen`: skor, eğilim ve önerilen aksiyonlar; "Geliştiriliyor" bağlamında.
- **`FloatingNotice`** — buzlu cam bildirim (ikon, başlık, alt satır).
  - Konum sınıfıyla ekran kenarına taşar, yavaş süzülür; reduced-motion'da sabit durur.
- **`ModuleVisual({ id })`** korunur: ilgili modül ekranını `AppFrame` içinde, bir bildirimle birlikte verir.
- **Erişilebilirlik:**
  - Ekran içeriği `aria-hidden`; `figure` içinde tek cümlelik `figcaption` (ekran okuyucuya özel) okunur.
  - Bildirimler de `aria-hidden` olur; dekoratiftir.

## 3. Sayfa düzenleri

### Ortak parçalar

- **Üst menü:** Cam, yapışkan. Kaydırınca koyulaşır ve kenar alır (küçük bir scroll dinleyicisi ile).
- **Alt bilgi:** En koyu ton, üstte degrade ince çizgi.
- **`PageHero`:** Izgara ve ışık bulutu. Her sayfanın ışık rengi farklı: Özellikler turkuaz, Güvenlik yeşil-turkuaz, Hakkımızda mor, Fiyatlandırma indigo.
- **Bölüm ritmi:** Üç ton sırayla değişir, geçişlerde ince ışık çizgileri.

### Ana sayfa

1. **Giriş:** Ortalanmış başlık (ikinci yarısı degrade), iki buton. Altında taşan `OverviewScreen` ve üç `FloatingNotice`. Yalnız CSS ile giriş animasyonu; IO kullanılmaz.
2. **Vitrin:** `ProductShowcase` sekmeleri cam çubuk; panelde büyük modül ekranı.
3. **Çalışan yolculuğu:** Yapışkan kart ve adımlar; etkin adımın yanında ışık çizgisi.
4. **Modüller:** Cam bento, altı modül. Her kartta modül rengi ışık ve küçük ürün parçası.
5. **Yol haritası şeridi:** Üç cam sütun, renkli üst ışık çizgisi.
6. **Güvenlik:** 3B eğik katman yığını (Uygulama, Oturum, Yetki, Veritabanı) ve cam güvence maddeleri.
7. **SSS:** Cam akordeon.
8. **Çağrı bandı:** Degrade cam kutu.

### Özellikler

- Cam modül çipleri.
- Yapışkan menü korunur. Her blokta `ModuleVisual` (büyük ekran ve bildirim) ile metin; ekranlar sırayla sağa ve sola yerleşir.
- `DataFlowDiagram` ışıklı çizgilerle; roller bölümü cam kartlarla.

### Güvenlik

- `SecurityLayers` 3B yığın olur. Seçili katman öne çıkar, açıklaması yanda görünür. Mevcut klavye ve ARIA davranışı korunur.
- Kategoriler 2×2 cam kart; şeffaflık iki sütun.

### Hakkımızda

- Manifesto girişinde büyük tipografi ve mor ışık.
- Numaralı cam sahne kartları; ilkeler 2×2.
- `ProcessTimeline` ışık akan çizgi, `ModuleMaturity` ve `RoadmapBoard` cam sütunlar.
- Taahhütler en koyu bantta, ışıklı onay işaretleriyle.

### Fiyatlandırma

- Üç cam paket kartı. Önerilen paketin kenarında dönen degrade ışık (`@property` açısı; reduced-motion'da sabit).
- Koyu karşılaştırma tablosu. Durum etiketleri ve pilot notu korunur.

### Diğer sayfalar

- **İletişim / demo:** Solda cam panelde form; sağda "Süreç nasıl işler" adımları ve küçük bir ürün ekranı.
- **KVKK:** Okunaklı metin sütunu.
- **404:** Büyük degrade "404", ışık ve ana sayfaya dönüş.

### Giriş ve davet ekranları

- **Sol:** Koyu cam panelde form.
- **Sağ:** Işıklı zemin üzerinde `OverviewScreen` (küçük), bir bildirim ve güvence maddeleri.
- Davranış aynı kalır.

## 4. Doğrulama ve çalışma düzeni

- Tüm iş **satır içinde**, alt ajan kullanılmadan yapılır.
- **Sıra:** tema → ürün ekranları → ortak parçalar → ana sayfa → **kullanıcı kontrol noktası** → Özellikler → Güvenlik → Hakkımızda → Fiyatlandırma/İletişim/KVKK/404 → giriş/davet → temizlik. Her adım küçük bir commit.
- **Her adımda:**
  - Değişen sayfalar 1280px ve 375px'te baştan sona kaydırılarak gerçek ekran görüntüleriyle kontrol edilir. Görünmeyen bölüm, yatay taşma ve konsol hatası olmamalı.
  - Reduced-motion açıkken ayrıca kontrol edilir.
  - `npm --prefix frontend run lint`, `npm --prefix frontend test -- --run`, `npm --prefix frontend run build` temiz olmalı.
- **Testler:** Mevcut etkileşim testleri (vitrin, yolculuk, yapışkan menü, güvenlik katmanları, yol haritası, fiyat, form) korunur; görsel bileşen testleri yeni ekranlara göre güncellenir.
- **Birleştirme:** Aynı dalda (`claude/ui-ux-landing-page-design-23fc85`) devam edilir. Bitince AGENTS.md'ye uygun rapor ve tek seferlik birleştirme onayı istenir.
