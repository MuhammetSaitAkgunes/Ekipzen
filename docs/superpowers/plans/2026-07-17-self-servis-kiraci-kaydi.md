# Self-Servis Kiracı Kaydı Implementasyon Planı

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Müşterilerin platform anahtarı olmadan kendi şirketlerini (kiracı) ve ilk hr-admin hesaplarını public bir formdan oluşturabilmesi; hesap e-posta doğrulaması (davet kabul) ile etkinleşir.

**Architecture:** Mevcut provizyon + davet altyapısı yeniden kullanılır. Yeni bir `RegisterTenantCommand` sunucu tarafında slug türetir, kiracıyı **pasif** oluşturur ve mevcut davet e-postası akışını tetikler. İlk hr-admin daveti kabul edince (`accept-invite`) kiracı **etkinleşir** — böylece doğrulanmamış kayıtlar pasif kalır. Uç public'tir ama daha sıkı bir `signup` rate-limit politikasıyla korunur.

**Tech Stack:** .NET 9, MediatR, FluentValidation, EF Core, ASP.NET rate limiting; React 18 + TS, TanStack Query, Vitest.

## Global Constraints

- Kiracı izolasyonu bozulmaz: yeni kiracı yalnız kendi verisini görür (mevcut global filtre).
- Mevcut **provizyon** davranışı (platform-key, `IsActive = true`) ve tüm mevcut testler değişmeden yeşil kalmalı.
- Sırlar/limitler yapılandırmadan gelir; testler limiti yüksek env değeriyle etkisizleştirir.
- Commit mesajı sonu: `Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>`.
- Demo şifresi `demo123`; davet token'ı e-posta gövdesinde `DAVET-KODU:` satırında.
- Dal: `main`'den `feature/self-service-signup`.

---

## Dosya Yapısı

- **Create:** `backend/src/Ekipzen.Application/Features/Tenancy/TenantSlug.cs` — şirket adından ASCII slug türetme (saf fonksiyon, unit-test edilebilir).
- **Create:** `backend/src/Ekipzen.Application/Features/Tenancy/TenantOnboarding.cs` — kiracı+admin oluşturma ortak yardımcısı (DRY: provizyon ve self-servis paylaşır).
- **Create:** `backend/src/Ekipzen.Application/Features/Tenancy/Commands/RegisterTenantCommand.cs` — self-servis kayıt komutu + validator + handler.
- **Modify:** `backend/src/Ekipzen.Application/Features/Tenancy/Commands/ProvisionTenantCommand.cs` — ortak yardımcıyı kullan (aktif kiracı).
- **Modify:** `backend/src/Ekipzen.Infrastructure/Identity/IdentityService.cs` — `AcceptInviteAsync` pasif kiracıyı etkinleştirir.
- **Modify:** `backend/src/Ekipzen.API/Controllers/TenancyController.cs` — `POST /api/tenants/signup`.
- **Modify:** `backend/src/Ekipzen.API/Program.cs` — `signup` rate-limit politikası.
- **Modify:** `backend/tests/Ekipzen.Tests.Integration/EkipzenApiFactory.cs` — `RateLimiting__SignupPermitPerHour` yüksek.
- **Create:** `backend/tests/Ekipzen.Tests.Unit/Tenancy/TenantSlugTests.cs` — slug türetme birim testleri.
- **Create:** `backend/tests/Ekipzen.Tests.Integration/Tenancy/SelfServiceSignupTests.cs` — uçtan uca kayıt+doğrulama.
- **Create:** `frontend/src/auth/CompanySignupPage.tsx` — public "şirketini kaydet" formu + başarı ekranı.
- **Create:** `frontend/src/auth/CompanySignupPage.test.tsx` — birim testleri.
- **Modify:** `frontend/src/routes.tsx` — `/register-company` route.
- **Modify:** `frontend/src/auth/LoginPage.tsx` — kayıt sayfasına bağlantı.
- **Modify:** `docs/gelistirme-gunlugu.md` — kayıt.

---

## Task 1: Slug türetme (saf fonksiyon)

**Files:**
- Create: `backend/src/Ekipzen.Application/Features/Tenancy/TenantSlug.cs`
- Test: `backend/tests/Ekipzen.Tests.Unit/Tenancy/TenantSlugTests.cs`

