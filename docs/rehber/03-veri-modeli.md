# 3. Veri Modeli

Veri modelini anlamak, iş kurallarını anlamanın yarısıdır. Bu bölüm "hangi
tablolar var, nasıl ilişkili, neden bazı şeyler view/fonksiyon olarak yazıldı"
sorularını yanıtlar.

## 3.1 Varlıklar (Entities) — konuya göre gruplu

Varlıklar `backend/src/IKPro.Domain/Entities/<Konu>/` altında gruplanır:

| Grup | Ana varlıklar | Rol |
| --- | --- | --- |
| **Organization** | `Employee`, `EmployeeProfile`, `Department`, `EmployeeDocument` | Personel çekirdeği |
| **Leaves** | `LeaveType`, `LeaveRequest`, `LeaveBalance` | İzin |
| **Attendance** | puantaj/timesheet kayıtları | Mesai |
| **Payroll** | `PayrollPeriod`, `PayrollEmployee`, `PayrollResult`, `PayrollSettings`, `IncomeTaxBracket` | Bordro |
| **Recruitment** | `Candidate`, `Position`, `InterviewNote`, `CandidateEvaluation`, `CandidateHistory` | İşe alım |
| **Compliance** | `ComplianceDocument` | Uyum/evrak |
| **Actions** | `GlobalAction`, `AuditLog` | Aksiyon merkezi + denetim |
| **Analytics** | risk metrikleri (çoğu view) | Dashboard |
| **Settings** | `CompanyProfile`, `NotificationSettings`, `SecuritySettings`, `Subscription` | Ayarlar |

Kimlik tarafı ayrıdır: `IKPro.Infrastructure/Identity/ApplicationUser` (login
hesabı) bir `EmployeeId` ile personel kaydına bağlanır. **Bir kullanıcı ≠ bir
personel**: `hr-admin` demo hesabının personel kaydı yoktur (bu yüzden `/leaves`
gibi personel uçlarında 403 alır — bilinçli).

### Önemli ilişkiler

- `Employee` — `Department` (çok-bir): her personel bir departmanda; departmanın
  bir yöneticisi vardır → `manager` kapsamı buradan türer.
