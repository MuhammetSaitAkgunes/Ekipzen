# Pilot P1 — teslim ve senkronizasyon kapsamı

Tarih: 8 Eylül 2026. Durum: yerel geliştirme ve doğrulama tamamlandı; GitHub senkronizasyonu kullanıcı onayı bekliyor.

## Teslim

İK yöneticisine özel `GET /api/setup` ve açık `POST /api/setup` işlemi eklendi. Yeni tenant şirket profili, eksik izin türleri, 2026 tam gün tatilleri ve uzman incelemesi bekleyen bordro taslağı ile başlayabilir. Tekrarlanan/eşzamanlı kurulum mevcut kayıtları değiştirmez. Başka tenant kimliği gönderilerek kapsam değiştirilemez.

2026 başlangıç verileri demo initializer'dan ortak bir Application şablonuna taşındı; değerler değiştirilmedi. Demo dışında otomatik bordro onayı verilmez. İlk müşteri kurulumunda örnek çalışan/departman, risk verisi veya abonelik eklenmez.

Bordro ayarları onaylı set yokken mevcut taslağı açar; tamamen boş şirkette kurulum panelini gösterir. Teknik hatalar sonsuz yükleniyor görünümünde kalmaz. Kaydedilmemiş alan/vergi dilimi değişikliklerinde eski taslağın onayı kapatılır; işlem sürerken form kilitlenir. İlk ileri tarihli setin onayı sonrası ekranın yükleniyor durumuna takılması da test edildi.

ASP.NET Core rehberi doğrultusunda mevcut controller/MediatR/EF Core katmanları korundu: yetki API sınırında, başlangıç iş kuralı Application'da, transaction ve tenant kilidi mevcut Infrastructure hizmetinde. Yeni bağımlılık veya migration eklenmedi. Frontend sözleşmesi Swagger'dan üretildi.

Pilot profili: yaklaşık 10 personel, Windows / SQL Server; Pazartesi–Cuma 09.00–18.00, 1 saat yemek molası, net 40 saat/hafta. Çalışma profili bu pakette yalnız plana işlendi; hesap kuralları P3'te ayrıca doğrulanacak.

## Doğrulama kanıtı

| Kontrol | Sonuç |
| --- | --- |
| `dotnet build backend/IKPro.sln --no-restore -v minimal` | Başarılı; 0 hata, 0 uyarı |
| `dotnet test backend/IKPro.sln --no-restore -v minimal` | 91 birim + 207 entegrasyon; 0 başarısız, 0 atlanan |
| `npm run test -- --run` (frontend) | 45 dosyada 165 test; tamamı geçti |
| `npm run lint` (frontend) | Başarılı |
| `npm run build` (frontend) | TypeScript + Vite üretim derlemesi başarılı |
| `npm run gen:api` (frontend) | Yerel Swagger sözleşmesinden şema üretildi |
| `git diff --check` | Hata yok |
| `git fetch ikpro --prune` ve `main...ikpro/main` | Kontrol sırasında 0 ileri / 0 geri |

Toplam **463 başarılı test**, önceki 447 teste göre **16 yeni test**. Yeni entegrasyon senaryosu gerçek provizyon/davet kabulü → boş şirket → açık kurulum → uzman notu olmadan onayın reddi → test amaçlı notla taslak kaydı/onayı → API'den 10 personel → ilk taslak bordro akışını kapsar. Bu not gerçek uzman onayı değildir; test yalnız test kataloglarında koşar. Başlangıçta veri olmaması, özel kayıtların korunması, tekrar/eşzamanlı kurulum, tenant izolasyonu ve rol/kimlik koruması ayrıca doğrulandı.

İlk hedefli koşumda bir testin JSON enum okuma ayarı ve bir frontend testinin sorgu tekrar bekleme süresi düzeltildi. Yukarıdaki tam koşum bu düzeltmelerden sonra geçti. Frontend testlerinde jsdom'un iki “navigation to another Document” bildirimi var; test başarısızlığı değil ve gerçek tarayıcı kabulünün yerine geçmiyor.