**Interfaces:**
- Produces: `static string TenantSlug.From(string companyName)` → `[a-z0-9-]+`, ≤64, boşsa `"sirket"`.

- [ ] **Step 1: Failing test yaz**

```csharp
// backend/tests/Ekipzen.Tests.Unit/Tenancy/TenantSlugTests.cs
using FluentAssertions;
using Ekipzen.Application.Features.Tenancy;
using Xunit;

namespace Ekipzen.Tests.Unit.Tenancy;

public class TenantSlugTests
{
    [Theory]
    [InlineData("Acme Teknoloji A.Ş.", "acme-teknoloji-a")]
    [InlineData("Globex   Bilişim", "globex-bilisim")]
    [InlineData("Ekipzen İK", "ekipzen-k")]
    [InlineData("!!!", "sirket")]
    [InlineData("", "sirket")]
    public void From_ProducesSlug(string input, string expected)
        => TenantSlug.From(input).Should().Be(expected);

    [Fact]
    public void From_Truncates_To64()
        => TenantSlug.From(new string('a', 100)).Length.Should().BeLessThanOrEqualTo(64);
}
```

Not: "Bilişim" → "bili-im" değil "bilisim" bekleniyor; Türkçe `ş/ı/İ` gibi harfler için basit transliterasyon (ş→s, ı→i, İ→i, ğ→g, ü→u, ö→o, ç→c) uygulanır, sonra kalan ASCII-dışı karakterler tireye döner. "Ekipzen İK" → transliterasyon "ekipzen ik" beklenir; ancak `İ.ToLowerInvariant()` kültüre bağlı sorunlu olduğundan önce transliterasyon map'i uygulanır → "ekipzen-ik". **Beklenen değeri buna göre yaz:** `[InlineData("Ekipzen İK", "ekipzen-ik")]`. (Yukarıdaki `"ekipzen-k"` yerine `"ekipzen-ik"` kullan.)

- [ ] **Step 2: Testi çalıştır, başarısızlığı gör**

Run: `cd backend && dotnet test tests/Ekipzen.Tests.Unit --filter TenantSlugTests`
Expected: FAIL (TenantSlug yok / derleme hatası).

- [ ] **Step 3: Minimal implementasyon**

```csharp
// backend/src/Ekipzen.Application/Features/Tenancy/TenantSlug.cs
using System.Text;
using System.Text.RegularExpressions;

namespace Ekipzen.Application.Features.Tenancy;

/// <summary>
/// Şirket adından URL/alt-alan dostu slug türetir: Türkçe harfleri transliterasyonla
/// ASCII'ye çevirir, kalanları tireye indirir. Saf fonksiyon (self-servis kayıtta kullanılır).
/// </summary>
public static class TenantSlug
{
    private const int MaxLength = 64;

    private static readonly Dictionary<char, char> TurkishMap = new()
    {
        ['ş'] = 's', ['Ş'] = 's', ['ı'] = 'i', ['İ'] = 'i', ['ğ'] = 'g', ['Ğ'] = 'g',
        ['ü'] = 'u', ['Ü'] = 'u', ['ö'] = 'o', ['Ö'] = 'o', ['ç'] = 'c', ['Ç'] = 'c',
    };

    public static string From(string? companyName)
    {
        var source = companyName ?? string.Empty;
        var sb = new StringBuilder(source.Length);
        foreach (var ch in source)
        {
            var c = TurkishMap.TryGetValue(ch, out var mapped) ? mapped : char.ToLowerInvariant(ch);
            sb.Append((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') ? c : '-');
        }

        var slug = Regex.Replace(sb.ToString(), "-+", "-").Trim('-');
        if (slug.Length > MaxLength) slug = slug[..MaxLength].Trim('-');
        return string.IsNullOrEmpty(slug) ? "sirket" : slug;
    }
}
```

- [ ] **Step 4: Testi çalıştır, geçtiğini gör**