- `Employee` 1—1 `EmployeeProfile`: IBAN, acil durum kişisi gibi hassas/opsiyonel alanlar.
- `PayrollPeriod` 1—çok `PayrollEmployee` (dönemdeki her personel satırı) 1—1
  `PayrollResult` (onaylandığında donan hesap **snapshot**'ı).
- `PayrollPeriod` — `PayrollSettings` (yıla göre): dönem, oluştuğu yılın parametre
  setini referanslar.
- `LeaveRequest` — `LeaveType` (düşümlü mü? onay ister mi?) ve `Employee`.
- `Candidate` işe alınınca yeni bir `Employee` + `ApplicationUser` + `LeaveBalance`
  **doğurur** (bkz. [Bölüm 6.4](06-moduller.md#64-işe-alım-adaydan-personele)).

## 3.2 Enum'lar — durum makinelerinin dili

Enum'lar `Domain/Enums/DomainEnums.cs` içinde. Çoğu bir **durum makinesini** temsil
eder; iş kuralları bu durumlar arası geçişleri sınırlar:

| Enum | Değerler | Not |
| --- | --- | --- |
| `EmployeeStatus` | Active, Passive | Toplu pasife alma bunu değiştirir |
| `LeaveStatus` | Pending, Approved, Rejected, Cancelled | Frontend'de küçük harf ("pending"…) |
| `PayrollApprovalStatus` | PreCalc, Control, MissingData, ReadyForApproval, Approved | Satır yaşam döngüsü |
| `PayrollPeriodStatus` | Draft, Control, Approved, Closed | Dönem yaşam döngüsü |
| `CandidateStatus` | New, Interview, Offer, Rejected, Hired | Pipeline; ileri yönlü |
| `ActionStatus` | Open, Week, Done | **İleri yönlü** (geri dönüş 409) |
| `ComplianceStatus` | Missing, InReview, DueSoon, Completed | Evrak durumu |
| `RiskLevel` | High, Medium, Low | Uyum/aksiyon önceliği |

> **Neden enum + eşleme?** Backend içeride güçlü tipli enum kullanır; API sınırında
> frontend'in beklediği metne (`"pending"`, `"Onaylandı"`) çevrilir. Böylece
> veritabanı tutarlı (sayısal/enum), arayüz okunur. Bu eşleme fonksiyonları her
> modülün `...Mappings` sınıfındadır (ör. `LeaveMappings.ToDto`).

## 3.3 SQL View'ları ve fonksiyonları — neden ORM'e her şeyi yaptırmadık?

EF Core çoğu CRUD işini üstlenir. Ama bazı işler saf SQL'de çok daha doğru/hızlıdır.
Bu yüzden bazı **okuma modelleri** (`Domain/ReadModels/`) veritabanındaki bir
**view**'a eşlenir. View'lar migration'larla oluşturulur:

| Read model | SQL nesnesi | Neden view/fonksiyon? |
| --- | --- | --- |
| `LeaveBalanceSummary` | `vw_LeaveBalanceSummary` | Hak ediş + devir − onaylı kullanım tek sorguda |
| `MonthlyAttendanceSummary` | `vw_MonthlyAttendanceSummary` | Aylık gün/fazla mesai toplamı (bordronun girdisi) |
| `PayrollPeriodSummary` | `vw_PayrollPeriodSummary` | Dönem toplamları |
| `ComplianceReadiness` | `vw_ComplianceReadiness` | Denetim hazırlık skoru (sayım + formül) |
| `EmployeeRiskMetric`, `DepartmentRiskSummary` | analitik view/SP | Ağır risk agregasyonu |
| İş günü hesabı | SQL **fonksiyonu** | Hafta sonu + resmi tatil düşerek gün sayısı |

Migration dosyaları: `Persistence/Migrations/*SqlObjects.cs`, `*View.cs`.

> **Neden agregasyon SQL'de?** "Bu personelin bu yıl kalan izni kaç gün?" sorusu,
> tüm izin taleplerini belleğe çekip C#'ta toplamak yerine, veritabanının tek
> sorguda hesaplaması gereken bir iştir. View, bu mantığı **tek doğruluk kaynağı**
> yapar: hem frontend bakiye kartı hem de "izin oluştururken yeterli mi?" kontrolü
> aynı formülü kullanır. (Not: Bölüm 8.7'deki #2 düzeltmesi, bu view'ın yalnız
> *onaylı* izinleri saydığını, *bekleyen* talepleri de gözetmemiz gerektiğini
> ortaya çıkardı — view'ı değiştirmeden uygulama katmanında tamamlandı.)

## 3.4 Migration'lar ve seed (tohum veri)

- **Migration** = veritabanı şemasının versiyonlu değişim kaydı. 9 migration var
  (`Persistence/Migrations/`). Uygulama ilk açılışta bunları uygular.
- **Seed** = demo verinin veritabanına ekilmesi (`Persistence/AppDbContextInitializer.cs`):
  3 kullanıcı + roller, departmanlar, personeller, izin tipleri/bakiyeleri, uyum
  belgeleri, aday havuzu vs. `EntitledDays = 24` (yıllık izin) gibi varsayılanlar
  buradan gelir.

> **Neden seed önemli?** Testler ve duman kontrolleri bu seed'e dayanır. Ör.
> "Ahmet'in 2026 bakiyesi 24 gün" varsayımı seed'den gelir. Seed'i değiştirirsen
> ona bağlı testleri de gözden geçir.

## 3.5 Pratik ipucu: bir alanın nereden geldiğini bulmak

Frontend'de bir değer görüyorsun, kaynağını merak ediyorsun. Zincir:
`schema.d.ts` (tip) → ilgili DTO (`...Dtos.cs`) → Handler'daki `Select(...)` →
Entity veya ReadModel → (view ise) migration SQL'i. Bu zinciri takip ederek her
sayının **tam olarak** nasıl hesaplandığını bulabilirsin.

---

Önceki: [← 2. Mimari](02-mimari.md) · Sonraki: [4. Kimlik & Güvenlik & Hata →](04-kimlik-guvenlik-hata.md)
