# Pilot P2-A — yerel üretim hazırlığı ve senkronizasyon kapsamı

Tarih: 8 Eylül 2026. Durum: yerel yazılım/paket doğrulaması tamamlandı; GitHub senkronizasyonu onay bekliyor. **P2 bütünü veya müşteri pilotu henüz tamamlanmadı.**

## Teslim edilen sınır

- Production açılışı; demo seed, örnek/zayıf sır, geniş host/CORS, yanlış davet origin'i, güvensiz SQL bağlantısı, eksik SMTP ve geçici veri yollarıyla çalışmayı reddeder. Ayar hatalarında sır değerleri yazılmaz.
- IIS in-process/x64 hedefi; aynı origin React + API, HTTPS yönlendirmesi, HSTS, CSP ve API `no-store` başlıkları. Bilinmeyen host reddedilir; keyfî forwarded başlıkları güvenilir kabul edilmez.
- Kalıcı evrak ve anahtar dizinleri yayın dizininden ayrıdır. Data Protection anahtarları özel anahtarlı RSA PFX ile şifrelenir; release dizini değişse de çözülebilir. Loglar dış dizinde günlük/10 MB eşiğiyle döner; 14 dosya tutulur.
- Harici `EKIPZEN_CONFIG_FILE` yapılandırması ve bağlantı kurmayan `--verify-production=true` kontrolü. Hatalı açılış artık sıfır olmayan çıkış kodu verir.
- Üretimde en az 12 karakter/büyük harf/rakam şifre politikası. Demo ön doldurma ve demo rol değiştirme yalnız geliştirmede kalır. Sabit sahte giriş skorları kaldırıldı.
- İşlevsiz SMS 2FA ve haftalık rapor seçenekleri UI'da kullanılamıyor olarak gösterilir, API etkinleştirme girişimini reddeder. Eski `true` kaydı etkin özellik gibi gösterilmez.
- Yeni, mevcut paketi ezmeyen Windows paket betiği; dosya SHA-256 manifesti; sır içermeyen örnek yapılandırma; salt okunur sunucu envanteri ve işletim kılavuzu. CI Node 24 ile üretim build'i de çalıştıracak şekilde güncellendi.

ASP.NET Core rehberi doğrultusunda açılış, yapılandırma, Data Protection ve HTTP güvenliği API hosting sınırında tutuldu; mevcut Application/Domain/Infrastructure ayrımı korundu. Yeni NuGet/npm paketi, veritabanı migration'ı veya bordro hesap değişikliği eklenmedi.

## Doğrulama kanıtı

| Kontrol | Sonuç |
| --- | --- |
| `dotnet test Ekipzen.sln --no-restore -c Release` (`backend`) | 91 birim + 240 entegrasyon; 0 başarısız/atlanan |
| Son host reddi testi eklendikten sonra `dotnet build Ekipzen.sln --no-restore -c Release -warnaserror` | 0 hata, 0 uyarı |
| `dotnet test tests/Ekipzen.Tests.Integration/Ekipzen.Tests.Integration.csproj --no-build -c Release --filter FullyQualifiedName~ProductionHostingTests` | 34/34; tam koşumdaki 33 test tekrarlandı, son 1 test ayrıca geçti |
| `npm run test -- --run` (`frontend`) | 45 dosyada 169/169 |
| `npm run lint` ve `npm run build` | Başarılı |
| İki Windows betiğinin PowerShell AST denetimi | Sözdizimi hatası yok |
| Temiz kaynak commit'inden `build-windows-pilot.ps1 -ReleaseName p2-verified-20260908` | Başarılı; `dirtyWorkingTree=false` |
| Yayın kontrolü | 152 site dosyasının SHA-256 değeri doğrulandı; in-process; iki environment adı Production; ANCM stdout kapalı |
| Paket kapıları | Development config/App_Data ve frontend demo kimlik/parola işaretçisi yok; taban SQL/JWT boş |
| Yayın DLL ön kontrolü | Eksik ayarla exit 1; dış test JSON'u/geçici test PFX'i ile exit 0; SQL/SMTP/IIS bağlantısı veya migration yok |
| Aynı release adıyla ikinci paket denemesi | Reddedildi; önceki manifest hash'i değişmedi |
| `git diff --check` | Hata yok |
| `git fetch ekipzen`, `main...ekipzen/main` | Kontrol sırasında 0 ileri / 0 geri; ikisi de `518ab53` |