## Sınırlar ve takip

- Bu paket **pilot açılış onayı değildir**. 2026 yarım gün tatilleri, haftalık çalışma/izin politikası, doğru geçmiş matrahlar, bağımsız hesap mutabakatı ve geri yükleme kabulü açık kapılardır.
- Yeni API şemasını okurken yerel `demo#1` ve `globex#2` veritabanlarının mevcut kod şemasının gerisinde olduğu görüldü. Otomatik migration yapılmadı. Yerel demo güncellemesi, yedek ve hedef kontrolüyle ayrıca onaylanmalı; aşağıdaki Git paketine dahil değildir.
- API şema üretiminden sonra durduruldu. Müşterinin Windows sunucusuna yayın, gerçek personel aktarımı, üretim e-postası veya veritabanı müdahalesi yapılmadı.
- Gerçek tarayıcı kabulü ve GitHub CI sonucu bu paketin yerel test sonuçlarıyla eşdeğer sayılmadı. P2/P5'te ayrıca doğrulanacak.

## Onaya sunulan Git kapsamı

- Uzak depo: yalnız `ikpro` — `https://github.com/MuhammetSaitAkgunes/IKPro.git`.
- Kaynak: `refs/heads/codex/pilot1-customer-bootstrap`.
- Hedef: `refs/heads/main`; uzak hedef `refs/heads/main` (`ikpro/main`).
- Başlangıç/hedef mevcut tabanı: `309c4dee57bb1c68b99b51a8ea74276e44ec879a`.
- Commitler: `docs(pilot): define accounting-office readiness gates` ve `feat(setup): bootstrap pilot tenants with reviewed payroll drafts`. Kesin iki commit kimliği son onay mesajında bildirilir; onay yalnız bu iki commit içindir.

Onaylanan kaynak SHA ve hedef tabanı tekrar doğrulandıktan sonra uygulanabilecek işlemler:

```text
git push -u ikpro codex/pilot1-customer-bootstrap:codex/pilot1-customer-bootstrap
git switch main
git merge --ff-only codex/pilot1-customer-bootstrap
git push ikpro main:main
```

Fast-forward merge ek merge commit'i üretmez. Hedef değişmişse bu onay yeni commit/merge kapsamına genişletilmez; tekrar değerlendirilir. Başka branch, tag, remote, force-push, history rewrite, deployment veya migration yetkisi içermez.

### Değişen dosyaların tamamı — 16 dosya

1. `raporlar/pilot-hazirlik-analizi-2026-09-07.md`
2. `docs/pilot-hazirlik-plani.md`
3. `docs/pilot-p1-dogrulama.md`
4. `backend/src/IKPro.API/Controllers/SetupController.cs`
5. `backend/src/IKPro.Application/Features/Setup/TenantBaseline2026.cs`
6. `backend/src/IKPro.Application/Features/Setup/TenantSetupHandlers.cs`
7. `backend/src/IKPro.Infrastructure/Persistence/AppDbContextInitializer.cs`
8. `backend/tests/IKPro.Tests.Integration/Tenancy/TenantSetupTests.cs`
9. `frontend/src/api/schema.d.ts`
10. `frontend/src/features/payroll/queries.ts`
11. `frontend/src/features/payroll/SettingsTab.tsx`
12. `frontend/src/features/payroll/SettingsTab.test.tsx`
13. `frontend/src/features/settings/SettingsPage.tsx`
14. `frontend/src/features/settings/SettingsPage.test.tsx`
15. `frontend/src/features/settings/TenantSetupPanel.tsx`
16. `frontend/src/features/settings/TenantSetupPanel.test.tsx`

Sıradaki geliştirme paketi: P2 — Windows üretim yayını ve güvenli yapılandırma. Kod ve yerel testlerin geçmesi, müşteri ortamındaki bu kontrolleri kendiliğinden kapatmaz.
