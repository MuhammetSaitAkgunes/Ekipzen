# 1. Genel Bakış

## 1.1 İK Pro nedir?

İK Pro, bir şirketin insan kaynakları operasyonunu tek yerden yöneten bir web
uygulamasıdır. "SaaS" (Software as a Service) demek: tarayıcıdan girilen, çok
kullanıcılı, rol bazlı bir kurumsal ürün. Bu depo o ürünün **çalışan bir demosu**dur:
gerçek bir veritabanı, gerçek kimlik doğrulama ve gerçek iş kuralları vardır ama
demo verilerle beslenir ve üretim yerine öğrenme/gösterim amaçlıdır.

Uygulama şunları yapar: personel kayıtları, izin talep/onay akışı, mesai-puantaj
takibi, **bordro hesaplama** (Türkiye 4/a brüt→net), işe alım (aday havuzundan
işe alıma), evrak/uyum takibi, risk analitiği (ayrılma/tükenmişlik sinyalleri),
global aksiyon merkezi ve denetim izi.

## 1.2 Kimler kullanır? (Roller)

Sistemde üç rol vardır. Rol, kullanıcının **ne görebileceğini ve ne yapabileceğini**
belirler. Rol adları kod boyunca sabittir (`backend/.../Domain/Constants/Roles.cs`):

| Rol | Kod adı | Ne görür / yapar |
| --- | --- | --- |
| İK Yöneticisi | `hr-admin` | **Her şey.** Tüm personel, tüm bordro, ayarlar, işe alım, silme/oluşturma. |
| Departman Yöneticisi | `manager` | **Yalnız kendi ekibi.** Ekip izin onayı, ekip uyum/risk görünümü (salt-okur ağırlıklı). |
| Çalışan | `employee` | **Yalnız kendisi.** Kendi izinleri, kendi bordro pusulaları, kendi puantajı. |

> **Neden rol bazlı?** Kurumsal İK verisi hassastır (maaş, TC kimlik, sağlık raporu).
> "Herkes her şeyi görsün" bir güvenlik felaketidir. Roller, yetkiyi hem rota
> seviyesinde (frontend) hem de veri seviyesinde (backend `ScopeFor`) sınırlar.
> Bkz. [Bölüm 4](04-kimlik-guvenlik-hata.md).

Demo hesaplar (hepsi şifre `demo123`):

| E-posta | Rol |
| --- | --- |
| `ik@hrmaster.local` | hr-admin |
| `ece.arslan@hrmaster.local` | manager |
| `ahmet.yilmaz@hrmaster.local` | employee |

## 1.3 Depo neyden oluşur? (Üç ayaklı yapı)

```
İK Pro/
├── backend/            .NET 9 API + iş mantığı + veritabanı
├── frontend/           React uygulaması (kullandığımız gerçek arayüz)
├── legacy-frontend/    Eski mock uygulama (tasarım referansı, dokunulmaz)
├── docs/               Dokümantasyon (bu rehber, günlük, planlar)
└── raporlar/           Backend faz planı
```

