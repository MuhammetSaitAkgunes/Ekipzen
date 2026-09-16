# Ekipzen

3 rollü (hr-admin / manager / employee) Türkçe bir İK SaaS **demosu** — gerçek
backend'e bağlı React arayüzü. Eski mock prototip, React portunun piksel-parite
referansı olarak `legacy-frontend/` altında korunur.

> **Üretim ürünü değildir.** Bordro/mevzuat hesapları demo amaçlıdır; üretim
> kullanımı öncesi mevzuat ve mali müşavir doğrulaması gerekir.

## Depo Yapısı

| Klasör | İçerik |
| --- | --- |
| `backend/` | .NET 9 Clean Architecture API (EF Core + MSSQL, JWT + refresh, QuestPDF bordro pusulası). Ayrıntı: `backend/README.md` |
| `frontend/` | React 18 + TypeScript + Vite + TanStack Query. Tipler backend Swagger'ından üretilir (`npm run gen:api`) |
| `legacy-frontend/` | Orijinal mock uygulama (değiştirilmez; parite referansı). Bkz. `legacy-frontend/README.md` |
| `docs/` | Geliştirme günlüğü (`docs/gelistirme-gunlugu.md`), tasarım dokümanı ve dilim planları (`docs/superpowers/`) |
| `raporlar/` | Backend faz planı (`raporlar/backend-development-plan.md`) |

## Kurulum ve Çalıştırma

Önkoşullar: .NET 9 SDK, Node.js 20+, MSSQL (LocalDB/SQL Express yeterli).

```bash
# 1) Backend — http://localhost:5053 (Swagger: /swagger)
cd backend
dotnet run --project src/Ekipzen.API --launch-profile http

# 2) Frontend — http://localhost:5173 (/api → :5053 proxy'lidir)
cd frontend
npm install
npm run dev
```

Testler:

```bash
cd frontend && npm test -- --run   # Vitest + RTL birim testleri
cd backend && dotnet test          # entegrasyon testleri
```

## Demo Kullanıcılar

Seed veritabanıyla birlikte gelir (tümü şifre: `demo123`):

| E-posta | Rol |
| --- | --- |
| `ik@hrmaster.local` | hr-admin |
| `ece.arslan@hrmaster.local` | manager |
| `ahmet.yilmaz@hrmaster.local` | employee |

## Modüller

Risk Merkezi (5 risk detay sayfası) · Genel Durum · Global Aksiyon Merkezi +
Denetim İzi · Personel Yönetimi (evrak/foto dahil) · İşe Alım (ATS + işe alım →
personel dönüşümü) · Mesai & Puantaj · İzin & Onay · Bordro (dönem yaşam
döngüsü, tekil hesaplama, pusula PDF) · Uyum & Belge · Yönetici Konsolu ·
Sistem Ayarları · Ctrl+K global arama.

Rol matrisi eski `routes.js` ile birebirdir; manager yalnız ekibini, employee
yalnız kendini görür.

## Dokümantasyon

- **Yeni başlayanlar için geliştirici rehberleri (A–Z):** `docs/rehberler/` —
  genel bakış, kurulum, mimari, backend/frontend, kimlik, multi-tenancy, veritabanı,
  testler, adım adım yeni özellik ekleme, sözlük/SSS.
- KVKK & veri izolasyonu (derin): `docs/kvkk-veri-izolasyonu.md`
- Kaldığımız yer + geçmiş: `docs/gelistirme-gunlugu.md`
- React port tasarım kararları: `docs/superpowers/specs/2026-07-13-react-frontend-port-design.md`
- Planlar (dilim + SaaS/KVKK): `docs/superpowers/plans/`
