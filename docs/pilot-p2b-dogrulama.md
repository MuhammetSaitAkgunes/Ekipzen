# Pilot P2-B / 1 — CI, bağımlılıklar ve Turhost ön kontrolü

8 Eylül 2026. Yerel alt paket doğrulandı; GitHub senkronizasyonu için yeni onay bekleniyor. **P2 gerçek ortam kabulü veya pilot açılışı tamamlanmış değildir.** P2-A `09c2b34` ile daha önce senkronize edildi; eski P2-A raporu tarihsel kayıttır, güncel açık maddeler bu belge ve pilot planındadır.

## Tamamlanan işler ve sınırları

- Frontend audit sonucu 7 yüksek + 1 orta paket kaydından **0 bulguya** indirildi. Bu, uygulamanın tüm güvenlik risklerinin bittiği anlamına gelmez. Major/force yükseltmesi yapılmadı; TypeScript peer uyumu için mevcut `--legacy-peer-deps` yaklaşımı korundu.
- Kilitlenen sürümler: `@redocly/openapi-core 1.34.19`, `brace-expansion 2.1.4`, `js-yaml 4.3.1`, `nanoid 3.3.18`, `postcss 8.5.28`, `react-router` ve `react-router-dom 7.18.3`, `undici 7.29.1`. Doğrudan manifestte yalnız router alt sınırı değişti.
- CI frontend işine `npm audit --audit-level=moderate` eklendi; geliştirme bağımlılıkları da kapsanır. Tarama servisinin erişilememesi kontrolü başarılı saydırmaz.
- Entegrasyon test host'unda otomatik şema tarayıcı kaldırıldı, identity retry zamanlayıcısı ayarla kapatıldı. Retry iş mantığı DI'da ve açık `RunOnceAsync` testlerinde kaldı. Yeni regresyon testi bu izolasyonu doğrular. Üretim servisleri, SQL drop/provizyon akışı ve bordro hesabı değiştirilmedi.
- ASP.NET Core becerisinin test-host rehberi doğrultusunda izolasyon test fabrikasında tutuldu; üretim servis davranışı ve mevcut katman sınırları korundu.
- Yayın planı Turhost Windows Profesyonel / SQL Turhost / kökte normal alan adı deneyimi / ayrı DLL API hedefine uyarlandı. Visual Studio publish profili, API alt yol uyarlaması ve SQL yetki ayrımı henüz uygulanmadı; sağlayıcı koşulları önce teyit edilecek. Araştırma ve kopyalanabilir destek soruları: [Turhost ön kontrolü](turhost-pilot-onkontrol.md).

### GitHub CI bulgusu — kesin kanıt ve çıkarım ayrımı