Toplam **501 farklı test senaryosu doğrulandı**: 91 birim + 241 entegrasyon + 169 frontend. Bu, 501 testin tek komutta koştuğu anlamına gelmez; yukarıdaki tam koşum ve son hedefli koşum birlikte bu sayıyı verir. P1'deki 463 senaryoya göre 38 yeni senaryo var. jsdom'un iki “navigation to another Document” bildirimi önceki koşumdaki gibi sürüyor; test başarısızlığı değil, gerçek tarayıcı kanıtı da değil.

İlk yerel paket denemesi AppShell'de kalan demo rol girişini yakalayıp durdu. Giriş yolu geliştirmeyle sınırlandı; üretim yokluğu test edildi; yukarıdaki temiz paket bu düzeltmeden sonra üretildi. Önceki başarısız prova klasörü ezilmedi ve teslim paketi olarak kullanılmamalı.

### Yerel paket kimliği

- Dizin: `artifacts/p2-verified-20260908` (Git dışı, henüz sunucuya gönderilmedi).
- Kaynak commit: `e70e46e886670456dcb89bde3d2cfd1804149085`.
- Manifest SHA-256: `38893FB65DE4128BA7D076013C87CBC2E09AF02BFCE9BEB2339DDA4AD9FFC106`.
- Bu doğrulama belgesi paket üretiminden sonraki ayrı dokümantasyon commit'idir; paket kodunu değiştirmez.
- Paket kimliği bir kriptografik imza, güvenlik sertifikası veya pilot kabulü değildir.

## Açık kapılar ve sonraki karar

1. **P2-B / gerçek ortam:** Windows/SQL sürümleri henüz bilinmiyor. Sistem yöneticisi `scripts/get-windows-server-inventory.ps1` çıktısını sağlamalı. Bu bilgisayarın envanteri müşteri sunucusu yerine kullanılmadı.
2. **SQL en az yetki:** Mevcut provizyon/migration günlük uygulama bağlantısıyla DDL kullanıyor. `sa` engeli tek başına yeterli değil. Runtime ve kurulum/şema yönetimi yetkilerini ayırma, platform yönetim uçlarının ağ sınırı ve kontrollü uygulama planı ürün sahibiyle karara bağlanmalı. Bu pakette sessizce değiştirilmedi; kalıcı `sysadmin` ile geçiş önerilmez.
3. **Bağımlılıklar:** Paket üretimindeki npm kontrolü 7 yüksek + 1 orta olmak üzere 8 paket kaydı bildirdi; önceki analizdeki kayıtlar çözülmedi. Paket sayısı doğrulanmış sömürü sayısı değildir. Önceki rapordaki kullanım-yolu değerlendirmesi korunur; körlemesine `audit fix --force` yapılmadı. Kontrollü yama ve yeniden audit P2-B işidir. Yeni bir NuGet güvenlik taraması yapılmış gibi raporlanmıyor.
4. **Gerçek kabul:** IIS/TLS/SQL/SMTP bağlantısı, davet teslimi, gerçek tarayıcı/CSP, dosya ve restart provası, ACL ve log içeriği/retention, başarılı GitHub CI koşumu henüz kanıtlanmadı. Yerel test host'u IIS değildir.
5. **İşletim tasarımı:** localStorage token saklama riski devam ediyor; CSP HttpOnly tasarımı değildir. PFX/anahtar yedeği, sertifika yenileme ve alarm, .NET destek takvimine uygun yükseltme ayrıca tamamlanmalı. Ayrıntılar ve resmî kaynaklar: `docs/windows-pilot-yayin.md`.
6. **Hesap/kabul:** 40 saatlik haftanın mevzuata uygun hesap girdileri, yarım gün tatilleri, açılış bakiyeleri, bağımsız mali müşavir mutabakatı ve geri yükleme kabulü P3/P4/P5'te açık. Bu pakette bordro doğruluğu veya satışa hazırlık için yeni uygunluk iddiası yok.

