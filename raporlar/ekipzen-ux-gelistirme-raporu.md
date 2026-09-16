# Ekipzen — Frontend UX/UI Geliştirme Raporu

> **Teslimat türü:** Analiz + öncelikli geliştirme yol haritası (yalnızca rapor, kod değişikliği yok).
> **Hedef:** Kolay anlaşılır, bilişsel yükü hafif, az tıklamayla iş bitiren bir SaaS İK deneyimi.
> **Yöntem:** `index.html`, `routes.js`, tüm `styles/*.css` ve `components/*.js` incelendi; `ui-ux-pro-max` tasarım-sistemi motoru "HR SaaS dashboard" için çalıştırıldı (öneri: *Data-Dense Dashboard* — mevcut yön doğru).

---

## Context — Neden bu rapor?

Ekipzen, olgun bir tasarım temeline sahip (token sistemi, IBM Plex, petrol/evergreen palet, light/dark, rol bazlı yönlendirme). Sorun görsellik değil; **bilgi mimarisi ve etkileşim ekonomisi**. Üç ayrı "giriş/aksiyon" ekranı, gizli satır işlemleri, placeholder butonlar ve sistematik erişilebilirlik boşlukları kullanıcının "nereden başlayacağım / bunu kaç tıklamada yaparım" yükünü artırıyor. Bu rapor güçlü yönleri korur, sürtünmeyi kaldırır.

---

## 1. Güçlü yönler (korunmalı)

- **Tek kaynak tasarım tokenları** — `styles/main.css:7-82`; hex hardcode yok, light/dark tam kapsanmış.
- **Erişilebilirlik temelleri var** — görünür `:focus-visible` halkası (`main.css:134`), `prefers-reduced-motion` (`main.css:140`).
- **Tipografi doğru** — IBM Plex Sans (arayüz) + Plex Mono (veri, `tabular-nums`). Data-dense dashboard için ideal.
- **Rol bazlı rota filtresi** — `routes.js` `roles[]` + `canAccessRoute`.
- **Grafik dayanıklılığı** — Chart.js yoksa `chart-fallback` (`dashboard.js:1050`).
- **Akıllı "aksiyon-önce" kurgu** — Risk Merkezi kartları doğrudan detaya götürüyor; öneri metinleri ("önerilen aksiyon") iyi bir UX içgörüsü.

---

## 2. Ana bulgular (öncelik sırasıyla)

### 🔴 Kritik — bilişsel yük & yön kaybı

**B1. Üç örtüşen "ana sayfa/aksiyon" ekranı.**
- `Risk Merkezi` (`/dashboard`, `Dashboard()`), `Genel Durum` (`/overview`, `OverviewDashboard()`) ve `Aksiyonlar` (`/actions`, `ActionsCenter()`) — üçü de "Kokpit" grubunda (`components/layout.js:15`).
- Ayrıca aksiyonlar **üç** yerde yaşıyor: dashboard içindeki `Aksiyon Merkezi` aside'ı + `ActionCenterDetail` sayfası + ayrı `Global Aksiyon Merkezi`. Kullanıcı "gerçek liste hangisi?" sorusuyla kalıyor.
- **Varsayılan açılış `/dashboard`** (`routes.js:26`) = en ağır analitik ekran. İlk ekran olarak yüksek yük.

**B2. Grup etiketleri içerikle çelişiyor.**
- "Bordro & Uyum" grubunda sadece `payroll` var; *Uyum* aslında Risk Merkezi alt-detayında (`compliance-risk`). Etiket, olmayan bir menü öğesi vaat ediyor (`layout.js:14-19`).

### 🟠 Yüksek — az tıklama / iş bitirme

**B3. Satır işlemleri gizli ve tek yollu.**
- Personel tablosunda her satırda tek bir `⋯` butonu var (`personnel.js:92`), etiketi yok, ne yaptığı belirsiz. Görüntüle/Düzenle/İzin gibi sık işlemler en az 2 tıklama + keşif gerektiriyor.
- **Toplu seçim yok** — "Dışa Aktar" butonu var ama satır checkbox'ı yok; toplu işlem imkânsız.

**B4. Placeholder butonlar (ölü uçlar).**
- "Filtrele" ve "Dışa Aktar" işlevsiz (`personnel.js:53-54`, `actions.js:62`). Kullanıcı tıklar, hiçbir şey olmaz → güven kaybı, bilişsel yük. Ya işlevsel yap ya da gösterme.

