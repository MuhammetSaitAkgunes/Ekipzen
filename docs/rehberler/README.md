# Ekipzen — Geliştirici Rehberleri

Bu klasör, projeye **sıfırdan** başlayan bir geliştiricinin (yeni mezun dahil)
ortalama üzeri seviyede hakim olması için hazırlanmış rehber setidir. Her dosya
tek bir konuya odaklanır, gerçek kod dosyalarına referans verir ve "neden böyle
yaptık?" sorusunu da yanıtlar.

## Okuma Sırası

Aşağıdaki sırayla okumak en verimlisidir; ama her rehber kendi başına da anlaşılır.

| # | Rehber | Ne öğreneceksin |
| --- | --- | --- |
| 00 | [Projeye Genel Bakış](00-projeye-genel-bakis.md) | Ürün ne, kimler için, teknoloji yığını, büyük resim mimari |
| 01 | [Ortam Kurulumu](01-ortam-kurulumu.md) | Backend + frontend + veritabanını nasıl çalıştırırsın, ilk giriş |
| 02 | [Mimari: Clean Architecture](02-mimari-clean-architecture.md) | Katmanlar, bağımlılık kuralı, bir isteğin yolculuğu |
| 03 | [Backend Derinlemesine](03-backend-derinlemesine.md) | Domain/Application/Infrastructure/API, CQRS, doğrulama, hata yönetimi |
| 04 | [Frontend Derinlemesine](04-frontend-derinlemesine.md) | React yapısı, yönlendirme, TanStack Query, API istemcisi, auth |
| 05 | [Kimlik & Yetkilendirme](05-kimlik-ve-yetkilendirme.md) | JWT, refresh rotasyonu, roller, davet (invite) akışı |
| 06 | [Multi-Tenancy (Çok Kiracılılık)](06-multi-tenancy.md) | TenantId, global filtre, izolasyon, provizyon vs self-servis kayıt |
| 07 | [Veritabanı & Migration'lar](07-veritabani-ve-migrationlar.md) | EF Core, migration ekleme, seed, SQL view/fonksiyon |
| 08 | [Testler](08-testler.md) | Birim vs entegrasyon testi, TDD, testleri çalıştırma |
| 09 | [Yeni Özellik Ekleme (Adım Adım)](09-yeni-ozellik-ekleme-adim-adim.md) | Uçtan uca bir özelliği DB'den ekrana ekleme |
| 10 | [Sözlük & SSS](10-sozluk-ve-sss.md) | Terimler, sık sorular, sorun giderme |

## İlgili Diğer Dokümanlar

- **Kaldığımız yer + geçmiş:** [`../gelistirme-gunlugu.md`](../gelistirme-gunlugu.md)
- **KVKK & veri izolasyonu (derin):** [`../kvkk-veri-izolasyonu.md`](../kvkk-veri-izolasyonu.md)
- **Tasarım kararları & planlar:** [`../superpowers/`](../superpowers/)
- **Kök README (hızlı başlangıç):** [`../../README.md`](../../README.md)

> **Not:** Ekipzen bir **demo/MVP**'dir. Bordro ve mevzuat hesapları örnek amaçlıdır;
> üretim öncesi mali müşavir/hukuk doğrulaması gerekir.