Run: `cd backend && dotnet test tests/Ekipzen.Tests.Unit --filter TenantSlugTests`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git checkout -b feature/self-service-signup
git add backend/src/Ekipzen.Application/Features/Tenancy/TenantSlug.cs backend/tests/Ekipzen.Tests.Unit/Tenancy/TenantSlugTests.cs
git commit -m "feat(tenancy): şirket adından slug türetme (self-servis kayıt hazırlık)"
```

---

## Task 2: Ortak onboarding yardımcısı + RegisterTenantCommand

**Files:**
- Create: `backend/src/Ekipzen.Application/Features/Tenancy/TenantOnboarding.cs`
- Create: `backend/src/Ekipzen.Application/Features/Tenancy/Commands/RegisterTenantCommand.cs`
- Modify: `backend/src/Ekipzen.Application/Features/Tenancy/Commands/ProvisionTenantCommand.cs`

**Interfaces:**
- Consumes: `IIdentityService.EmailExistsAsync`, `IIdentityService.CreateTenantAdminAsync(int tenantId, string name, string email, string companyName, CancellationToken)`, `IApplicationDbContext.Tenants`, `TenantSlug.From`.
- Produces: `RegisterTenantCommand(string CompanyName, string AdminName, string AdminEmail) : IRequest<RegisterTenantResult>`; `RegisterTenantResult(string Slug, string AdminEmail)`; `TenantOnboarding.CreateWithAdminAsync(...)`.

- [ ] **Step 1: Ortak yardımcıyı yaz**

```csharp
// backend/src/Ekipzen.Application/Features/Tenancy/TenantOnboarding.cs
using Ekipzen.Application.Common.Exceptions;
using Ekipzen.Application.Common.Interfaces;
using Ekipzen.Domain.Entities.Tenancy;

namespace Ekipzen.Application.Features.Tenancy;

/// <summary>
/// Kiracı + ilk hr-admin oluşturmanın ortak adımları (provizyon ve self-servis paylaşır).
/// Admin şifresiz oluşturulur; davet e-postası CreateTenantAdminAsync içinde gönderilir.
/// </summary>
public static class TenantOnboarding
{
    public static async Task<Tenant> CreateWithAdminAsync(
        IApplicationDbContext context,
        IIdentityService identityService,
        string companyName,
        string slug,
        string adminName,
        string adminEmail,
        bool isActive,
        CancellationToken cancellationToken)
    {
        // Admin e-postasını önce doğrula — kiracı yazılmadan çakışmayı yakala (orphan önlenir).
        if (await identityService.EmailExistsAsync(adminEmail, cancellationToken))
        {
            throw new ConflictException($"'{adminEmail}' e-postasıyla kayıtlı bir hesap zaten var.");
        }

        var tenant = new Tenant
        {
            Name = companyName,
            Slug = slug,
            IsActive = isActive,
            CreatedAtUtc = DateTime.UtcNow,
        };
        context.Tenants.Add(tenant);
        await context.SaveChangesAsync(cancellationToken);

        await identityService.CreateTenantAdminAsync(tenant.Id, adminName, adminEmail, tenant.Name, cancellationToken);
        return tenant;
    }
}
```

- [ ] **Step 2: ProvisionTenant'ı ortak yardımcıya taşı (davranış aynı: aktif kiracı)**

`ProvisionTenantCommandHandler.Handle` gövdesini değiştir (slug çakışma kontrolü kalır; email-kontrolü+oluşturma yardımcıya devreder):

```csharp
public async Task<ProvisionTenantResult> Handle(
    ProvisionTenantCommand request, CancellationToken cancellationToken)
{
    var slug = request.Slug.Trim().ToLowerInvariant();
    if (await context.Tenants.AnyAsync(t => t.Slug == slug, cancellationToken))
    {
        throw new ConflictException($"'{slug}' kısa adıyla bir şirket zaten var.");
    }

    var tenant = await TenantOnboarding.CreateWithAdminAsync(
        context, identityService,
        request.CompanyName.Trim(), slug,
        request.AdminName.Trim(), request.AdminEmail.Trim(),
        isActive: true, cancellationToken);

    return new ProvisionTenantResult(tenant.Id, tenant.Slug, request.AdminEmail.Trim());
}
```

(Artık kullanılmayan `using`'leri bırak; `IApplicationDbContext`/`IIdentityService` ctor'da mevcut.)

- [ ] **Step 3: RegisterTenantCommand yaz**

```csharp
// backend/src/Ekipzen.Application/Features/Tenancy/Commands/RegisterTenantCommand.cs
using FluentValidation;
using Ekipzen.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Ekipzen.Application.Features.Tenancy.Commands;

