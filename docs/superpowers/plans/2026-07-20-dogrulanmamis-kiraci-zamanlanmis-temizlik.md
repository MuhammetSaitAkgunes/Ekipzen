# Doğrulanmamış Kiracı Zamanlanmış Temizlik Implementasyon Planı

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans (inline). Steps use checkbox (`- [ ]`) syntax.

**Goal:** `cleanup-unverified` işleminin dış bir cron sistemine bağımlı olmadan, uygulama-içi bir `BackgroundService` ile periyodik çalışması. Varsayılan **kapalı** (opt-in) — yıkıcı işlem sürprizi olmasın; üretimde yapılandırmayla açılır.

**Architecture:** `UnverifiedTenantCleanupService : BackgroundService`, `IServiceScopeFactory` ile scope açıp `ISender` üzerinden mevcut `CleanupUnverifiedTenantsCommand`'i çağırır. Tek "geçiş" mantığı test edilebilir `RunOnceAsync` metoduna ayrılır; `ExecuteAsync` bunu `PeriodicTimer` ile döngüler. Ayarlar `Cleanup:UnverifiedTenants` bölümünden (`Enabled`, `IntervalHours`, `OlderThanDays`).

**Tech Stack:** .NET 9 Hosting (BackgroundService, PeriodicTimer), Options, MediatR.

## Global Constraints

- Varsayılan `Enabled=false` → mevcut davranış/testler değişmez; hosted service startup'ta hemen çıkar.
- Mevcut `CleanupUnverifiedTenantsCommand`/`ITenantPurger` yeniden kullanılır (yeni silme mantığı yok).
- Commit sonu `Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>`. Dal: `main`'den `feature/scheduled-cleanup`.

## Dosya Yapısı

- **Create:** `backend/src/Ekipzen.API/Services/UnverifiedTenantCleanupOptions.cs`
- **Create:** `backend/src/Ekipzen.API/Services/UnverifiedTenantCleanupService.cs`
- **Modify:** `backend/src/Ekipzen.API/Program.cs` — options bind + `AddHostedService`.
- **Create:** `backend/tests/Ekipzen.Tests.Integration/Tenancy/ScheduledCleanupTests.cs`
- **Modify:** `backend/src/Ekipzen.API/appsettings.json` — örnek (kapalı) blok.
- **Modify:** docs.

## Task 1: Options + Service (TDD)

- [ ] **Step 1:** Failing test `ScheduledCleanupTests`: eski+doğrulanmamış kiracı seed; `UnverifiedTenantCleanupService`'i factory scope-factory + `Options.Create(new {OlderThanDays=7})` ile kur; `RunOnceAsync` çağır; kiracı silinmiş olmalı. Aktif/doğrulanmış kiracı korunmalı.
- [ ] **Step 2:** FAIL (tip yok).
- [ ] **Step 3:** `UnverifiedTenantCleanupOptions` (`Enabled=false`, `IntervalHours=24`, `OlderThanDays=30`) + `UnverifiedTenantCleanupService` (ctor: `IServiceScopeFactory`, `IOptions<...>`, `ILogger<...>`; `RunOnceAsync` scope→`ISender`→`CleanupUnverifiedTenantsCommand`; `ExecuteAsync`: `Enabled` false ise return, değilse `RunOnceAsync` + `PeriodicTimer` döngü).
- [ ] **Step 4:** PASS.
- [ ] **Step 5:** DI: Program.cs'e options bind + `AddHostedService<UnverifiedTenantCleanupService>()`. Tam paket PASS (Enabled=false varsayılan → hiçbir testi etkilemez).
- [ ] **Step 6:** Commit.

## Task 2: Yapılandırma + docs + kapanış

- [ ] appsettings.json örnek blok (`Cleanup:UnverifiedTenants` kapalı, açıklamalı).
- [ ] KVKK doküman + günlük güncelle.
- [ ] Tam doğrulama + commit + kullanıcı onayıyla merge/push.

## Self-Review Notları

- Varsayılan kapalı → prod'da bilinçli açılır; test/dev'de sessiz. ✓
- `RunOnceAsync` ayrık → deterministik test (timer flakiness yok). ✓
- Silme mantığı tekrar edilmedi; komut/purger yeniden kullanıldı (DRY). ✓
