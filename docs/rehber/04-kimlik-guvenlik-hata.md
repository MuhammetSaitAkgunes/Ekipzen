# 4. Kimlik, Güvenlik ve Hata Yönetimi

Bu üç konu her modülü kesen "altyapı"dır. Bir kez anlarsan tüm uygulamada aynı
şekilde çalışır.

## 4.1 Kimlik doğrulama: JWT + refresh token rotasyonu

**JWT (JSON Web Token)** = kullanıcının kim olduğunu kanıtlayan, imzalı, süresi
kısa bir "erişim bileti". Her API isteğinde `Authorization: Bearer <token>`
başlığında gider. Sunucu bileti imzasıyla doğrular — veritabanına bakmadan.

> **Neden JWT (durumsuz)?** Sunucu her istekte "bu oturum hâlâ geçerli mi?" diye
> DB'ye bakmaz; bileti imzayla doğrular. Ölçeklenmesi kolay. Bedeli: bilet süresi
> dolunca yenilenmeli — bu yüzden **refresh token** var.

**Refresh token** = erişim bileti dolduğunda yenisini almak için kullanılan,
uzun ömürlü, **veritabanında hash'lenmiş** saklanan gizli anahtar. Akış:

```
login  → erişim token (kısa) + refresh token (uzun, DB'de hash'li)
istek  → 401 (erişim token süresi doldu)
refresh→ eski refresh token'ı gönder → yeni erişim + yeni refresh al
         (eski refresh İPTAL edilir — "rotasyon")
```

`backend/.../Identity/IdentityService.cs`:
- `RefreshAsync`: token'ı hash'ler, DB'de bulur, aktifse **iptal eder** (tek
  kullanımlık) ve yeni çift üretir.
- `LogoutAsync`: refresh token'ı iptal eder (idempotent — yoksa sessiz başarı).

> **Neden rotasyon?** Bir refresh token çalınırsa, meşru kullanıcı bir kez
> yenileyince çalınan token geçersiz olur. Tek kullanımlık olması hasarı sınırlar.
> Token'ların **hash'li** saklanması: DB sızsa bile ham token ele geçmez.

### Frontend tarafı: sessiz yenileme (tek-uçuş)

