# Ekipzen — Frontend'in React'e Dönüşümü (Tasarım)

> Durum: Onaylandı · Tarih: 2026-07-13
> Karar sahibi onayları: veri kapsamı, stack, stil, depo düzeni, veri katmanı — hepsi
> kullanıcıyla tek tek netleştirildi ve tasarımın tamamı onaylandı.

## Bağlam ve amaç

Mevcut frontend ~9.100 satırlık vanilla JS/CSS (11 modül, 3 rol, bundler yok) ve tamamen
mock veriyle çalışıyor. Backend (.NET 9, `backend/`) Faz 0–12 ile tamamlandı; 70+ uç
Swagger/OpenAPI ile yayında. Amaç: frontend'i React ekosistemine, **tasarım birebir aynı
kalacak şekilde** dönüştürmek ve aynı işte **gerçek API'ye bağlamak** (mock katmanı
tamamen kalkar).

## Verilen kararlar

| Karar | Seçim |
|---|---|
| Veri kapsamı | Port + gerçek API (JWT auth dahil; mock yalnız backend seed'inde yaşar) |
| Stack | Vite + React 18 + TypeScript + React Router v7 (SPA; SSR yok) |
| Stil | Mevcut ~4.600 satır CSS **aynen** taşınır; class adları ve DOM yapısı korunur |
| Depo düzeni | Yeni uygulama `frontend/`; eski dosyalar parite bitince `legacy-frontend/`e |
| Veri katmanı | TanStack Query + `openapi-typescript` ile swagger.json'dan tip üretimi |

## Mimari

- **`frontend/` SPA**: Vite + React 18 + TS + React Router v7.
- **Tip üretimi**: `npm run gen:api` → backend `swagger.json` → `src/api/schema.d.ts`
  (openapi-typescript). Sözleşme değişince tek komutla senkron; elle DTO yazılmaz.
- **Fetch wrapper** (`src/api/`): Bearer token ekler, RFC 7807 ProblemDetails zarfını tek
  yerde çözer, 401'de refresh-token akışını dener (mevcut `authService.js` davranışının
  birebir karşılığı), refresh de düşerse oturumu kapatıp login'e yönlendirir.
- **Sunucu durumu**: TanStack Query (cache/invalidation/loading). Global client state
  yok; oturum için küçük bir `AuthContext` yeter.
- **Grafikler**: Chart.js + react-chartjs-2 — mevcut canvas grafiklerin aynı kütüphaneyle
  birebir karşılığı (renkler CSS token'larından okunmaya devam eder).

## Klasör yapısı

```
frontend/src/
 ├─ api/          → üretilmiş tipler + fetch wrapper + queryClient
 ├─ auth/         → AuthContext, Login ekranı, RequireRole guard
 ├─ layout/       → AppShell: sidebar, header, global arama, açık-aksiyon rozeti
 ├─ features/     → dashboard/ overview/ personnel/ leaves/ attendance/ payroll/
 │                  recruitment/ compliance/ actions/ settings/ manager/
 ├─ styles/       → mevcut CSS dosyaları aynen (main.css, layout.css, payroll.css …)
 └─ routes.tsx    → routes.js rol matrisinin TS karşılığı (tek kaynak)
```

Her feature klasörü: sayfa component'i + alt component'ler + `queries.ts` (TanStack
Query hook'ları). Eski dev dosyalar (ör. 1.114 satırlık `dashboard.js`) ekran başına
ayrı dosyalara bölünür (RiskCenter, Overview, AttritionDetail…); davranış aynı kalır.

## Birebir tasarım stratejisi (kritik ilke)

1. CSS dosyaları **değiştirilmeden** kopyalanır; React component'leri **aynı class
   adlarıyla aynı DOM yapısını** üretir. `onclick` → React handler, `innerHTML`
   şablonu → JSX; görsel çıktı değişmez.
2. `frontend-design` ve `ui-ux-pro-max` becerileri implementasyonda iki yerde kullanılır:
   - her modül portunda eski/yeni ekran **yan yana parite kontrolü**,
   - API'yle zorunlu hale gelen **loading/skeleton, boş durum ve hata durumlarını**
     mevcut tasarım diline (petrol/evergreen token'ları, IBM Plex) uygun eklerken.
3. Eski uygulama, parite doğrulanana kadar yerinde kalır (yan yana karşılaştırma).

## Dönüşüm sırası (dikey dilimler)

Her dilim = ekran + gerçek API bağlantısı + parite kontrolü; dilim bitmeden sonrakine
geçilmez.

1. **İskelet**: Vite kurulumu, CSS taşıma, tip üretimi, AuthContext + login,
   AppShell + router + rol guard'ları (routes.js matrisi birebir)
2. **Overview + Dashboard/Risk Merkezi** (5 risk detay sayfası + grafikler)
3. **Personel + Departman** (directory, tam kart, foto/evrak yükleme-indirme)
4. **İzin & Onay** + **Puantaj**
5. **Bordro** (dönem yaşam döngüsü, ayarlar, pusula PDF indirme)
6. **İşe Alım** + **Uyum**
7. **Aksiyon Merkezi + Audit + global arama** + **Ayarlar** + **Yönetici konsolu**
8. **Kapanış**: eski dosyalar `legacy-frontend/`e taşınır; README ve dokümantasyon
   güncellenir

## Hata yönetimi

- ProblemDetails çözümü tek yerde (fetch wrapper) → TanStack Query `error` durumuna
  düşer → mevcut toast bileşeninin React karşılığıyla gösterilir (400 validasyon
  mesajları alan bazlı, 409 iş kuralı mesajı doğrudan).
- 401 → sessiz refresh denemesi → başarısızsa logout + login'e redirect.
- 403 → "yetkiniz yok" ekranı (rol guard'ları zaten çoğunu route seviyesinde keser).

## Test ve doğrulama

- **Vitest + React Testing Library**: login/guard yönlendirmeleri, rol bazlı menü
  görünürlüğü, form validasyonları, fetch wrapper'ın 401-refresh akışı.
- Her dilimde gerçek backend'e karşı manuel duman testi (demo kullanıcılarla üç rol).
- Piksel parite: eski/yeni yan yana manuel karşılaştırma (eski uygulama yerinde durur).

## Kapsam dışı (YAGNI)

Tailwind geçişi, SSR/Next.js, i18n altyapısı, Storybook, Playwright/E2E, tema
değiştirici eklentileri. Bunlar istenirse ayrı iş olarak ele alınır.
