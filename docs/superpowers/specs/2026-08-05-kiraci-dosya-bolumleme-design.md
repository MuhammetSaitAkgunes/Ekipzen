# Kiracı Bazlı Dosya Bölümlemesi — Tasarım

**Tarih:** 2026-08-05
**Durum:** Onaylandı, uygulamaya hazır

## Problem

Yüklenen dosyalar kiracıya göre bölümlenmiyor. Bu üç somut soruna yol açıyor:

1. **Kiracı bazlı yedek alınamıyor.** Bir müşterinin dosyalarını ayrı yedekleyip
   ayrı geri yüklemek mümkün değil; KVKK gereği yedekten seçici imha da öyle.
2. **Purge eksik.** `TenantPurger` yalnız `EmployeeDocuments.FilePath` listesini
   siliyor ([TenantPurger.cs:25-28](../../../backend/src/Ekipzen.Infrastructure/Persistence/TenantPurger.cs));
   çalışan **fotoğrafları** ve **şirket logosu** kiracı silindikten sonra diskte
   kalıyor. Kişisel veri, "sil" işleminden sağ çıkıyor.
3. **İleride ayrı veritabanına bölme zorlaşıyor.** "Şu kiracının tüm dosyaları"
   sorusuna ancak veritabanına sorarak cevap verilebiliyor.

Mevcut yerleşim:

```
storage/
  documents-emp-14/   ← evrak (çalışan ID'siyle; kiracı yok)
  photos/             ← TÜM kiracıların fotoğrafları düz klasörde
  outbox/             ← e-posta stub'ları (davet token'ları)
```

## Kapsam

**Dahil:** özlük evrakı, çalışan fotoğrafı, şirket logosu — bölümleme, purge
kapsamı, migrasyon ve kiracı başına yedek.

**Hariç (bilinçli):** `outbox/` bölümlenmez. Bu bir geliştirme stub'ıdır
(`Email:Mode=outbox`); üretimde SMTP kullanılır ve dosya hiç oluşmaz. Ayrı bir
iş olarak ele alınmalı — davet token'ı içerdiği için kişisel veridir.

## Yol şeması

```
storage/
  tenant-1/
    documents-emp-14/{guid}.pdf
    photos/{guid}.png
    branding/{guid}.png     ← şirket logosu (kategori adı kodda "branding")
  tenant-2/…
  outbox/
```

**Veritabanındaki yol değişmez.** Bugün saklanan değer zaten kiracıdan bağımsız
göreli yoldur (`documents-emp-14/{guid}.pdf`); ön eki yalnızca depo katmanı
uygular. Bu üç fayda sağlar:

- Migrasyon yalnızca dosya taşımadır; **hiçbir DB satırı güncellenmez**.
- Kiracılar arası yol kaçışı **yapısal olarak imkânsız** olur: kayıttaki yol
  kurcalansa bile ön ek her zaman *aktif* kiracıdan yeniden uygulanır.
- İleride ayrı DB'ye bölünürse yollar olduğu gibi taşınır.

## Mimari

`LocalFileStorage` **scoped** olur (bugün singleton) ve `ICurrentTenant` alır.
Her işlemde etkin kök `_root/tenant-{id}/` olarak çözülür; çağıran taraf
kiracıyı hiç bilmez.

Singleton→scoped geçişi güvenlidir: sınıf durumsuzdur, yalnız `_root` tutar.
Kiracıyı singleton'a enjekte etmek "captive dependency" hatası olurdu (ilk
isteğin kiracısı sonsuza kadar yapışırdı) — scoped'a geçmek bunu önler.

### Neden çağrı yerinde değil

Alternatif, her handler'ın kategoriyi `tenant-{id}/…` diye kurmasıydı. Reddedildi:
yeni bir yükleme ucu eklendiğinde ön eki koymayı unutmak mümkün olurdu — bu tam
olarak veritabanı filtresinde reflection'la yok edilen hata türüdür. Depo
katmanının uygulaması "güvenli olanı otomatik yap" ilkesini sürdürür.

### Kiracı bağlamı yoksa

`TenantId` null ise işlem **reddedilir** (`InvalidOperationException`). Sessizce
paylaşılan köke yazmak, ilerideki bir sızıntının tohumudur.

