# İK Pro — Geliştirici Rehberi (A'dan Z'ye)

Bu rehber, İK Pro projesine **hiç aşina olmayan** bir geliştiricinin (yeni mezun
junior dahil) okuduğunda projeye **ortalama üstü** hakim olmasını hedefler. Yalnız
"ne var" değil, **"neden böyle yaptık"** sorusunun cevabını da anlatır.

> Kısa tanım: **İK Pro**, 3 rollü (İK yöneticisi / departman yöneticisi / çalışan)
> Türkçe bir **İnsan Kaynakları SaaS demosu**dur. Gerçek bir .NET backend'e bağlı
> bir React arayüzü ve bu arayüzün "birebir kopyalanacak tasarım referansı" olan
> eski bir mock uygulamadan oluşur.

## Nasıl okunmalı?

Bölümler sıralı okunacak şekilde tasarlandı, ama hedefe göre kısayollar:

| Ben kimim / ne istiyorum | Şu sırayla oku |
| --- | --- |
| **İlk günüm, hiçbir şey bilmiyorum** | 01 → 02 → 04 → 05 → 07 → 09 |
| **Backend'e özellik ekleyeceğim** | 02 → 03 → 04 → 06 → 07 |
| **Frontend'e ekran ekleyeceğim** | 05 → 06 → 07 |
| **"Neden bu teknoloji/karar?" merak ediyorum** | 08 (kararlar) + ilgili bölüm |
| **Terim/kısaltma takıldım** | 09 (sözlük) |

## İçindekiler

1. [Genel Bakış — Ürün, roller, modül haritası](01-genel-bakis.md)
2. [Mimari — Monorepo, Clean Architecture, uçtan uca istek yaşam döngüsü](02-mimari.md)
3. [Veri Modeli — Varlıklar, enum'lar, SQL view'ları ve neden](03-veri-modeli.md)
4. [Kimlik & Güvenlik & Hata Yönetimi](04-kimlik-guvenlik-hata.md)
5. [Frontend Mimarisi — React, TanStack Query, rota, tasarım](05-frontend.md)
6. [Modüller — Bordro motoru, izin, işe alım, uyum derinlemesine](06-moduller.md)
7. [Geliştirme Akışı — Kurulum, çalıştırma, test, yeni özellik reçetesi](07-gelistirme-akisi.md)
8. [Kararlar — "Neden böyle yaptık?" (ADR)](08-kararlar-neden.md)
9. [Sözlük & Onboarding — Terimler + ilk hafta yol haritası](09-sozluk-onboarding.md)

## Bu rehberin dışındaki kaynaklar

- `docs/gelistirme-gunlugu.md` — **Kaldığımız yer** ve dilim dilim geçmiş (canlı günlük).
- `docs/superpowers/specs/` — React portu tasarım dokümanı.
- `docs/superpowers/plans/` — Her dilimin (1–8) uygulama planı.
- `raporlar/backend-development-plan.md` — Backend faz planı (12 faz).
- `backend/README.md` — Backend'e özel kurulum/uçlar.
- `legacy-frontend/README.md` — Eski mock uygulamayı çalıştırma.

## En kısa özet (30 saniye)

- **backend/** — .NET 9, Clean Architecture (Domain · Application · Infrastructure · API),
  EF Core + MSSQL, JWT + refresh, MediatR + FluentValidation, QuestPDF (bordro pusulası).
- **frontend/** — React 18 + TypeScript + Vite + TanStack Query; hash router; tipler
  backend Swagger'ından **otomatik** üretilir (`npm run gen:api`).
- **legacy-frontend/** — Orijinal mock uygulama; React portunun **piksel-parite referansı**
  (dokunulmaz).
- Roller: `hr-admin` (her şey), `manager` (yalnız ekibi), `employee` (yalnız kendisi).
- **Bu bir demodur** — bordro/mevzuat hesapları üretimde müşavir doğrulaması ister.