/// <summary>
/// Self-servis kayıt: müşteri kendi şirketini ve ilk hr-admin'ini public formdan oluşturur.
/// Platform anahtarı GEREKMEZ; kötüye kullanım 'signup' rate-limit'iyle sınırlanır.
/// Kiracı PASİF oluşturulur; admin davet e-postasını kabul edince (accept-invite) etkinleşir.
/// </summary>
public sealed record RegisterTenantCommand(string CompanyName, string AdminName, string AdminEmail)
    : IRequest<RegisterTenantResult>;

public sealed record RegisterTenantResult(string Slug, string AdminEmail);

public sealed class RegisterTenantCommandValidator : AbstractValidator<RegisterTenantCommand>
{
    public RegisterTenantCommandValidator()
    {
        RuleFor(x => x.CompanyName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.AdminName).NotEmpty().MaximumLength(128);
        RuleFor(x => x.AdminEmail).NotEmpty().EmailAddress().MaximumLength(256);
    }
}

public sealed class RegisterTenantCommandHandler(IApplicationDbContext context, IIdentityService identityService)
    : IRequestHandler<RegisterTenantCommand, RegisterTenantResult>
{
    public async Task<RegisterTenantResult> Handle(RegisterTenantCommand request, CancellationToken cancellationToken)
    {
        var slug = await GenerateUniqueSlugAsync(request.CompanyName, cancellationToken);

        var tenant = await TenantOnboarding.CreateWithAdminAsync(
            context, identityService,
            request.CompanyName.Trim(), slug,
            request.AdminName.Trim(), request.AdminEmail.Trim(),
            isActive: false, cancellationToken);

        return new RegisterTenantResult(tenant.Slug, request.AdminEmail.Trim());
    }

    private async Task<string> GenerateUniqueSlugAsync(string companyName, CancellationToken cancellationToken)
    {
        var baseSlug = TenantSlug.From(companyName);
        var candidate = baseSlug;
        var suffix = 2;
        while (await context.Tenants.AnyAsync(t => t.Slug == candidate, cancellationToken))
        {
            candidate = $"{baseSlug}-{suffix++}";
        }
        return candidate;
    }
}
```

- [ ] **Step 4: Derle**

Run: `cd backend && dotnet build`
Expected: 0 Hata.

- [ ] **Step 5: Mevcut provizyon testleri hâlâ yeşil**

Run: `cd backend && dotnet test tests/Ekipzen.Tests.Integration --filter "FullyQualifiedName~Tenancy"`
Expected: PASS (davranış değişmedi).

- [ ] **Step 6: Commit**

```bash
git add backend/src/Ekipzen.Application/Features/Tenancy/
git commit -m "feat(tenancy): ortak onboarding yardımcısı + RegisterTenantCommand (pasif kiracı)"
```

---

## Task 3: accept-invite pasif kiracıyı etkinleştirir

**Files:**
- Modify: `backend/src/Ekipzen.Infrastructure/Identity/IdentityService.cs`

**Interfaces:**
- Consumes: `AppDbContext context` (ctor'da mevcut), `ApplicationUser.TenantId`.
- Produces: davranış — `AcceptInviteAsync` sonrası kullanıcının kiracısı `IsActive = true`.

- [ ] **Step 1: `AcceptInviteAsync`'e etkinleştirme ekle**

`ResetPasswordAsync` başarı bloğundan sonra:

```csharp
// Şifre belirlendi = e-posta doğrulandı. Self-servis pasif kiracıyı ilk admin
// kabulünde etkinleştir (aktif kiracıda no-op; idempotent).
var tenant = await context.Tenants.FirstOrDefaultAsync(
    t => t.Id == user.TenantId, cancellationToken);
