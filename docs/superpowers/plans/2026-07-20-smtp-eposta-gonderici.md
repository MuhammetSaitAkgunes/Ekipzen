# SMTP Gerçek E-posta Göndericisi Implementasyon Planı

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans (inline). Steps use checkbox (`- [ ]`) syntax.

**Goal:** Davet/doğrulama e-postalarının gerçek SMTP üzerinden gönderilebilmesi; geliştirme/test dosya-outbox stub'ında kalır, üretim yapılandırmayla SMTP'ye geçer.

**Architecture:** `IEmailSender` soyutlaması değişmez. Yeni `MailKitSmtpEmailSender` (MailKit) eklenir; DI, `Email:Mode` yapılandırmasına göre seçer (`outbox` varsayılan → mevcut davranış/testler aynen; `smtp` → MailKit). `smtp` modunda eksik Host/From startup'ta fail-fast (JWT sır kontrolüyle aynı ilke).

**Tech Stack:** .NET 9, MailKit (CPM), Options pattern.

## Global Constraints

- Varsayılan davranış DEĞİŞMEZ: yapılandırma yoksa outbox kullanılır; mevcut tüm testler (invite token'ı outbox'tan okuyan dahil) yeşil kalır.
- SMTP şifresi asla commit edilmez; yalnız env/appsettings.Production'dan gelir.
- Commit sonu: `Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>`.
- Dal: `main`'den `feature/smtp-email`.

---

## Dosya Yapısı

- **Modify:** `backend/Directory.Packages.props` — MailKit sürümü.
- **Modify:** `backend/src/Ekipzen.Infrastructure/Ekipzen.Infrastructure.csproj` — MailKit referansı.
- **Create:** `backend/src/Ekipzen.Infrastructure/Email/SmtpOptions.cs` — options + doğrulama.
- **Create:** `backend/src/Ekipzen.Infrastructure/Email/MailKitSmtpEmailSender.cs` — implementasyon.
- **Modify:** `backend/src/Ekipzen.Infrastructure/DependencyInjection.cs` — mode'a göre seçim + fail-fast.
- **Create:** `backend/tests/Ekipzen.Tests.Unit/Email/SmtpOptionsTests.cs` — doğrulama birim testleri.
- **Create:** `backend/tests/Ekipzen.Tests.Integration/Email/EmailSenderSelectionTests.cs` — DI seçim testi.
- **Modify:** `backend/src/Ekipzen.API/appsettings.json` — örnek (boş/outbox) blok.
- **Modify:** `docs/kvkk-veri-izolasyonu.md` Bölüm 7 + `docs/gelistirme-gunlugu.md`.

---

## Task 1: SmtpOptions + doğrulama (TDD)

- [ ] **Step 1: Failing birim test** — `SmtpOptionsTests`: `Validate()` Host eksikse ve From eksikse `InvalidOperationException`; tam yapılandırmada sorunsuz; Port varsayılanı 587, StartTls varsayılanı true.
- [ ] **Step 2:** Testi çalıştır → FAIL.
- [ ] **Step 3:** `SmtpOptions` yaz (`Host`, `Port=587`, `User`, `Password`, `From`, `FromName="Ekipzen"`, `UseStartTls=true`, `Validate()`).
- [ ] **Step 4:** Test PASS. **Step 5:** Commit.

## Task 2: MailKitSmtpEmailSender + DI seçimi

- [ ] **Step 1:** MailKit paketi (Directory.Packages.props + Infrastructure csproj) → `dotnet restore` başarılı.
- [ ] **Step 2:** `MailKitSmtpEmailSender : IEmailSender` — MimeMessage kur, `SmtpClient` ile bağlan (StartTls/SslOnConnect), gerekiyorsa authenticate, gönder, disconnect.
- [ ] **Step 3:** DI: `Email:Mode` oku (`outbox` varsayılan). `smtp` ise `SmtpOptions` bağla + `Validate()` (fail-fast) + `MailKitSmtpEmailSender`; değilse mevcut `FileOutboxEmailSender`.
- [ ] **Step 4:** Failing entegrasyon testi `EmailSenderSelectionTests`: varsayılan factory'de `IEmailSender` → `FileOutboxEmailSender` (mevcut davranış güvencesi). (SMTP moduna gerçek sunucu gerektiğinden DI-seviyesinde yalnız outbox yolu entegrasyonda; smtp seçimi unit-benzeri ServiceCollection testiyle.)
- [ ] **Step 5:** Tüm backend testleri PASS. **Step 6:** Commit.

## Task 3: Yapılandırma örneği + docs + kapanış

- [ ] **Step 1:** `appsettings.json`'a `"Email": { "Mode": "outbox" }` ve yorum niteliğinde Smtp bloğu (şifresiz) ekle.
- [ ] **Step 2:** KVKK doküman Bölüm 7.4 (SMTP) "mevcut" olarak güncelle; günlük kaydı.
- [ ] **Step 3:** Tam doğrulama (backend testleri). **Step 4:** Commit + kullanıcı onayıyla merge/push.

## Self-Review Notları

- Outbox varsayılan kaldığı için test altyapısı (`DAVET-KODU` okuma) hiç etkilenmez. ✓
- Fail-fast yalnız `Email:Mode=smtp` iken; dev ortamı yapılandırmasız çalışmaya devam eder. ✓
- MailKit, System.Net.Mail.SmtpClient'a (obsolete) tercih edildi. ✓
- Kapsam dışı: e-posta şablonları/HTML, kuyruk/retry, bounce yönetimi.