**B5. Global arama pasif.**
- Header'da güçlü bir arama alanı var (`layout.js:96`) ama bağlı değil. Çalışan bir **komut paleti / global arama** (personel, aday, işlem, sayfaya git) "az tıklama" hedefinin en büyük tek kazancı olurdu.

**B6. Geri bildirim (toast) sistemi yok.**
- Kod tabanında `toast/notify` **hiç yok**. Kaydet/Onayla/Reddet sonrası kullanıcı ne olduğunu görmüyor → işlemi tekrar deneme, belirsizlik.

### 🟡 Orta — erişilebilirlik (SaaS'ta zorunlu)

**B7. `aria-label` / `aria-hidden` tüm kod tabanında sıfır.**
- İkon-only butonlar (`⋯`, onay/ret, header ikonları) yalnızca `title` taşıyor; ekran okuyucu için `aria-label` yok. Dekoratif `<i>` ikonlarında `aria-hidden` yok.
- Header arama input'unda görünür/`aria` etiketi yok (yalnız placeholder).

**B8. Dokunma hedefleri 44px altında.**
- `btn-sm` ve `btn-icon-sm` = 32px (`main.css:231, 298`). Mobil kılavuzu min 44×44px.

**B9. Mobilde okunabilirlik.**
- `html` 15px kök; tablo hücresi `0.9rem` ≈ 13.5px (`main.css:91, 447`). Mobil gövde metni için önerilen min 16px'in altında.

### 🟢 Düşük — tutarlılık & teknik borç

**B10. Sınıf adı çoğullaşması (aynı şeyin 5 takma adı).**
- Kart başlığı: `.card-header-clean/.section-head/.panel-head/.widget-header/.card-head` (`main.css:830`).
- Tablo: `.data-table/.pro-table/.att-table/.leaf-table/.mini-table` (`main.css:402`).
- Rozet/pill: 11 farklı takma ad (`main.css:471-481`). Çalışıyor ama bakım + zihinsel yük.

**B11. Durum sınıfları Türkçe değere bağlı.**
- `.status-tag.teklif`, `.mülakat`, `.yeni` gibi anlam yerine değere bağlı sınıflar (`main.css:493-513`) — yeni durum eklemeyi kırılgan yapıyor.

**B12. Başlık ölçeği tutarsız.**
- `page-header h2` = 1.4rem, `welcome-header h2` = 1.5rem (`main.css:188, 673`). Küçük ama fark ediliyor.

---

## 3. Öncelikli geliştirme yol haritası (etki / efor)

| # | Öneri | Etki | Efor | Faz |
|---|-------|------|------|-----|
| B1 | **Rol bazlı tek ana sayfa** + aksiyon yüzeylerini tekilleştir | ★★★ | Orta | 1 |
| B5 | Global arama / komut paletini işlevsel yap | ★★★ | Orta | 2 |
| B3 | Satır içi hızlı işlemler + toplu seçim | ★★★ | Orta | 2 |
| B6 | Toast/geri bildirim sistemi | ★★★ | Düşük | 1 |
| B7 | `aria-label`/`aria-hidden` sistematik ekleme | ★★☆ | Düşük | 1 |
| B4 | Placeholder butonları işlevsel yap veya gizle | ★★☆ | Düşük | 1 |
| B8/B9 | Dokunma hedefi 44px + mobil 16px gövde | ★★☆ | Düşük | 2 |
| B2 | Nav grup etiketlerini içerikle hizala | ★★☆ | Düşük | 1 |
| B10-12 | CSS takma adlarını sadeleştir, başlık ölçeği | ★☆☆ | Orta | 3 |

**Faz 1 (Hızlı kazanımlar, düşük risk):** B6, B7, B4, B2 + başlık ölçeği. Görünürlük ve güven anında artar.
**Faz 2 (Etkileşim ekonomisi):** B5, B3, B8/B9. "Az tıklama" hedefinin kalbi.
**Faz 3 (Sağlamlaştırma):** B1 IA birleştirmesi tamamı + CSS borç temizliği.

---

## 4. Öne çıkan öneri — Rol bazlı tek ana sayfa (seçilen yön)

**Bugün:** 3 ayrı ekran + 3 aksiyon yüzeyi. **Hedef:** 1 uyarlanır ana sayfa + 1 aksiyon kaynağı.