Önerilen sonraki karar: sunucu envanteri toplanırken SQL runtime/provizyon yetki ayrımı ve kontrollü bağımlılık yamalarını ayrı P2-B geliştirme paketi olarak planlamak. Gerçek müşteri verisi, sunucu yayını, migration ve e-posta gönderimi yapılmadı.

## Onaya sunulan Git kapsamı

- Remote: yalnız `ekipzen` — `https://github.com/MuhammetSaitAkgunes/Ekipzen.git`.
- Kaynak ref: `refs/heads/codex/pilot2-production-hosting`.
- Hedef ref: yerel ve uzak `refs/heads/main` (`ekipzen/main`).
- Hedef mevcut tabanı: `518ab5368c62379872d102a29ba5942d48fb6236`.
- Commit 1: `f9504d6be5ceaef90457be802114a991aae20e32` — `feat(hosting): enforce production startup and remove demo controls`.
- Commit 2: `e70e46e886670456dcb89bde3d2cfd1804149085` — `ops(pilot): add Windows package and deployment preflight guide`.
- Commit 3: bu belgenin `docs(pilot): record P2 production preparation evidence` commit'i; kesin SHA son onay mesajında verilir.

Kaynak SHA ve hedef tabanı tekrar doğrulandıktan sonra, tek açık kullanıcı onayı aşağıdaki işlemleri kapsayabilir:

```text
git push -u ekipzen codex/pilot2-production-hosting:codex/pilot2-production-hosting
git switch main
git merge --ff-only codex/pilot2-production-hosting
git push ekipzen main:main
```

Fast-forward ek merge commit'i üretmez. Hedef değişmişse onay yeni commitlere genişletilmez. Başka branch/tag/remote, force-push, history rewrite, sunucu yayını veya migration bu kapsama dahil değildir.

### Değişen dosyaların tamamı — 26 dosya

1. `.github/workflows/ci.yml`
2. `.gitignore`
3. `backend/src/Ekipzen.API/Hosting/PilotHosting.cs`
4. `backend/src/Ekipzen.API/Hosting/ProductionConfiguration.cs`
5. `backend/src/Ekipzen.API/Ekipzen.API.csproj`
6. `backend/src/Ekipzen.API/Program.cs`
7. `backend/src/Ekipzen.API/appsettings.Development.json`
8. `backend/src/Ekipzen.API/appsettings.Production.json`
9. `backend/src/Ekipzen.API/appsettings.json`
10. `backend/src/Ekipzen.Application/Features/Settings/SettingsDtos.cs`
11. `backend/src/Ekipzen.Application/Features/Settings/SettingsHandlers.cs`
12. `backend/tests/Ekipzen.Tests.Integration/Hosting/ProductionHostingTests.cs`
13. `backend/tests/Ekipzen.Tests.Integration/Settings/SettingsTests.cs`
14. `deployment/windows/configuration.example.json`
15. `docs/pilot-hazirlik-plani.md`
16. `docs/pilot-p2-dogrulama.md`
17. `docs/windows-pilot-yayin.md`
18. `frontend/src/auth/AcceptInvitePage.tsx`
19. `frontend/src/auth/LoginPage.test.tsx`
20. `frontend/src/auth/LoginPage.tsx`
21. `frontend/src/features/settings/SettingsPage.test.tsx`
22. `frontend/src/features/settings/SettingsPage.tsx`
23. `frontend/src/layout/AppShell.test.tsx`
24. `frontend/src/layout/AppShell.tsx`
25. `scripts/build-windows-pilot.ps1`
26. `scripts/get-windows-server-inventory.ps1`
