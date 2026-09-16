# 10 — Sözlük & SSS

## Sözlük (Terimler)

| Terim | Anlamı |
| --- | --- |
| **Tenant (Kiracı)** | Bir müşteri şirket. Her kiracının verisi izoledir. |
| **Multi-tenant** | Birçok kiracının aynı uygulamayı/DB'yi paylaşması; veri `TenantId` ile ayrılır. |
| **Clean Architecture** | Katmanlı mimari; bağımlılık içe (Domain'e) akar. |
| **CQRS** | Command (yazma) / Query (okuma) ayrımı. Her ikisi MediatR üzerinden. |
| **MediatR** | İstek → handler yönlendiren kütüphane; controller ile iş mantığını gevşek bağlar. |
| **Handler** | Bir komut/sorgunun asıl işini yapan sınıf. |
| **DTO** | Data Transfer Object — API'ye giren/çıkan sade veri nesnesi (varlık değil). |
| **Global query filter** | EF Core'un her sorguya otomatik eklediği `WHERE TenantId=...` koşulu. |
| **ITenantScoped** | "Bu tip kiracıya bağlı" işareti; global filtreyi tetikler. |
| **Impersonate** | HTTP dışı bağlamda (seed, arka plan) aktif kiracıyı elle ayarlama. |
| **Provizyon** | Platform anahtarıyla yeni kiracı oluşturma (iç işlem). |
| **Self-servis kayıt** | Müşterinin public formdan kendi şirketini oluşturması. |
| **Davet (invite) akışı** | Kullanıcı şifresiz oluşur, e-postadaki token'la şifre belirler. |
| **Purge** | Bir kiracının tüm verisini kalıcı silme (KVKK). |
| **JWT** | İmzalı erişim token'ı; claim'ler (rol, tenant vb.) taşır. |
| **Refresh rotasyonu** | Her token yenilemede eski refresh token'ı iptal edip yeni çift üretme. |
| **Seed** | Veritabanına örnek/başlangıç verisi yükleme. |
| **Migration** | Şema değişikliğini kodla ifade eden, uygulanabilir adım. |
| **Read model / View** | Yalnız okuma amaçlı, çoğu zaman bir SQL view'e karşılık gelen tip. |
| **ProblemDetails** | RFC 7807 standart hata JSON formatı. |

## Sık Sorulan Sorular

**S: Yeni bir tabloya `TenantId` filtresini nasıl eklerim?**
Eklemene gerek yok. Varlık `BaseEntity`'den türesin; global filtre otomatik uygulanır ([06](06-multi-tenancy.md)).

**S: Handler'da doğrulamayı elle mi çağırayım?**
Hayır. FluentValidation validator'ı yaz; `ValidationBehavior` pipeline'ı otomatik çalıştırır ([03](03-backend-derinlemesine.md)).

**S: Frontend'de tip hatası alıyorum, DTO'yu değiştirdim.**
Backend çalışırken `npm run gen:api` çalıştır — tipler Swagger'dan yeniden üretilir ([04](04-frontend-derinlemesine.md)).

**S: Controller'a iş mantığı yazabilir miyim?**
Hayır. Controller ince olmalı — yalnız `sender.Send(...)`. İş mantığı handler'da ([02](02-mimari-clean-architecture.md)).

**S: Yeni bir SQL view eklerken nelere dikkat etmeliyim?**
View EF global filtresini baypas eder; `TenantId` sütununu **elle** taşımalı, fonksiyonlar `@tenantId` almalı ([07](07-veritabani-ve-migrationlar.md)).

**S: HTTP durum kodunu nerede belirliyorum?**
İş kodunda değil — uygun exception'ı fırlat (`NotFoundException`, `ConflictException`...); `GlobalExceptionHandler` doğru kodu üretir ([03](03-backend-derinlemesine.md)).

**S: Bir kullanıcı başka çalışanın verisini görebilir mi?**
Hayır. İki katman korur: policy (rol) + sorgu kapsamı (`ICurrentUser.EmployeeId`) ([05](05-kimlik-ve-yetkilendirme.md)).

## Sorun Giderme

| Belirti | Çözüm |
| --- | --- |
| Backend açılışta migration/DB hatası | SQL Server çalışıyor mu? `ConnectionStrings__DefaultConnection` doğru mu? |
| `dotnet ef` bulunamadı | `dotnet tool install --global dotnet-ef` |
| Entegrasyon testi "SqlException" | Test DB'sine (`EkipzenDb_Test`) erişim yok; SQL Server açık olmalı. |
| Frontend 401 döngüsü | Refresh token süresi dolmuş; çıkış yapıp yeniden gir. |
| Davet/SMTP e-postası gelmiyor (dev) | `Email:Mode=outbox` — e-posta diske yazılır: `App_Data/storage/outbox`. |
| Kiracı testinde slug hatası | Slug yalnız `[a-z0-9-]`; Türkçe/aksanlı harfler türetilirken düşürülür/çevrilir. |
| Yeni özellik testte 404 | Controller route'u ve `[Authorize]` policy'sini kontrol et; DI kaydı gerekiyor mu? |

## Nereden Devam?

- Kaldığımız yer ve tüm geçmiş: [`../gelistirme-gunlugu.md`](../gelistirme-gunlugu.md)
- KVKK & güvenlik derinlemesine: [`../kvkk-veri-izolasyonu.md`](../kvkk-veri-izolasyonu.md)
- Tasarım kararları & planlar: [`../superpowers/`](../superpowers/)