```
ÖNCESİ                          SONRASI
Kokpit                          Ana Sayfa  (rol bazlı /home)
  ├─ Risk Merkezi   (admin)       ├─ Çalışan: bugünkü durum + kişisel aksiyonlar
  ├─ Genel Durum    (herkes)      ├─ Yönetici: ekip özeti + onay kuyruğu + risk şeridi
  └─ Aksiyonlar     (herkes)      └─ İK Admin: risk skoru + kurumsal sinyaller
                                 Aksiyonlar (tek global liste — dashboard sadece "en acil 3" + "Tümünü aç")
```

**İlkeler:**
- **Progressive disclosure:** Ana sayfa yalnızca *bugün ne yapmalıyım*'ı gösterir; derin analitik (heatmap, kapasite, nabız) tek tık ötede detay sayfalarında kalır.
- **Tek aksiyon gerçeği:** Dashboard'daki `action-center` aside'ı özet olur (en acil 3), tek "Aksiyon Merkezi"ne (`/actions`) yönlendirir. `ActionCenterDetail` ile `ActionsCenter` birleştirilir.
- **Rol = varsayılan rota:** `DefaultProtectedRoute` role göre çözülür (çalışan → kişisel özet, admin → risk). Böylece ilk ekran her zaman kullanıcıya uygun ağırlıkta.
- **Geriye dönük uyum:** Mevcut sayfalar/rotalar korunur; yalnızca gruplama, varsayılan rota ve aside davranışı değişir. Kırılma yok.

---

## 5. Kategori bazlı somut öneriler (uygulama notları)

**Navigasyon/IA** — `layout.js:14-19`, `routes.js`: grupları `Ana Sayfa / Çekirdek İK / Bordro / Yönetim` olarak sadeleştir; "Uyum"u ya gerçek menü öğesi yap ya da etiketten çıkar. Aktif öğe göstergesi (sol şerit) zaten iyi.

**Tablolar & az tıklama** — her satıra 2-3 görünür hızlı eylem (Görüntüle/Düzenle) + taşan `⋯`; sol checkbox ile toplu seçim; seçim yapılınca beliren toplu aksiyon çubuğu. "Dışa Aktar" seçime bağlanır.

**Geri bildirim** — global `showToast(mesaj, tip)` yardımcı fonksiyonu + `aria-live="polite"` bölge; tüm kaydet/onay/ret akışlarına bağla.

**Erişilebilirlik** — ikon-only butonlara `aria-label`, dekoratif `<i>`'lere `aria-hidden="true"`; arama input'una `<label class="sr-only">`; `.btn-sm/.btn-icon-sm` min 40-44px; mobilde gövde 16px.

**Form modalleri** — `personnel.js` full-screen modal sekmeli yapısı iyi; kaydet sonrası toast + modal kapanış + liste güncelleme döngüsünü netleştir; zorunlu alan doğrulaması ve hata mesajı alan yanında.

**CSS borcu** — kart-başlığı ve tablo takma adlarını tek kanonik sınıfa indir (kademeli, mevcut markup'ı kırmadan bir alias katmanıyla); durum pill'lerini anlam bazlı (`.is-success/.is-warning/.is-danger`) yap.

---

## 6. Doğrulama / başarı ölçütleri

Bu bir rapor olduğundan "test" yerine **kabul kriterleri** öneriyorum (uygulama fazına geçilirse):

- **Tıklama sayısı:** En sık 5 iş (personel görüntüle, izin onayla, aksiyon kapat, bordro aç, aday ilerlet) ≤ 2 tıklama.
- **İlk ekran yükü:** Rol bazlı ana sayfada "birincil aksiyon" ekranın üst katında, kaydırmasız görünür.
- **Erişilebilirlik:** Klavye ile tüm etkileşimli öğeler gezilebilir; ekran okuyucu her ikon butonunu adlandırır; kontrast ≥ 4.5:1 (light/dark). Lighthouse a11y ≥ 95.
- **Geri bildirim:** Her yazma işlemi görünür toast + `aria-live` duyurusu üretir.
- **Responsive:** 375 / 768 / 1024 / 1440px'de yatay kaydırma yok, dokunma hedefleri ≥ 44px.

---

### Sonraki adım
Bu rapor tek başına teslimattır. İstersen Faz 1 "hızlı kazanımlar"ı (toast + aria-label + placeholder temizliği + nav etiketleri) düşük riskle uygulayabilirim — bunun için ayrı onay verirsin.