if (tenant is { IsActive: false })
{
    tenant.IsActive = true;
    await context.SaveChangesAsync(cancellationToken);
}
```

Gerekli `using Microsoft.EntityFrameworkCore;` dosyada yoksa ekle. (`context.Tenants` global filtreye tabi değildir — Tenant `ITenantScoped` değil.)

- [ ] **Step 2: Derle**

Run: `cd backend && dotnet build`
Expected: 0 Hata.

- [ ] **Step 3: Commit**

```bash
git add backend/src/Ekipzen.Infrastructure/Identity/IdentityService.cs
git commit -m "feat(tenancy): davet kabulü pasif kiracıyı etkinleştirir (e-posta doğrulama kapısı)"
```

---

## Task 4: signup ucu + rate-limit politikası + entegrasyon testleri

**Files:**
- Modify: `backend/src/Ekipzen.API/Controllers/TenancyController.cs`
- Modify: `backend/src/Ekipzen.API/Program.cs`
- Modify: `backend/tests/Ekipzen.Tests.Integration/EkipzenApiFactory.cs`
- Create: `backend/tests/Ekipzen.Tests.Integration/Tenancy/SelfServiceSignupTests.cs`

**Interfaces:**
- Consumes: `RegisterTenantCommand`, `TenancyTestBase.AcceptInviteAsync`, `AuthedClientAsync`.
- Produces: `POST /api/tenants/signup` → 201 `RegisterTenantResult`; `signup` rate-limit policy.

- [ ] **Step 1: Failing entegrasyon testi yaz**

```csharp
// backend/tests/Ekipzen.Tests.Integration/Tenancy/SelfServiceSignupTests.cs
using FluentAssertions;
using Ekipzen.Application.Features.Departments;
using Ekipzen.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;

namespace Ekipzen.Tests.Integration.Tenancy;

[Collection(ApiCollection.Name)]
public sealed class SelfServiceSignupTests(EkipzenApiFactory factory) : TenancyTestBase(factory)
{
    private Task<HttpResponseMessage> SignupAsync(string company, string email) =>
        Factory.CreateClient().PostAsJsonAsync("/api/tenants/signup", new
        {
            companyName = company,
            adminName = "Kurucu Yönetici",
            adminEmail = email,
        });

    [Fact]
    public async Task Signup_CreatesInactiveTenant_ActivatedOnInviteAccept()
    {
        var email = $"kurucu-{Guid.NewGuid():N}@yenisirket.local";
        var response = await SignupAsync("Yeni Şirket A.Ş.", email);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        // Doğrulanmadan giriş yapılamaz (kiracı pasif).
        var preLogin = await Factory.CreateClient().PostAsJsonAsync("/api/auth/login",
            new { email, password = "demo123" });
        preLogin.StatusCode.Should().Be(HttpStatusCode.Unauthorized, "doğrulanmamış kiracı pasif");

        // Davet kabulü kiracıyı etkinleştirir → giriş çalışır, yalnız kendi (boş) kiracısını görür.
        await AcceptInviteAsync(email);
        var admin = await AuthedClientAsync(email);
        var depts = await GetAsync<List<DepartmentDto>>(admin, "/api/departments");
        depts.Should().BeEmpty();
    }

    [Fact]
    public async Task Signup_DuplicateEmail_Returns409()
    {
        var email = $"dup-{Guid.NewGuid():N}@x.local";
        (await SignupAsync("İlk Şirket", email)).StatusCode.Should().Be(HttpStatusCode.Created);
        (await SignupAsync("İkinci Şirket", email)).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Signup_SameCompanyName_DerivesDistinctSlugs()
    {
        await SignupAsync("Paralel A.Ş.", $"a-{Guid.NewGuid():N}@p.local");
        await SignupAsync("Paralel A.Ş.", $"b-{Guid.NewGuid():N}@p.local");

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var slugs = await db.Tenants.Where(t => t.Name == "Paralel A.Ş.")
            .Select(t => t.Slug).ToListAsync();
        slugs.Should().HaveCountGreaterThanOrEqualTo(2);
        slugs.Should().OnlyHaveUniqueItems("aynı ad için slug'lar benzersiz türetilmeli");
    }
}
```

- [ ] **Step 2: Testi çalıştır, başarısızlığı gör**

Run: `cd backend && dotnet test tests/Ekipzen.Tests.Integration --filter "FullyQualifiedName~SelfServiceSignup"`
Expected: FAIL (endpoint 404).

- [ ] **Step 3: signup rate-limit politikası (Program.cs)**

`auth` politikasından sonra ekle:

```csharp
var signupPermitPerHour = builder.Configuration.GetValue("RateLimiting:SignupPermitPerHour", 10);
options.AddPolicy("signup", httpContext =>
    RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = signupPermitPerHour,
            Window = TimeSpan.FromHours(1),
            QueueLimit = 0,
        }));