**Neden `legacy-frontend/` hâlâ duruyor?** Proje aslında iki aşamada gelişti:
önce her ekranın "nasıl görüneceğini" gösteren, veriyi sahte (mock) üreten bir
HTML/JS prototip yazıldı. Sonra bu prototip, gerçek backend'e bağlı bir React
uygulamasına **piksel-piksel aynı kalacak şekilde** taşındı. Eski uygulama,
"yeni ekran doğru mu göründü?" karşılaştırması için referans olarak korunur.
Detay: [Bölüm 8.2](08-kararlar-neden.md#82-neden-eski-mock-uygulama-korunuyor).

## 1.4 Modül haritası

Uygulama, sol menüde gruplanan modüllerden oluşur. Her modülün bir frontend
klasörü (`frontend/src/features/<modül>`) ve bir backend özellik klasörü
(`backend/src/IKPro.Application/Features/<Modül>`) vardır.

| Modül | Rota | Ne yapar | Erişim |
| --- | --- | --- | --- |
| Risk Merkezi | `/dashboard` | Ayrılma/tükenmişlik/uyum risk analitiği + 5 detay ekranı | MGMT |
| Genel Durum | `/overview` | Şirket KPI özeti, departman dağılımı, işe alım hunisi | Herkes |
| Aksiyonlar | `/actions` | Global aksiyon merkezi + denetim izi + arama | Herkes (audit MGMT) |
| Personel | `/personnel` | Directory, kart (6 sekme), evrak/foto yükle-indir | MGMT |
| İşe Alım | `/recruitment` | Aday havuzu → mülakat → **işe al** (Employee'ye dönüşür) | hr-admin |
| Mesai & Puantaj | `/attendance` | Canlı pano, aylık puantaj, manuel giriş, CSV | MGMT |
| İzinler | `/leaves` | Talep, onay kuyruğu, bakiye, ekip yokluk | Herkes |
| Bordro | `/payroll` | Dönem yaşam döngüsü, tekil hesap, pusula PDF, ayarlar | hr-admin + çalışan |
| Uyum | `/risk/compliance` | Evrak/denetim hazırlığı takibi | MGMT |
| Yönetici Konsolu | `/manager` | İzin analitiği + onay paneli | MGMT |
| Ayarlar | `/settings` | Şirket profili, bildirim, güvenlik, abonelik | hr-admin |

("MGMT" = hr-admin + manager.) Rol matrisinin tam hali `frontend/src/routes.tsx`
içinde tek bir dizide tanımlıdır ve backend policy'leriyle bilinçli olarak eşleşir.

## 1.5 Türkçe terimlerle tanışma

İK ve bordro alanına özgü kelimeler bu projede sık geçer. En kritikleri (tam
liste [Bölüm 9](09-sozluk-onboarding.md)):

- **Bordro** — maaş bordrosu; brüt maaştan kesintiler düşülerek net maaşın hesaplandığı belge.
- **Puantaj** — bir ayda kimin kaç gün/saat çalıştığının kaydı (bordronun girdisi).
- **SGK** — Sosyal Güvenlik Kurumu; maaştan kesilen sigorta primi (işçi + işveren payı).
- **Matrah** — bir verginin/kesintinin üzerinden hesaplandığı tutar (ör. gelir vergisi matrahı).
- **Kümülatif matrah** — yıl başından beri biriken gelir vergisi matrahı (vergi dilimini belirler).
- **4/a** — SGK'da "işçi" statüsü (eski adıyla SSK); bordro motoru bu statüye göre hesaplar.

## 1.6 Kısa tarihçe (nasıl bu hale geldi?)

1. **Backend önce** — 12 fazlı bir planla (`raporlar/backend-development-plan.md`)
   .NET API sıfırdan yazıldı: kimlik, personel, izin, puantaj, bordro motoru,
   işe alım, analitik, uyum, aksiyon, ayarlar.
2. **Eski mock frontend** — ürünün "hissi" için ayrı bir statik prototip vardı.
3. **React portu (8 dilim)** — mock prototip, gerçek backend'e bağlı React'e
   dilim dilim taşındı; her dilim TDD + duman testi + parite kontrolüyle bitti.
4. **Kod incelemesi düzeltmeleri** — port bittikten sonra "kullanıma hazır ürün"
   gözüyle inceleme yapıldı; 6 bulgu (1 kritik güvenlik + iş kuralları) giderildi.
   Detay: [Bölüm 8.7](08-kararlar-neden.md#87-kod-i̇nceleme-düzeltmeleri-16).

Bu geçmişin canlı kaydı `docs/gelistirme-gunlugu.md` dosyasındadır; "şu an
neredeyiz" her zaman en üsttedir.

---

Sonraki: [2. Mimari →](02-mimari.md)
