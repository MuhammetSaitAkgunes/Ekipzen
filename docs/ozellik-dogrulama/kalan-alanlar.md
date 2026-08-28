# Kalan Alanlar — Özellik Doğrulama

**Tarih:** 2026-08-13 · **Son güncelleme:** 2026-08-28 · **Kapsam:** Departments (4 uç), Compliance (6),
Actions (8), Settings (6), Dashboard (7) · **Mevcut test:** sırasıyla ~4, 5, 5, 4, 7

Bu beş alan tek belgede toplandı: hepsi düşük iş riskli ve bulgu sayısı az.
İki katman da uygulandı — senaryo yürüyüşü gerçek API üzerinden yapıldı.

---

## Özet

| # | Alan | Katman | Önem | Bulgu | Nerede |
| --- | --- | --- | --- | --- | --- |
| **X-1** | Tümü | Kod | **ÇÖZÜLDÜ** (eski YÜKSEK) | Geçersiz enum alan bazlı 400; güvenlik ağı ayrıntı sızdırmayan 400 döndürüyor | `GlobalExceptionHandler`; validator'lar; integration testi |
| D-1 | Departments | Mantık | ORTA | Açık pozisyonu olan departman silinebiliyor; pozisyon **sahipsiz kalıyor** | `DeleteDepartmentCommandHandler` — yalnız personel kontrolü var |
| C-1 | Compliance | Mantık | ORTA | **Son tarih (`DueDate`) hiç kullanılmıyor**; süresi geçen belge kendiliğinden risk olmuyor | `vw_ComplianceReadiness`; durum tamamen elle |
| C-2 | Compliance | Kod | **ÇÖZÜLDÜ** | Durum/seviye komutları ve filtreleri FluentValidation kapsamında | `Compliance/Commands`, `Compliance/Queries` |
| N-1 | Actions | Kod | **ÇÖZÜLDÜ** | Öncelik/durum komutları ve filtreleri FluentValidation kapsamında | `Actions/Commands`, `Actions/Queries` |

**Doğru çalışan, kanıtlanmış davranışlar:**

- **Departments:** personeli olan departman silinemiyor (409, açık mesajla)
- **Settings:** boş şirket adı reddediliyor (400); abonelik platformdan okunuyor
  (Faz 2b/Görev 6 doğrulaması hâlâ geçerli)
- **Dashboard:** tümü `Management` politikası + satır kapsamı; yönetici yalnız
  kendi ekibinin metriklerini görüyor (mevcut testlerle kapsanmış)
- **Actions:** yetki dağılımı ince ayarlı — durum değiştirme `Management`,
  oluşturma/silme `HrAdminOnly`. Yönetici ilerletebiliyor ama silemiyor.
- **Compliance:** okuma `Management` + kapsam, yazma `HrAdminOnly`

---

## X-1 · Geçersiz enum değeri 500 döndürüyor — ÇÖZÜLDÜ 2026-08-28

Canlı yakalandı:

```
PATCH /api/compliance/documents/6/status   {"status": "completed"}
→ 500

System.ArgumentException: Geçersiz belge durumu: completed
  (Eksik|İncelemede|Süresi Yaklaşıyor|Tamamlandı)
  at ComplianceMappings.ParseStatus(String value)
```

**Çözüm:** Actions ve Compliance içindeki tüm durum/öncelik/seviye komut ve
filtreleri alan bazlı FluentValidation kurallarına alındı; Recruitment filtresinde
kalan aynı boşluk da kapatıldı. Savunma katmanı olarak `ArgumentException`
merkezi handler'da iç mesajı istemciye taşımadan 400'e eşleniyor. RFC 7807 hata
zarfı, alan anahtarları ve güvenlik ağı 8 yeni integration testiyle doğrulandı.

**Kök neden:** `GlobalExceptionHandler` `ArgumentException`'ı **haritalamıyor**;
haritalanan tipler `ValidationException`, `NotFoundException`, `ConflictException`,
`ForbiddenAccessException`, `UnauthorizedException`, `TenantInaccessibleException`,
`TenantSchemaOutdatedException`. Haritalanmayan her istisna 500 oluyor.

**Kapsam ölçüldü** — 8 `Parse*` yardımcısı `ArgumentException` fırlatıyor:

| Modül | Alan | Doğrulayıcı var mı? | Sonuç |
| --- | --- | --- | --- |
| Compliance | durum, risk seviyesi | ❌ yok | **500** (canlı doğrulandı) |
| Actions | öncelik, durum | ❌ yok | **500** |
| Attendance | puantaj tipi | ✅ `.Must(...)` | 400 |
| Employees | durum, çalışma şekli | ✅ `.Must(...)` | 400 |
| Recruitment | aday durumu | ✅ `.Must(...)` | 400 |