```

- [ ] **Step 4: signup ucu (TenancyController.cs)**

`using Ekipzen.Application.Features.Tenancy.Commands;` mevcut. `Provision` metodundan sonra ekle:

```csharp
/// <remarks>Self-servis kayıt (public, platform anahtarı yok, 'signup' rate-limit'li).
/// Kiracı pasif oluşturulur; admin davet e-postasını kabul edince etkinleşir.</remarks>
[HttpPost("signup")]
[EnableRateLimiting("signup")]
[ProducesResponseType<RegisterTenantResult>(StatusCodes.Status201Created)]
[ProducesResponseType(StatusCodes.Status400BadRequest)]
[ProducesResponseType(StatusCodes.Status409Conflict)]
public async Task<ActionResult<RegisterTenantResult>> Signup(
    RegisterTenantCommand command, CancellationToken cancellationToken)
    => StatusCode(StatusCodes.Status201Created, await sender.Send(command, cancellationToken));
```

- [ ] **Step 5: Test factory signup limitini etkisizleştir**

`EkipzenApiFactory` ctor'unda diğer env satırlarının yanına:

```csharp
Environment.SetEnvironmentVariable("RateLimiting__SignupPermitPerHour", "1000000");
```

- [ ] **Step 6: Testleri çalıştır, geçtiğini gör**

Run: `cd backend && dotnet test tests/Ekipzen.Tests.Integration --filter "FullyQualifiedName~SelfServiceSignup"`
Expected: PASS (3 test).

- [ ] **Step 7: Tüm backend paketi yeşil**

Run: `cd backend && dotnet test`
Expected: PASS.

- [ ] **Step 8: Commit**

```bash
git add backend/src/Ekipzen.API/ backend/tests/Ekipzen.Tests.Integration/
git commit -m "feat(tenancy): POST /api/tenants/signup + signup rate-limit + entegrasyon testleri"
```

---

## Task 5: Frontend — şirket kayıt sayfası

**Files:**
- Create: `frontend/src/auth/CompanySignupPage.tsx`
- Create: `frontend/src/auth/CompanySignupPage.test.tsx`
- Modify: `frontend/src/routes.tsx`
- Modify: `frontend/src/auth/LoginPage.tsx`

**Interfaces:**
- Consumes: `apiFetch<T>(path, init)` (`../api/client`), `PublicOnly` guard.
- Produces: `/register-company` route; `CompanySignupPage` bileşeni.

- [ ] **Step 1: Failing birim testi yaz**

```tsx
// frontend/src/auth/CompanySignupPage.test.tsx
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { afterEach, beforeEach, expect, test, vi } from "vitest";
import { CompanySignupPage } from "./CompanySignupPage";

beforeEach(() => {
  localStorage.clear();
  vi.stubGlobal("fetch", vi.fn());
});
afterEach(() => vi.unstubAllGlobals());

const renderPage = () =>
  render(
    <MemoryRouter>
      <CompanySignupPage />
    </MemoryRouter>,
  );

test("başarılı kayıtta signup çağrılır ve doğrulama ekranı görünür", async () => {
  vi.mocked(fetch).mockResolvedValueOnce(
    new Response(JSON.stringify({ slug: "acme", adminEmail: "kurucu@acme.local" }),
      { status: 201, headers: { "Content-Type": "application/json" } }),
  );
  renderPage();
  await userEvent.type(document.getElementById("company-name")!, "Acme A.Ş.");
  await userEvent.type(document.getElementById("admin-name")!, "Kurucu");
  await userEvent.type(document.getElementById("admin-email")!, "kurucu@acme.local");
  await userEvent.click(screen.getByRole("button", { name: /kaydol/i }));

  expect(vi.mocked(fetch).mock.calls[0][0]).toBe("/api/tenants/signup");
  expect(await screen.findByText(/doğrulama e-postası/i)).toBeInTheDocument();
});

