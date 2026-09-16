# Ekipzen — Backend API Sözleşmesi

> Faz 12 teslim dokümanı · Base URL: `https://localhost:7001/api` · Auth: JWT Bearer
> Roller: **A** = hr-admin, **M** = manager, **E** = employee (routes.js matrisiyle birebir)
> Hatalar: RFC 7807 ProblemDetails — 400 validasyon, 401 kimlik, 403 yetki, 404 kayıt yok, 409 iş kuralı

## Kimlik & Oturum (`/auth`, `/me`)

| Uç | Rol | Açıklama |
|---|---|---|
| `POST /auth/login` | herkese açık | JWT + refresh token döner (lockout: 5 deneme/5 dk) |
| ~~`POST /auth/register`~~ | — | **KALDIRILDI** — kiracı sızıntısı (her kaydı en düşük Id'li kiracıya bağlıyordu). Katılımın tek yolu davettir. |
| `POST /auth/refresh` | herkese açık | Refresh token rotasyonu |
| `POST /auth/logout` | A M E | Refresh token iptali |
| `POST /auth/change-password` | A M E | Şifre değişikliği (ayarlar güvenlik sekmesi) |
| `GET /me` | A M E | Oturum sahibi profil + rol + bağlı personel |

## Personel & Departman (`/employees`, `/departments`)

| Uç | Rol | Açıklama |
|---|---|---|
| `GET /employees` | A M | Directory: arama/filtre/sayfalama; M yalnız ekibini görür |
| `GET /employees/{id}` | A M | Tam kart (TC yalnız A'ya açık; listede maskeli) |
| `POST /employees` · `PUT /employees/{id}` | A | Kart oluştur/güncelle (profil grupları) |
| `POST /employees/bulk-deactivate` · `PATCH /employees/{id}/status` | A | Toplu/tekil pasifleştirme |
| `POST /employees/{id}/photo` | A | Foto (JPG/PNG, 2 MB) |
| `GET/POST /employees/{id}/documents` · `GET .../documents/{documentId}` | A (yazma), A M (okuma) | Özlük evrakı |
| `GET /departments` | A M | Liste |
| `POST/PUT/DELETE /departments...` | A | CRUD |

## İzin & Onay (`/leaves`)

| Uç | Rol | Açıklama |
|---|---|---|
| `GET /leaves/types` · `GET /leaves/balance` · `GET /leaves/my` | A M E | Katalog, bakiye (SQL view), taleplerim |
| `POST /leaves` | A M E | Talep; iş-günü SQL fonksiyonuyla hesaplanır; çakışma/bakiye 409 |
| `POST /leaves/{id}/cancel` | A M E | Yalnız kendi pending talebi |
| `GET /leaves/pending` · `POST /leaves/{id}/approve\|reject` | A M | Onay kuyruğu (M ekip kapsamlı; kendi talebine karar veremez) |
| `GET /leaves/team` | A M E | Takım izin widget'ı |

## Puantaj / Mesai (`/attendance`) — tümü A M

`GET /attendance/live` (canlı yoklama) · `GET /attendance` (aylık satırlar) ·
`POST /attendance` + `PUT /attendance/{id}` (manuel giriş/düzeltme) ·
`GET /attendance/summary` (aylık özet — SQL view; fazla mesai bordroya beslenir)

## Bordro (`/payroll`)

| Uç | Rol | Açıklama |
|---|---|---|
| `GET/POST /payroll/periods` · `GET /payroll/periods/{id}` | A | Dönem yaşam döngüsü (fazla mesai puantajdan gelir) |
| `GET /payroll/periods/{id}/summary` | A | Dönem özeti (SQL view) |
| `PUT .../rows/{rowId}` · `POST .../check` · `POST .../rows/{rowId}/approve` · `POST .../submit` | A | Girdi düzenleme → kontrol → onay (sonuç snapshot) → dönem kapatma |
| `POST /payroll/preview` | A | Tekil brüt→net önizleme (**PayrollEngine** = payroll.js birebir) |
| `GET/PUT /payroll/settings` | A | Yıla göre versiyonlu parametreler + vergi dilimleri |
| `GET /payroll/my` | A E | Kendi bordrolarım |
| `GET .../rows/{rowId}/payslip` | A E | QuestPDF bordro pusulası (E yalnız kendisininkini) |

## İşe Alım / ATS (`/candidates`, `/positions`, `/recruitment`) — tümü yalnız A

`GET/POST /candidates` · `GET /candidates/{id}` · `PATCH /candidates/{id}/status`
(pipeline: Yeni→Mülakat→Teklif→Red; geçişler history'ye düşer; Hired kilitli) ·
`POST /candidates/{id}/notes|evaluations` · `POST /candidates/{id}/hire`
(→ Employee dönüşümü; kontenjan düşer) · `GET/POST /positions` · `GET /recruitment/funnel`

## Analitik & Risk (`/dashboard`)

| Uç | Rol | Açıklama |
|---|---|---|
| `GET /dashboard/metrics` | A M | Risk merkezi: skor/trend/departman riski/talent capacity (SQL view'lar; risk formülü dashboard.js ile birebir; M ekip kapsamlı) |
| `GET /dashboard/attrition` · `/burnout` · `/manager-load` · `/employee-voice` · `/compliance` | A M | Detay sayfaları |
| `GET /dashboard/overview` | A M E | Genel durum KPI'ları |

## Uyum & Belge (`/compliance`)

| Uç | Rol | Açıklama |
|---|---|---|
| `GET /compliance/documents` | A M | Durum/seviye/arama filtreli; M ekip kapsamlı |
| `GET /compliance/readiness` | A M | Hazırlık skoru (SQL view) + denetim kontrol listesi |
| `POST /compliance/documents` | A | Açık mükerrer belge 409 |
| `PUT .../documents/{id}` · `PATCH .../status` · `PATCH .../owner` | A | Durum akışı (aynı durum 409; Tamamlandı → seviye low) + sorumlu atama |

## Aksiyon Merkezi & Audit & Arama (`/actions`, `/audit-logs`, `/search`)

| Uç | Rol | Açıklama |
|---|---|---|
| `GET /actions` | A M E | priority/source/owner/status filtreli |
| `GET /actions/badge` | A M E | Açık aksiyon rozet sayacı |
| `POST/PUT/DELETE /actions...` | A | CRUD |
| `PATCH /actions/{id}/status` | A M | İleri yönlü: open→week→done (geri dönüş 409) |
| `GET /audit-logs` | A M | Append-only denetim izi (SQL trigger kaynaklı); modül/arama filtreli |
| `GET /search?q=` | A M E | Birleşik arama: personel (rol kapsamlı) + aksiyon + aday (yalnız A) |

## Ayarlar (`/settings`)

| Uç | Rol | Açıklama |
|---|---|---|
| `GET /settings` | A | Birleşik görünüm: şirket + bildirim + güvenlik + abonelik |
| `PUT /settings/company\|notifications\|security` | A | Profil, e-posta toggle'ları, 2FA |
| `POST /settings/company/logo` | A | PNG/JPG, maks 2 MB |
| `GET /settings/company/logo` | A M E | Header logosu (tüm roller) |

Bildirim tetikleyicileri toggle'lara uyar: **Yeni Personel Kaydı** ve **İzin Talepleri**
açıkken ilgili olaylar `IEmailSender`'a yazılır (geliştirmede `{Storage:Root}/outbox`).

## Diğer

`GET /ping` (sürüm/sağlık bilgisi) · `GET /health` (health check: self + DB)

## apiClient.js hizalaması (son kontrol — Faz 12)

`services/apiClient.js` sözleşmesindeki tüm placeholder'lar birebir karşılanır:

- ✅ `POST /api/auth/login` · ⛔ `POST /api/auth/register` (kaldırıldı, yukarı bkz.) · ✅ `GET /api/me`
- ✅ `GET /api/actions` · ✅ `GET /api/audit-logs` · ✅ `PATCH /api/actions/{id}/status`
- ✅ Base URL `https://localhost:7001/api` (launchSettings 7001) · ✅ Bearer token şeması