`frontend/src/api/client.ts`: bir istek 401 dönerse, `apiFetch` **otomatik**
refresh dener ve isteği tekrarlar. Eşzamanlı birden çok 401 olsa bile tek bir
refresh isteği yapılır ("tek-uçuş" — `refreshInFlight` promise'i paylaşılır).
Refresh de düşerse **veya** tekrar da 401 dönerse oturum temizlenir ve `/login`'e
yönlendirilir. Kullanıcı çoğu zaman token yenilenmesini hiç fark etmez.

## 4.2 Yetkilendirme: roller ve policy'ler

Kimlik "sen kimsin?", yetki "ne yapabilirsin?" sorusudur. İki katmanda uygulanır:

**Backend (asıl güvenlik sınırı)** — Controller/uç üzerinde policy'ler:
- `[Authorize]` — giriş yapmış olmak yeter.
- `Policies.HrAdminOnly` — yalnız hr-admin (ör. bordro, ayarlar, silme uçları).
- `Policies.Management` — hr-admin + manager (ör. onay kuyruğu, denetim izi).

**Frontend (kullanıcı deneyimi)** — `routes.tsx`'te her rotanın `roles` dizisi;
`guards.tsx` yetkisiz rolü ya yönlendirir ya "yetki gerekli" ekranı gösterir.

> **Neden çift kontrol?** Frontend kontrolü **güvenlik değildir** — kullanıcı
> isteği elle (curl) atabilir. Gerçek sınır backend'dedir. Frontend kontrolü
> sadece "olmayan düğmeyi gösterme" nezaketi. İkisi bilinçli olarak **aynı matrisi**
> yansıtır; biri diğeriyle çelişirse ya 403 (frontend fazla cömert) ya da kör
> düğme (frontend fazla cimri) oluşur.

## 4.3 Veri kapsamı: `ScopeFor` — yetkinin en ince katmanı

Bir manager `/api/leaves/pending` çağırınca, uç ona açık (`Management` policy) —
ama **tüm şirketin** bekleyen taleplerini görmemeli, yalnız **kendi ekibinin**.
Bunu policy yapamaz (policy uç seviyesinde çalışır, veri seviyesinde değil).

Çözüm: `ScopeFor(currentUser)` uzantısı. Bir `IQueryable`'ı role göre daraltır:
- hr-admin → her şey,
- manager → yönettiği personeller + kendisi,
- employee → yalnız kendisi.

```csharp
var query = context.LeaveRequests.ScopeFor(currentUser);  // rol kapsamı burada
```

Bu tek satır, "veri sızıntısı"nı sistematik olarak önler. Aynı desen izin, uyum,
personel, dashboard sorgularında kullanılır.

> **Gerçek bir hata örneği:** Kod incelemesinde (Bölüm 8.7 #4) uyum belgeleri
> `ScopeFor` ile daraltılırken, uyum **hazırlık skoru** yanlışlıkla şirket geneli
> view'dan dönüyordu. Yani manager, ekip tablosuyla şirket geneli KPI'yı yan yana
> görüyordu — hem yanıltıcı hem sızıntı. Düzeltme: skoru da aynı kapsamdan hesapla.

## 4.4 Hata yönetimi: tek hattan

İyi bir kurumsal uygulamada hatalar **tutarlı** olmalı: backend her hatayı aynı
formatta döner, frontend her hatayı aynı şekilde gösterir.

### Backend: anlamlı istisna → ProblemDetails

Handler'lar HTTP kodu düşünmez; **anlamlı istisna** fırlatır:

| İstisna | HTTP | Anlam |
| --- | --- | --- |
| `ValidationException` | 400 | Gövde geçersiz (alan bazlı mesajlar) |
| `UnauthorizedException` | 401 | Kimlik doğrulanamadı |
| `ForbiddenAccessException` | 403 | Yetki yok / kapsam dışı |
| `NotFoundException` | 404 | Kayıt yok |
| `ConflictException` | 409 | İş kuralı ihlali (ör. yetersiz bakiye) |

`GlobalExceptionHandler` (middleware) bunları **ProblemDetails** JSON'una çevirir
(RFC 7807: `title`, `status`, `errors`). Tek yerde. Handler kodu temiz kalır:

```csharp
if (days > available)
    throw new ConflictException($"Yetersiz izin bakiyesi: ...");   // 409 + mesaj
```

### Frontend: ProblemDetails → ApiError → toast

`apiFetch` başarısız yanıtı bir `ApiError`'a (status + mesaj + alan hataları)
çevirir. Sayfa bunu yakalar:
- **400** doğrulama → alan bazlı form hatası veya form-error kutusu,
- **409** iş kuralı → toast'ta doğrudan Türkçe mesaj,
- **401** → sessiz refresh / login'e yönlendirme,
- **403** → "yetki yok" (genelde rota guard'ı zaten keser).

Sonuç: hiçbir sayfa "hata kodu 500" gibi ham şeyler göstermez; kullanıcı her
zaman **Türkçe, anlamlı** bir mesaj görür.

## 4.5 Güvenlikte öğrenilen ders (kritik bulgu)

Kod incelemesinde bulunan **en ciddi açık** (Bölüm 8.7 #1): kayıt ucu
(`/api/auth/register`) anonimdi (giriş gerektirmez, doğru) **ama** istemciden
gelen `role` alanını kabul ediyordu. Yani herhangi biri
`{"role":"hr-admin"}` göndererek kendine yönetici hesabı açabilirdi — klasik
**yetki yükseltme (privilege escalation)**.

Düzeltme: anonim kayıt istemci rolünü **tamamen yok sayar**, her zaman `employee`
atar. Yükseltilmiş roller yalnız seed veya (ileride) yetkili bir yönetim
akışıyla verilir.

> **Ders:** "Girdiye asla güvenme" ilkesi rol/yetki alanları için hayatidir.
> Bir alanın kullanıcı tarafından set edilebilir olması, o alanın **güvenli**
> olduğu anlamına gelmez.

---

Önceki: [← 3. Veri Modeli](03-veri-modeli.md) · Sonraki: [5. Frontend →](05-frontend.md)