Yani koruma **modül modül elle** yazılmış; iki modülde yazılmamış ve orada
istemci hatası sunucu hatasına dönüşüyor.

**Neden önemli:** 500, "istemci yanlış gönderdi" değil "sunucu bozuldu" demektir.
Sonuçları: istemci hatayı düzeltmesi gerektiğini anlamaz, hata izleme sistemi
gerçek arızalarla dolar (gürültü içinde gerçek 500'ler kaybolur), ve istisna
mesajı istemciye iç ayrıntı taşır.

**Çözüm iki katmanlı olmalı:** (a) `GlobalExceptionHandler` `ArgumentException`'ı
400'e haritalasın — güvenlik ağı; (b) her enum alanı doğrulayıcıyla korunsun —
asıl çözüm. Yalnız (a) yapılırsa hata mesajları kullanıcıya uygun olmaz; yalnız
(b) yapılırsa bir sonraki unutulan alan yine 500 verir.

---

## D-1 · Açık pozisyonu olan departman silinebiliyor — ORTA

Canlı yürütüldü:

```
1) "Silinecek Departman" oluşturuldu
2) Bu departmana açık pozisyon eklendi   → 201
3) Departman silindi                     → 204   ← engellenmedi
4) Pozisyonun durumu: departmanId = NULL ← sahipsiz kaldı
```

`DeleteDepartmentCommandHandler` **personel** kontrolünü açıkça yapıyor ve
anlamlı bir 409 döndürüyor — doğru refleks. Ama aynı ilişkiye sahip
`Positions.DepartmentId` kontrol edilmiyor; FK `SET NULL` davrandığı için ilan
sessizce departmansız kalıyor.

Sonuç: açık iş ilanı departmansız görünür, aday havuzu raporları bozulur ve
işe alım sırasında departman seçimi kaybolur. Veri kaybı yok ama **sessiz
bozulma** var — bu projede tekrar eden örüntü.

---

## C-1 · Uyum belgelerinde son tarih hiç kullanılmıyor — ORTA

`ComplianceDocument.DueDate` alanı var, ama:

- `vw_ComplianceReadiness` **`DueDate`'e hiç bakmıyor** — yalnız `Status`
  değerlerini sayıyor (canlı tanım okundu)
- "Süresi Yaklaşıyor" bir **durum değeri**, hesaplanan bir şey değil; birinin
  elle işaretlemesi gerekiyor

Yani son tarihi geçmiş bir belge, biri elle durumunu değiştirene kadar
"Tamamlandı" görünmeye devam eder ve denetim hazırlık skoru **yanlış yüksek**
çıkar.

Uyum modülünün varlık sebebi "hangi belgenin süresi doluyor" sorusunu
cevaplamaktır; bugün bu soruyu **kullanıcı sisteme söylüyor**, sistem
kullanıcıya değil.

---

## Katman 2 — kontrol listesi (beş alan ortak)

| # | Kontrol | Sonuç |
| --- | --- | --- |
| 1 | Kiracı izolasyonu | ✅ Tümü global filtre üzerinden; `vw_ComplianceReadiness` `TenantId` taşıyor |
| 2 | Yetki kapsamı | ✅ Politika dağılımı doğru ve ince ayarlı (Actions'ta durum/silme ayrımı iyi örnek) |
| 3 | Doğrulama | ✅ X-1, C-2, N-1 kapandı; enum alanları validator + merkezi güvenlik ağıyla korunuyor |
| 4 | İşlem sınırı | ✅ Tek varlık işlemleri; çok adımlı yazma yok |
| 5 | N+1 / kartezyen | ✅ Dashboard sorguları view/agregasyon üzerinden |
| 6 | Hata zarfı | ✅ Geçersiz istek 400 ProblemDetails; iç `ArgumentException` mesajı sızmıyor |
| 7 | Audit izi | ✅ `ComplianceDocuments` trigger kapsamında; `audit-logs` ucu var |
| 8 | Test boşluğu | ⚠️ Enum regresyonları kapandı; departman-pozisyon ilişkisi (D-1) ve son tarih mantığı (C-1) test edilmemiş |

---

## Sonraki adım

1. **C-1** — son tarih mantığı (ürün kararı: eşik kaç gün?)
2. **D-1** — pozisyon kontrolü, personel kontrolünün yanına
