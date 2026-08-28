# Özellik Doğrulama (ÖD)

Her özellik **bir kez**, iki katmanda doğrulanır. Değişiklik başına değil — özellik
başına.

## Neden

247 testimiz vardı ve `PurgeUnverifiedAsync`'in koruması sessizce kalkmıştı,
çünkü o davranışın hiç testi yoktu. `TenantFileIsolationTests` ise doğru olduğu
*tesadüfen* doğru olan bir şeyi sınıyordu. Testler **değişikliği** takip ettiği
için çok sayıdalar ama hiçbiri "bu özellik bütün olarak doğru mu" sorusunu
cevaplamıyor.

ÖD bu boşluğu kapatır: soru "ne değişti" değil, **"bu özellik doğru mu ve
doğruluğunun kanıtı ne"**.

## Katman 1 — Mantık (senaryo yürüyüşü)

Özellik, **gerçek API üzerinden, gerçek bir kiracıda, baştan sona bir iş hikâyesi
olarak** yürütülür. "Uç 200 dönüyor mu" değil; "İK müdürü bu işi gerçekten
yapabiliyor mu, sonuç mantıklı mı, mevzuata uygun mu".

Her adımda üç soru: **ne olmalı, ne oluyor, eksik olan ne.**

Bu katman kodu okuyarak yapılamaz — çalıştırmayı zorunlu kılar. Kasıtlı:
"okudum, doğru görünüyor" bu projede birkaç kez yanıldı.

## Katman 2 — Pratik kod kalitesi

Sabit kontrol listesi; maddeler bu projede gerçekten kırılan şeylerden türetildi:

1. **Kiracı izolasyonu** — ambient context kullanımı var mı, yazma yolu da izole mi
2. **Yetki kapsamı** — rol + satır düzeyi (yönetici yalnız kendi ekibi)
3. **Doğrulama** — sınır değerler, negatif/boş, çakışma, tarih aralıkları
4. **İşlem sınırı** — çok adımlı yazma yarıda kalırsa geriye ne kalıyor, kurtarma yolu var mı
5. **N+1 / kartezyen / indeks** — sorgu maliyeti veriyle nasıl büyüyor
6. **Hata zarfı** — 400/403/404/409 doğru mu, iç detay sızıyor mu
7. **Audit izi** — kritik değişiklik iz bırakıyor mu
8. **Test boşluğu** — bu özelliğin hangi davranışı test EDİLMEMİŞ

## Bulgu akışı

Bulgular **önce raporlanır, düzeltme onayla yapılır** (kullanıcı kararı).
Böylece ÖD'nin kapsamı kendiliğinden büyümez.

Her bulgu: `kimlik | katman | önem | NEREDE (dosya/metot) | durum`.
Konum bilgisi zorunlu: düzeltme aşamasında bulguyu yeniden araştırmak zorunda
kalmamak için.
Önem: **KRİTİK** (güvenlik/veri kaybı) · **YÜKSEK** (müşteriye zarar verir) ·
**ORTA** (veri/kullanım bozukluğu) · **DÜŞÜK** (kalite borcu).
Durum: `açık` · `düzeltildi` · `kabul edildi (gerekçe)` · `dış girdi bekliyor`.

## "Bitti" tanımı

Bir özellik, **ÖD belgesi olmadan bitmiş sayılmaz.** Belgedeki her bulgu ya
düzeltilmiş ya da gerekçesiyle kabul edilmiş olmalıdır.

## Durum

| Özellik | Uç | Test | ÖD | Açık bulgu |
| --- | --- | --- | --- | --- |
| [Recruitment](recruitment.md) | 10 | 5 | ✅ 2026-08-28 | 10 |
| [Payroll](payroll.md) | 13 | 12 | ✅ 2026-08-28 | 10 açık / 2 çözülmüş |
| [Employees](employees.md) | 13 | 39 | ✅ 2026-08-28 | 5 açık / 1 çözülmüş |
| [Leaves](leaves.md) | 9 | 9 | ✅ 2026-08-13 | 8 |
| [Actions](kalan-alanlar.md) | 8 | 8 | ✅ 2026-08-28 | 0 açık / 1 çözülmüş |
| [Dashboard](kalan-alanlar.md) | 7 | 7 | ✅ 2026-08-13 | 0 |
| [Settings](kalan-alanlar.md) | 6 | 4 | ✅ 2026-08-13 | 0 |
| [Compliance](kalan-alanlar.md) | 6 | 8 | ✅ 2026-08-28 | 1 açık / 1 çözülmüş |
| [Attendance](attendance.md) | 5 | 6 | ✅ 2026-08-28 | 7 açık / 1 çözülmüş |
| [Departments](kalan-alanlar.md) | 4 | — | ✅ 2026-08-13 | 1 |
| Auth / Tenancy | 15 | 121 | ⬜ (üç fazdır denetleniyor) | — |
| **Çapraz kesen** | — | 1 | ✅ 2026-08-28 | 0 açık / **1 çözülmüş** (X-1) |

**Toplam: 49 tespit · 42 açık bulgu — 0 KRİTİK, 12 YÜKSEK.**
Hepsi tek listede: [**bulgular.md**](bulgular.md) — düzeltme planı buradan yürür.
