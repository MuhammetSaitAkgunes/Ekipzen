# Kalan Alanlar — Özellik Doğrulama

**Tarih:** 2026-08-13 · **Kapsam:** Departments (4 uç), Compliance (6),
Actions (8), Settings (6), Dashboard (7) · **Mevcut test:** sırasıyla ~4, 5, 5, 4, 7

Bu beş alan tek belgede toplandı: hepsi düşük iş riskli ve bulgu sayısı az.
İki katman da uygulandı — senaryo yürüyüşü gerçek API üzerinden yapıldı.

---

## Özet

| # | Alan | Katman | Önem | Bulgu | Nerede |
| --- | --- | --- | --- | --- | --- |
| **X-1** | Tümü | Kod | **YÜKSEK** | **Geçersiz enum değeri 500 döndürüyor** — `ArgumentException` haritalanmamış | `GlobalExceptionHandler`; 8 `Parse*` yardımcısı |
| D-1 | Departments | Mantık | ORTA | Açık pozisyonu olan departman silinebiliyor; pozisyon **sahipsiz kalıyor** | `DeleteDepartmentCommandHandler` — yalnız personel kontrolü var |
| C-1 | Compliance | Mantık | ORTA | **Son tarih (`DueDate`) hiç kullanılmıyor**; süresi geçen belge kendiliğinden risk olmuyor | `vw_ComplianceReadiness`; durum tamamen elle |
| C-2 | Compliance | Kod | ORTA | Durum/seviye alanlarında **hiç doğrulayıcı yok** → X-1'in canlı örneği | `Compliance/Commands` — `.Must(` yok |
| N-1 | Actions | Kod | ORTA | Öncelik/durum alanlarında **hiç doğrulayıcı yok** → X-1 | `Actions/…` — `.Must(` yok |

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

## X-1 · Geçersiz enum değeri 500 döndürüyor — YÜKSEK (sistemik)

Canlı yakalandı:

```
PATCH /api/compliance/documents/6/status   {"status": "completed"}
→ 500

System.ArgumentException: Geçersiz belge durumu: completed
  (Eksik|İncelemede|Süresi Yaklaşıyor|Tamamlandı)
  at ComplianceMappings.ParseStatus(String value)
```

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
| 3 | Doğrulama | ❌ X-1, C-2, N-1 |
| 4 | İşlem sınırı | ✅ Tek varlık işlemleri; çok adımlı yazma yok |
| 5 | N+1 / kartezyen | ✅ Dashboard sorguları view/agregasyon üzerinden |
| 6 | Hata zarfı | ❌ X-1 — haritalanmamış istisna 500'e düşüyor |
| 7 | Audit izi | ✅ `ComplianceDocuments` trigger kapsamında; `audit-logs` ucu var |
| 8 | Test boşluğu | ⚠️ Geçersiz enum girdisi (X-1) hiçbir alanda test edilmemiş; departman-pozisyon ilişkisi (D-1) ve son tarih mantığı (C-1) test edilmemiş |

---

## Sonraki adım

1. **X-1** — iki katmanlı düzeltme; tek bir yerde (`GlobalExceptionHandler`)
   yapılacak değişiklik 8 noktayı birden güvenceye alır
2. **C-1** — son tarih mantığı (ürün kararı: eşik kaç gün?)
3. **D-1** — pozisyon kontrolü, personel kontrolünün yanına