`09c2b34` için [CI koşumu 34197395396](https://github.com/MuhammetSaitAkgunes/Ekipzen/actions/runs/34197395396) başarısızdı: frontend geçti, backend derlendi; 91 birim test geçti, 241 entegrasyon testinden 1'i hata verdi. `Abonelik_KiracininKendiKatalogundaDEGIL_Platformda` testinin `TenancyTestBase.DisposeAsync` temizliğinde SQL, kataloğun kullanımda olduğunu bildirdi.

Testler katalogları tek tek silerken platformda kalan kayıtları zamanlanmış okuyucuların tarayabildiği koddan doğrulandı. Yeni test, düzeltme öncesinde retry zamanlayıcısının açık olduğunu yakalayıp beklenen şekilde başarısız oldu; izolasyondan sonra geçti. **CI logları kataloğu tutan oturumu kesin tanımlamıyor; aynı SQL hatası yerelde yeniden üretilemedi.** Bu paket saptanan eşzamanlı erişim riskini test ortamından kaldırır. GitHub'da yeni başarılı koşum ancak onaylı push sonrasında kanıtlanabilir; yerel başarı eski CI sonucunu değiştirmez.

## Doğrulama kaydı

Komutlar ilgili `backend` veya `frontend` dizininde çalıştırıldı.

| Kontrol | Sonuç |
| --- | --- |
| İzolasyon testinin düzeltme öncesi koşumu | Beklenen 1 başarısızlık: zamanlayıcı açık |
| İzolasyon + AbonelikPlatformda + Recruitment hedefli koşum | 10/10 geçti |
| `dotnet build Ekipzen.sln --no-restore -c Release -warnaserror` | 0 hata, 0 uyarı |
| `dotnet test Ekipzen.sln --no-build -c Release` | 91 birim + 242 entegrasyon; 0 başarısız/atlanan |
| `npm ci --legacy-peer-deps` | Kilit dosyasından temiz kurulum başarılı |
| `npm audit --audit-level=moderate` | 0 bulgu |
| `npm run test -- --run` | 45 dosyada 169/169 geçti |
| `npm run lint`, `npm run build` | Başarılı; TypeScript + üretim Vite derlemesi dahil |
| Üretim JS çıktısında bilinen demo parola/e-posta/rol kontrolü işaretçileri | Eşleşme yok; kapsamlı sır taraması iddiası değil |
| `git diff --check` | Hata yok |
| `git fetch ekipzen`, taban kontrolü | `main` ve `ekipzen/main`: `09c2b3491ea8fd6dd68e87b45ab4b89074a04482` |

Toplam **502 farklı test** geçti: 91 + 242 + 169. Hedefli 10 test bu sayıya ikinci kez eklenmedi. jsdom'un önceki koşumlarda da görülen iki “navigation to another Document” bildirimi sürüyor; bunlar gerçek tarayıcı testi değildir. Yeni NuGet taraması veya IIS/hosting testi yapılmış gibi raporlanmıyor.

## Açık kapılar ve sıradaki işlem

1. Turhost'tan hesaba özel .NET 9 / ANCM / uygulama havuzu, SQL katalog-yetki-isim-TLS, web kökü dışı kalıcı yollar, SMTP ve yedek/geri yükleme cevapları alınmalı. Paket sayfası bu yetkileri kanıtlamaz.
2. Cevaplara göre runtime ile katalog/şema yönetimi ayrımı ve `/api` alt uygulama adresleri karara bağlanmalı. Mevcut controller öneki ile IIS alt uygulamasının birlikte çift `/api/api` yolu doğurması uyarlama gerektirir. Tenant izolasyonu veya güvenli yapılandırma kapıları hosting'e uymak için azaltılmaz.
3. Uygun Visual Studio DLL publish profili ve güncel yayın paketi hazırlanmalı. **Eski `artifacts/p2-verified-20260908` paketi bu bağımlılık yamalarını içermez; güncel P2-B paketi olarak kullanılmamalı.** Bu alt pakette frontend derlendi, yeni Windows teslim paketi oluşturulmadı.
4. GitHub CI başarısı, ardından ayrıca onaylı test yayınıyla gerçek HTTPS/SQL/SMTP/davet/evrak/restart ve tarayıcı kabulü kanıtlanmalı.
5. P3 bordro girdileri ve 40 saatlik çalışma düzeni, P4 geri yükleme, P5 bağımsız bordro/uzman ve kullanıcı kabulü açık. Mevcut localStorage token riski ve runtime destek takvimi dahil önceki açık güvenlik/işletim maddeleri otomatik kapanmadı.

Hosting hesabına müdahale, migration, gerçek müşteri verisi aktarımı, e-posta veya destek talebi gönderimi yapılmadı. Kullanıcı sıradan alan adını açacak; API'nin adresinin gizli olması gibi ek bir şart varsayılmadı.

## Tek onaya sunulan senkronizasyon kapsamı

- Remote yalnız `ekipzen`: `https://github.com/MuhammetSaitAkgunes/Ekipzen.git`.
- Kaynak: `refs/heads/codex/pilot2b-ci-dependency-readiness`.
- Hedef: yerel ve uzak `refs/heads/main`; beklenen taban `09c2b3491ea8fd6dd68e87b45ab4b89074a04482`.
- Commit 1: `42e7cccb51225586d4aabd58de2f081ec38b0de3` — `fix(deps): resolve frontend advisories and enforce audit gate`.
- Commit 2: `8066f17e9de072e1e645b37d780f2a60b8d2fb49` — `test(ci): isolate timed readers from database cleanup`.
- Commit 3: bu belgeyi de içeren `docs(pilot): adapt P2 readiness gates to Turhost`; kesin SHA onay mesajında verilir.

Kaynak ve hedef SHA'ları yeniden doğrulanarak, tek açık onay aşağıdaki işlemleri kapsayabilir:

```text
git push -u ekipzen codex/pilot2b-ci-dependency-readiness:codex/pilot2b-ci-dependency-readiness
git switch main
git merge --ff-only codex/pilot2b-ci-dependency-readiness
git push ekipzen main:main
```

Fast-forward yeni merge commit'i üretmez. Onay başka commit/ref/tag/remote'a genişletilmez; hedef değişmişse durulur. Force-push, geçmişi yeniden yazma, sunucu yayını veya migration dahil değildir.

### Değişen dosyaların tamamı — 9 dosya

1. `.github/workflows/ci.yml`
2. `frontend/package.json`
3. `frontend/package-lock.json`
4. `backend/tests/Ekipzen.Tests.Integration/EkipzenApiFactory.cs`
5. `backend/tests/Ekipzen.Tests.Integration/Hosting/TestHostIsolationTests.cs`
6. `docs/pilot-hazirlik-plani.md`
7. `docs/windows-pilot-yayin.md`
8. `docs/turhost-pilot-onkontrol.md`
9. `docs/pilot-p2b-dogrulama.md`