test("çakışan e-postada hata mesajı görünür", async () => {
  vi.mocked(fetch).mockResolvedValueOnce(
    new Response(JSON.stringify({ title: "'kurucu@acme.local' e-postasıyla kayıtlı bir hesap zaten var.", status: 409 }),
      { status: 409 }),
  );
  renderPage();
  await userEvent.type(document.getElementById("company-name")!, "Acme A.Ş.");
  await userEvent.type(document.getElementById("admin-name")!, "Kurucu");
  await userEvent.type(document.getElementById("admin-email")!, "kurucu@acme.local");
  await userEvent.click(screen.getByRole("button", { name: /kaydol/i }));

  expect(await screen.findByText(/zaten var/i)).toBeInTheDocument();
});
```

- [ ] **Step 2: Testi çalıştır, başarısızlığı gör**

Run: `cd frontend && npx vitest run src/auth/CompanySignupPage.test.tsx`
Expected: FAIL (bileşen yok).

- [ ] **Step 3: Sayfayı yaz**

```tsx
// frontend/src/auth/CompanySignupPage.tsx
import { useState, type FormEvent } from "react";
import { useNavigate } from "react-router-dom";
import { apiFetch, ApiError } from "../api/client";

/**
 * Public self-servis kayıt: müşteri şirketini + ilk hr-admin'ini oluşturur.
 * Başarılıysa "doğrulama e-postası gönderildi" ekranı gösterilir; hesap, e-postadaki
 * davet bağlantısı (/accept-invite) ile şifre belirlenince etkinleşir.
 */
export function CompanySignupPage() {
  const navigate = useNavigate();
  const [companyName, setCompanyName] = useState("");
  const [adminName, setAdminName] = useState("");
  const [adminEmail, setAdminEmail] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [doneEmail, setDoneEmail] = useState<string | null>(null);

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    setError(null);
    setBusy(true);
    try {
      await apiFetch("/tenants/signup", {
        method: "POST",
        body: JSON.stringify({ companyName, adminName, adminEmail }),
      });
      setDoneEmail(adminEmail);
    } catch (e) {
      setError(e instanceof ApiError ? e.message : "Kayıt tamamlanamadı. Lütfen tekrar deneyin.");
    } finally {
      setBusy(false);
    }
  };

  return (
    <main className="auth-shell">
      <section className="auth-visual">
        <div className="auth-brand">
          <div className="brand-mark"><i aria-hidden="true" className="fa-solid fa-users-gear" /></div>
          <div>
            <strong>Ekipzen</strong>
            <span>HR MASTER Suite</span>
          </div>
        </div>
        <div className="auth-copy">
          <span className="status-pill info">Şirket kaydı</span>
          <h1>Şirketinizi dakikalar içinde Ekipzen'e taşıyın.</h1>
          <p>Kaydınızı oluşturun; e-postanıza gelen bağlantıyla şifrenizi belirleyip başlayın.</p>
        </div>
      </section>

      <section className="auth-panel">
        {doneEmail ? (
          <div className="auth-form active" role="status">
            <h2>Doğrulama e-postası gönderildi</h2>
            <p>
              <strong>{doneEmail}</strong> adresine bir doğrulama bağlantısı gönderdik.
              Şifrenizi belirleyip hesabınızı etkinleştirmek için e-postanızdaki bağlantıyı açın.
            </p>
            <button type="button" className="btn btn-primary auth-submit" onClick={() => navigate("/login")}>
              <i aria-hidden="true" className="fa-solid fa-arrow-right-to-bracket" /> Girişe dön
            </button>
          </div>
        ) : (
          <form className="auth-form active" onSubmit={submit}>
            <h2>Şirketinizi kaydedin</h2>
            <p>İlk yönetici hesabınızla şirketinizi oluşturun.</p>
            <div className="input-group">
              <label htmlFor="company-name">Şirket adı</label>
              <input id="company-name" className="input-control" value={companyName}
                onChange={(e) => setCompanyName(e.target.value)} />
            </div>
            <div className="input-group">
              <label htmlFor="admin-name">Ad soyad</label>
              <input id="admin-name" className="input-control" value={adminName}
                onChange={(e) => setAdminName(e.target.value)} />
            </div>
            <div className="input-group">
              <label htmlFor="admin-email">İş e-postası</label>
              <input id="admin-email" className="input-control" value={adminEmail}
                onChange={(e) => setAdminEmail(e.target.value)} />
            </div>
            {error && <p className="form-error" role="alert">{error}</p>}
            <button type="submit" className="btn btn-primary auth-submit" disabled={busy}>
              <i aria-hidden="true" className="fa-solid fa-building-user" />{" "}
              {busy ? "Kaydediliyor…" : "Şirketi kaydol"}
            </button>
          </form>
        )}
      </section>
    </main>
  );
}
```

- [ ] **Step 4: Route + login bağlantısı**

`frontend/src/routes.tsx`:
```tsx
import { CompanySignupPage } from "./auth/CompanySignupPage";
// ... buildRouteObjects içinde /signup satırından sonra:
{ path: "/register-company", element: <PublicOnly><CompanySignupPage /></PublicOnly> },
```

`frontend/src/auth/LoginPage.tsx` — login formunun altına (submit button'dan sonra, `</form>` öncesi) küçük bir bağlantı:
```tsx
{mode === "login" && (
  <p className="auth-alt">
    Şirketiniz yok mu?{" "}
    <button type="button" className="auth-link" onClick={() => navigate("/register-company")}>
      Şirketinizi kaydedin
    </button>
  </p>
)}
```

- [ ] **Step 5: Testleri çalıştır, geçtiğini gör**

Run: `cd frontend && npx vitest run src/auth/CompanySignupPage.test.tsx`
Expected: PASS (2 test).

- [ ] **Step 6: Tüm frontend testleri + build**

Run: `cd frontend && npx vitest run && npm run build`
Expected: tüm testler PASS; build hatasız.

- [ ] **Step 7: Commit**

```bash
git add frontend/src/auth/CompanySignupPage.tsx frontend/src/auth/CompanySignupPage.test.tsx frontend/src/routes.tsx frontend/src/auth/LoginPage.tsx
git commit -m "feat(frontend): self-servis şirket kayıt sayfası + login bağlantısı"
```

---

## Task 6: Doğrulama, günlük, kapanış

**Files:**
- Modify: `docs/gelistirme-gunlugu.md`

- [ ] **Step 1: Tam doğrulama**

Run: `cd backend && dotnet test` (tümü yeşil) ve `cd frontend && npx vitest run && npm run build`.

- [ ] **Step 2: Uçtan uca duman (manuel/opsiyonel)**

Backend + frontend çalışırken: `/#/register-company` → şirket kaydı → outbox'ta davet e-postası → `/#/accept-invite?...` ile şifre belirle → otomatik giriş → boş kendi kiracısı.

