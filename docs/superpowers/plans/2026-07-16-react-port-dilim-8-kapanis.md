# React Port Dilim 8: Kapanış Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** React portunu kapat: eski mock frontend `legacy-frontend/`e taşınır, kök README monorepo gerçeğine göre yeniden yazılır, çalışma zamanı verisi ignore edilir, dokümantasyon güncellenir.

**Architecture:** Kod değişikliği yok — dosya taşıma (`git mv`), README/dokümantasyon yazımı ve `.gitignore`. React uygulaması `frontend/` altında kendi `index.html`'ini kullandığından taşımadan etkilenmez; doğrulama tam test + build + kısa Playwright dumanıyla yapılır.

**Tech Stack:** git mv, Markdown; mevcut Vitest/Playwright doğrulaması.

## Global Constraints

- Eski mock dosyaların **içeriği değiştirilmez** — yalnız taşınır (parite referansı olarak korunur; tüm yolları göreli olduğundan `legacy-frontend/` altında statik sunucuyla çalışmaya devam eder).
- Tüm kullanıcı metinleri Türkçe.
- Her görev sonunda `cd frontend && npm test -- --run` yeşil kalmalı.

---

### Task 1: Eski mock frontend'i `legacy-frontend/`e taşı

**Files:**
- Move: `index.html`, `main.js`, `routes.js`, `components/`, `styles/`, `services/` → `legacy-frontend/`
- Create: `legacy-frontend/README.md`

- [x] **Step 1: Taşı**

```bash
mkdir legacy-frontend
git mv index.html main.js routes.js components styles services legacy-frontend/
```

- [x] **Step 2: `legacy-frontend/README.md` yaz**

```markdown
# Legacy Frontend (Mock)

React portu öncesindeki orijinal mock uygulama. **Değiştirilmez** — React
portunun (`frontend/`) piksel-parite referansıdır.

## Çalıştırma

Statik bir sunucuyla kök yerine bu klasörü sunun:

```bash
npx serve -l 4173 legacy-frontend
# http://localhost:4173  (giriş formu demo bilgileriyle önceden doludur)
```

Veri tamamen mock'tur (`services/mockData.js` + localStorage); backend gerekmez.
```

- [x] **Step 3: Doğrula** — Run: `cd frontend && npm test -- --run` → tümü PASS; `npm run build` → hatasız. Kökte eski dosya kalmadığını `git status` ile gör.

- [x] **Step 4: Commit**

```bash
git add -A
git commit -m "chore: eski mock frontend legacy-frontend/ altına taşındı (parite referansı)"
```

---

### Task 2: Kök `.gitignore` — çalışma zamanı verisi

**Files:**
- Create: `.gitignore`

- [x] **Step 1: Yaz** (App_Data backend'in çalışma zamanında oluşturduğu yükleme deposudur — repo'ya girmez)

```gitignore
# Backend çalışma zamanı dosya deposu (logo/evrak yüklemeleri)
backend/src/Ekipzen.API/App_Data/
```

- [x] **Step 2: Doğrula** — `git status --short` çıktısında `App_Data` artık görünmez.

- [x] **Step 3: Commit**

```bash
git add .gitignore
git commit -m "chore: backend App_Data çalışma zamanı deposu ignore edildi"
```

---

### Task 3: Kök README'yi monorepo gerçeğine göre yeniden yaz

**Files:**
- Modify: `README.md` (eski içerik mock-demo dönemine ait; "Backend Not Included" vb. artık yanlış)

- [x] **Step 1: Yeni `README.md` yaz** — bölümler: proje tanımı (3 rollü İK SaaS demo: hr-admin/manager/employee), depo yapısı (`backend/` .NET 9 Clean Architecture + EF Core + MSSQL; `frontend/` React 18 + Vite + TanStack Query; `legacy-frontend/` parite referansı; `docs/` günlük/spec/planlar; `raporlar/` backend faz planı), kurulum ve çalıştırma (backend `dotnet run --project src/Ekipzen.API --launch-profile http` → :5053, frontend `npm install && npm run dev` → :5173 `/api` proxy'li, testler `npm test -- --run` + `dotnet test`), demo kullanıcılar tablosu (`ik@hrmaster.local` / `ece.arslan@hrmaster.local` / `ahmet.yilmaz@hrmaster.local`, hepsi `demo123`), modül listesi (Risk Merkezi, Genel Durum, Aksiyonlar, Personel, İşe Alım, Mesai & Puantaj, İzinler, Bordro, Uyum, Yönetici Konsolu, Ayarlar, global arama), dokümantasyon işaretçileri (günlük + tasarım dokümanı + backend plan), "Üretim değil demo" uyarısı (bordro/mevzuat müşavir doğrulaması notu).

Not: rozet/başlık görselleri sadeleştirilir; içerik Türkçe kalır. Eski README'nin ayrıntılı mock anlatımı silinir (legacy-frontend/README.md zaten işaret ediyor).

- [x] **Step 2: Commit**

```bash
git add README.md
git commit -m "docs: kök README monorepo (backend + React frontend) gerçeğine göre yeniden yazıldı"
```

---

### Task 4: Dokümantasyon + duman + kapanış

**Files:**
- Modify: `docs/gelistirme-gunlugu.md`

- [x] **Step 1: Günlükteki eski-yol referanslarını güncelle** — "kökteki `index.html`" ifadeleri `legacy-frontend/` olarak düzeltilir ("Çalıştırma komutları" ve "Öncesi — Backend" bölümleri).

- [x] **Step 2: Duman testi** — backend + frontend başlat; kısa Playwright: hr-admin girişi → `#/dashboard`, `#/payroll`, `#/actions` açılır (taşıma hiçbir şeyi kırmadı); `npx serve -l 4173 legacy-frontend` → eski uygulama login ekranı açılır.

- [x] **Step 3: Günlük kapanış kaydı** — "Şu an neredeyiz": React portu **tamamlandı** (8/8 dilim); sıradaki adım: merge + istenirse yeni işler (ör. E2E paketi, i18n) ayrı karar. Dilim 8 kaydı eklenir; plan kutuları işaretlenir.

- [x] **Step 4: Kapanış commit**

```bash
git add docs/
git commit -m "docs: dilim 8 kapanış — günlük güncellendi, React portu tamamlandı"
```

---

## Sonraki adımlar

Port tamamlandı. Kapsam dışı bırakılanlar (spec "YAGNI"): Tailwind, SSR, i18n, Storybook, Playwright E2E paketi — istenirse ayrı iş.
