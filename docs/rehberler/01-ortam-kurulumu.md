# 01 — Ortam Kurulumu

Bu rehber, projeyi sıfırdan bilgisayarında çalışır hale getirir.

## Önkoşullar

| Araç | Sürüm | Ne için |
| --- | --- | --- |
| .NET SDK | 9.0+ | Backend'i derleyip çalıştırmak |
| Node.js | 20+ | Frontend'i çalıştırmak (npm ile birlikte gelir) |
| MSSQL | LocalDB / SQL Express / tam sürüm | Veritabanı |

Kurulu olduklarını doğrula:

```bash
dotnet --version   # 9.x görmelisin
node --version     # v20+ görmelisin
```

## 1) Veritabanı

Backend varsayılan olarak şu bağlantıyı kullanır (`backend/src/Ekipzen.API/appsettings.json`):

```
Server=localhost,1433;Database=EkipzenDb;User Id=sa;Password=Your_strong_Pass123;...
```

Kendi SQL Server'ına göre bu bağlantıyı ortam değişkeniyle ezebilirsin (önerilen):

```bash
# Windows PowerShell
$env:ConnectionStrings__DefaultConnection = "Server=(localdb)\MSSQLLocalDB;Database=EkipzenDb;Trusted_Connection=True;TrustServerCertificate=True"
$env:ConnectionStrings__PlatformConnection = "Server=(localdb)\MSSQLLocalDB;Database=EkipzenPlatform;Trusted_Connection=True;TrustServerCertificate=True"
```

> **Çok önemli:** Uygulama **iki ayrı veritabanı** kullanır — `DefaultConnection`
> (`EkipzenDb`, kiracı verisi) ve `PlatformConnection` (`EkipzenPlatform`, kiracı kimliği:
> `Tenants`, `TenantDirectoryEntries`; bkz. [06](06-multi-tenancy.md)). Yalnız
> `DefaultConnection`'ı ezip `PlatformConnection`'ı unutursan uygulama verisi senin
> sunucuna, kiracı kimliği ise `appsettings.Development.json`'daki varsayılan sunucuya
> bağlanır — **hata vermeden**, ve login sırasında şaşırtıcı biçimde tutarsız davranır.
> İkisini birlikte ayarla.

> **Not:** Migration'lar ve demo seed, uygulama **Development** modunda ilk açılışta
> (her iki veritabanı için de) otomatik uygulanır. Elle bir şey yapmana gerek yok
> (detay: [07](07-veritabani-ve-migrationlar.md)).

## 2) Backend'i Çalıştır

```bash
cd backend
dotnet run --project src/Ekipzen.API --launch-profile http
```

- API adresi: **http://localhost:5053**
- Swagger (API dokümanı, uçları deneme): **http://localhost:5053/swagger**
- Sağlık kontrolü: **http://localhost:5053/health**

İlk açılışta veritabanı oluşturulur ve demo verilerle doldurulur (birkaç saniye sürebilir).

## 3) Frontend'i Çalıştır

Yeni bir terminalde:

```bash
cd frontend
npm install          # ilk sefer bağımlılıkları kurar
npm run dev
```

- Arayüz adresi: **http://localhost:5173**
- `/api` istekleri otomatik olarak `:5053`'e proxy'lenir (Vite ayarı).

## 4) Giriş Yap

Seed veritabanı üç demo kullanıcıyla gelir (hepsinin şifresi `demo123`):

| E-posta | Rol | Ne görürsün |
| --- | --- | --- |
| `ik@hrmaster.local` | hr-admin | Tüm modüller |
| `ece.arslan@hrmaster.local` | manager | Yönetici konsolu, ekip |
| `ahmet.yilmaz@hrmaster.local` | employee | Kişisel görünüm |

Login ekranı bu bilgileri öndolu getirir; doğrudan "Giriş yap" diyebilirsin.
Üstteki demo rol seçici ile roller arası hızlı geçiş yapabilirsin.

## 5) Testleri Çalıştır

```bash
# Backend (birim + entegrasyon; MSSQL çalışır olmalı)
cd backend && dotnet test

# Frontend (Vitest + React Testing Library)
cd frontend && npm test -- --run
```

## Sık Karşılaşılan Sorunlar

| Belirti | Olası neden / çözüm |
| --- | --- |
| Backend açılışta DB hatası | SQL Server çalışmıyor ya da bağlantı dizesi yanlış. `ConnectionStrings__DefaultConnection`'ı kontrol et. |
| Frontend "Network Error" | Backend çalışmıyor. Önce `:5053`'ü aç. |
| `npm run dev` tip hataları | Backend Swagger'ından tipler eski olabilir: backend çalışırken `npm run gen:api` çalıştır. |
| Entegrasyon testleri düşüyor | Testler `EkipzenDb_Test` veritabanını her koşuda sıfırlar; SQL Server erişilebilir olmalı. |
| Port zaten kullanımda | Önceki `dotnet run`/`npm run dev` süreci hâlâ açık olabilir; kapat. |

## Sonraki Adım

Kod nasıl organize edilmiş, bir istek nasıl işleniyor → [02 — Mimari](02-mimari-clean-architecture.md).