- [ ] **Step 3: Günlüğü güncelle**

`docs/gelistirme-gunlugu.md` "Şu an neredeyiz" + yeni dated kayıt (self-servis kayıt: pasif kiracı, davet-kabul aktivasyonu, signup rate-limit, frontend sayfa).

- [ ] **Step 4: Kapanış**

Announce: superpowers:finishing-a-development-branch skill'iyle dalı tamamla (testleri doğrula, seçenekleri sun, kullanıcı onayıyla main'e merge + push).

---

## Self-Review Notları

- **Spec kapsamı:** self-servis kayıt (public form + backend), kötüye kullanım önleme (signup rate-limit + e-posta doğrulama kapısı = pasif-until-verified), DRY (ortak onboarding yardımcısı). ✓
- **Enumeration:** 409 çakışması e-posta varlığını sızdırır (provizyonla tutarlı); anti-enumeration (202 generic) ileri sertleştirme olarak bırakıldı — bkz. KVKK doküman Bölüm 7.
- **Suspended kiracı kenar durumu:** `accept-invite` pasif kiracıyı etkinleştirir; askıya alınmış (abonelik bitmiş) bir kiracıda yeni davet kabulü onu yeniden etkinleştirebilir — pratikte askıya alınan kiracı yeni davet üretmez; MVP'de kabul, not düşüldü.
- **Tip tutarlılığı:** `RegisterTenantResult(Slug, AdminEmail)`; `TenantOnboarding.CreateWithAdminAsync(...)` her iki handler'da aynı imzayla. ✓
- **Doğrulanmamış kiracı temizliği (purge):** pasif+doğrulanmamış kiracıların periyodik silinmesi ileri iş (T5.4 Bölüm 7 ile uyumlu).
