# 03 — Backend Derinlemesine

Bu rehber, dört backend katmanının içinde ne olduğunu gerçek dosyalarla anlatır.

## Proje Haritası

```
backend/
├─ src/
│  ├─ Ekipzen.Domain/           # Varlıklar, enum'lar, saf iş kuralları
│  │  ├─ Entities/            # Employee, LeaveRequest, Tenant, ...
│  │  ├─ Common/              # BaseEntity, AuditableEntity, ITenantScoped
│  │  ├─ ReadModels/          # SQL view karşılıkları (keyless)
│  │  └─ Services/            # PayrollEngine gibi saf hesap servisleri
│  ├─ Ekipzen.Application/      # Kullanım senaryoları
│  │  ├─ Features/<Modül>/    # Commands/Queries + Validator + Handler + DTO
│  │  └─ Common/
│  │     ├─ Interfaces/       # IApplicationDbContext, IEmailSender, ICurrentUser ...
│  │     ├─ Behaviors/        # ValidationBehavior (MediatR pipeline)
│  │     └─ Exceptions/       # NotFoundException, ConflictException ...
│  ├─ Ekipzen.Infrastructure/   # Dış dünya uygulamaları
│  │  ├─ Persistence/         # AppDbContext, migration'lar, interceptor'lar
│  │  ├─ Identity/            # ApplicationUser, JwtTokenService, IdentityService
│  │  ├─ Email/, Storage/, Pdf/
│  └─ Ekipzen.API/              # HTTP katmanı
│     ├─ Controllers/         # İnce controller'lar
│     ├─ Services/            # CurrentUser, CurrentTenant (HttpContext'ten)
│     └─ Program.cs           # Uygulama başlangıcı, middleware sırası
└─ tests/                     # Unit + Integration testleri
```

## Domain Katmanı

**Sadece iş.** Framework yok, veritabanı yok. Örnek: `Employee.cs` bir POCO sınıftır
ve `AuditableEntity`'den (o da `BaseEntity`'den) türer.

- `BaseEntity` → `Id`, audit alanları ve **`TenantId`** taşır; `ITenantScoped`
  arayüzünü uygular (çok kiracılılığın temeli — bkz. [06](06-multi-tenancy.md)).
- `ReadModels/` → SQL view'lerin C# karşılıkları (birincil anahtarsız/keyless);
  sadece okuma amaçlı raporlar için.

## Application Katmanı

Her modül `Features/<Modül>/` altında toplanır (Attendance, Leaves, Payroll,
Recruitment, Tenancy, ...). Bir uç genellikle **komut/sorgu + validator + handler**
üçlüsüdür (bkz. [02](02-mimari-clean-architecture.md#cqrs-nedir-commandquery-ayrımı)).

**Anahtar arayüzler** (`Common/Interfaces/`), Infrastructure'da uygulanır:
- `IApplicationDbContext` — DbSet'ler + `SaveChangesAsync` (EF Core'a doğrudan bağlanmadan DB erişimi).
- `ICurrentUser` — oturum açmış kullanıcı (`UserId`, `Email`, `Roles`, `EmployeeId`).
- `ICurrentTenant` — aktif kiracı (`TenantId`, `Impersonate`).
- `IEmailSender`, `IFileStorage`, `IIdentityService`, `ITenantPurger`.

**Doğrulama pipeline'ı:** `AddApplication()` (bkz. `Ekipzen.Application/DependencyInjection.cs`)
tüm FluentValidation validator'larını kaydeder ve `ValidationBehavior<,>`'i MediatR
pipeline'ına ekler. Böylece her komut, handler'a girmeden **otomatik** doğrulanır —
handler içinde elle doğrulama yazmaya gerek yoktur.

## Infrastructure Katmanı

Application'daki arayüzlerin gerçek uygulamaları burada:

- **`Persistence/AppDbContext.cs`** — EF Core context; tüm DbSet'ler, model
  konfigürasyonu ve **kiracı global filtresi** burada (reflection'la her
  `ITenantScoped` tipe otomatik `WHERE TenantId = @current` uygular).
- **`Persistence/Interceptors/AuditableEntityInterceptor.cs`** — kaydetme anında
  audit alanlarını ve `TenantId`'yi otomatik damgalar.
- **`Identity/JwtTokenService.cs`** — JWT üretimi (claim'ler: sub, name, email,
  role, employeeId, **tenant**); **`IdentityService.cs`** — login/refresh/logout,
  şifre değiştirme, davet akışı. **Kiracı DURUMUNU değiştirmez** — durum geçişleri
  provizyon yolunun işidir (bkz. [06](06-multi-tenancy.md)).
- **`Email/` `Storage/` `Pdf/`** — SMTP/outbox e-posta, yerel dosya deposu, QuestPDF bordro pusulası.

`Ekipzen.Infrastructure/DependencyInjection.cs` (`AddInfrastructure`) tüm bunları DI'a
kaydeder; e-posta göndericisi `Email:Mode`'a göre seçilir (outbox vs SMTP).

## API Katmanı

- **Controller'lar ince'dir:** yalnız komutu/sorguyu `sender.Send(...)` ile iletir.
  Örnek `MeController`:

  ```csharp
  [HttpGet("data-export")]
  [Authorize]
  public async Task<IActionResult> DataExport(CancellationToken ct)
  {
      var (content, fileName) = await sender.Send(new GetMyDataExportQuery(), ct);
      return File(content, "application/json", fileName);
  }
  ```

- **`Program.cs`** başlangıç sırasını kurar. Middleware sırası **önemlidir**:

  ```
  UseExceptionHandler → UseSerilogRequestLogging → (Swagger dev)
  → UseHttpsRedirection → UseCors → UseRateLimiter
  → UseAuthentication → UseAuthorization → MapControllers
  ```

- **Yetkilendirme:** `[Authorize(Policy = Policies.HrAdminOnly)]` gibi policy'ler
  rol matrisini uygular; `[EnableRateLimiting("auth")]` brute-force'u sınırlar.

## Hata Yönetimi

İş kodu **exception fırlatır**, HTTP kodu düşünmez:

| Exception | HTTP | Ne zaman |
| --- | --- | --- |
| `ValidationException` | 400 | FluentValidation kuralı ihlali |
| `UnauthorizedException` | 401 | Kimlik doğrulama başarısız |
| `ForbiddenAccessException` | 403 | Yetki kapsamı dışı |
| `NotFoundException` | 404 | Kayıt yok |
| `ConflictException` | 409 | Çakışma/iş kuralı ihlali |

`GlobalExceptionHandler` bunları standart **`ProblemDetails`** JSON'una çevirir.
Böylece frontend her hatayı tek biçimde işler.

## Sonraki Adım

Arayüz tarafı → [04 — Frontend Derinlemesine](04-frontend-derinlemesine.md).