Mevcut çağrı yerlerinin tamamı güvenlidir: altısı da ya HTTP isteği içindedir
(kiracı claim'den gelir) ya da `TenantPurger` gibi `Impersonate` eder.
`FileOutboxEmailSender` diske doğrudan yazar, `IFileStorage` kullanmaz — bu
değişiklikten etkilenmez.

## Purge — bölümlemenin asıl armağanı

Bugünkü purge, DB'den topladığı evrak yollarını tek tek siler; fotoğraf ve logo
bu listede olmadığı için kaçar.

Bölümlemeden sonra doğru çözüm daha basit: **`tenant-{id}/` dizinini komple sil.**
Bu, evrak/fotoğraf/logo ve *ileride eklenecek her dosya türünü* otomatik kapsar.
`IFileStorage`'a tek bir yöntem eklenir:

```csharp
/// <summary>Kiracının tüm dosya alanını siler (purge). Dizin yoksa sessizce geçer.</summary>
Task DeleteTenantSpaceAsync(int tenantId, CancellationToken cancellationToken);
```

Bu yöntem kiracıyı **açıkça parametre alır**, çünkü purge sırasında silinen
kiracı ile aktif bağlam ayrışabilir; örtük çözümleme burada tehlikelidir.

## Migrasyon

Tek seferlik script (`scripts/migrate-files-to-tenant-layout.ps1`):

1. `storage/documents-emp-*` ve `storage/photos`, `storage/logos` altındaki her
   dosya için sahibi veritabanından bulunur (evrak → çalışan → `TenantId`).
2. Dosya `tenant-{id}/…` altına **taşınır**; göreli alt yol korunur.
3. Sahibi çözülemeyen dosya **taşınmaz**, açıkça loglanır — sessizce silinmez.
4. DB satırlarına dokunulmaz.

Bugün 13 dosya var, hepsi geliştirme verisi; üretim müşterisi yok. Script yine de
yazılır: aynı ihtiyaç ileride başka bir kurulumda çıkabilir ve elle taşımak
hataya açıktır.

## Yedekleme

`scripts/backup-restore-drill.ps1` bugün evrak dizinini tek zip yapıyor.
`tenant-*` dizinlerini gezip **kiracı başına bir zip** üretecek şekilde değişir:

```
{BackupPath}/{Database}-tenant-1-{damga}.zip
{BackupPath}/{Database}-tenant-2-{damga}.zip
```

Kiracı listesi klasör adlarından gelir; veritabanına sorulmaz (yedek script'inin
DB şemasına bağımlı olmaması iyidir). Off-site kopyalama ve doğrulama mevcut
mantıkla aynı şekilde her arşive uygulanır.

Kiracı klasörü yoksa (henüz dosya yüklememiş kiracı) arşiv üretilmez; bu bir
hata değildir.

## Test

**Birim (`LocalFileStorageTests` genişletilir):**
- Kaydedilen dosya `tenant-{id}/` altına yazılır.
- Kiracı bağlamı yokken kaydetme reddedilir.
- Başka kiracının klasörüne çıkmaya çalışan göreli yol reddedilir.
- `DeleteTenantSpaceAsync` evrak + fotoğraf + logoyu birlikte siler; başka
  kiracının alanına dokunmaz.

**Entegrasyon:**
- Kiracı A'nın yüklediği evrakı kiracı B indiremez (mevcut
  `TenantFileIsolationTests` genişletilir).
- Purge sonrası kiracının fotoğraf ve logo dosyaları da diskte kalmaz.

## Riskler

| Risk | Azaltma |
| --- | --- |
| Scoped'a geçiş bir çağrı yerini bozar | Altı çağrı yerinin tamamı incelendi; hepsi HTTP bağlamında ya da impersonate ediyor |
| Migrasyon dosya kaybettirir | Taşıma yapılır (kopyala-sil değil); sahibi bulunamayan dosya yerinde bırakılır ve loglanır |
| Kiracı klasör adı çakışır | Ad `tenant-{int id}` — kullanıcı girdisi değil, çakışma imkânsız |
| Yedek script'i kiracı sayısıyla yavaşlar | Kabul edilir; dosya hacimleri KOBİ ölçeğinde küçük |
