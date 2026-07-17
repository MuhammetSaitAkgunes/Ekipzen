# 9. Sözlük & Onboarding

## 9.1 Sözlük

### İK / bordro terimleri

| Terim | Açıklama |
| --- | --- |
| **Bordro** | Maaş bordrosu; brüt maaştan kesintiler düşülerek net maaşın bulunduğu belge/hesap |
| **Puantaj** | Bir ayda kimin kaç gün/saat çalıştığının kaydı (bordronun girdisi) |
| **Brüt / Net** | Brüt = kesinti öncesi; Net = kesinti sonrası ele geçen |
| **SGK** | Sosyal Güvenlik Kurumu; maaştan kesilen sigorta primi (işçi + işveren payı) |
| **PEK (SGK matrahı)** | Prime Esas Kazanç; SGK priminin üzerinden hesaplandığı tutar (taban–tavan sınırlı) |
| **Matrah** | Bir verginin/kesintinin üzerinden hesaplandığı tutar |
| **Kümülatif matrah** | Yıl başından beri biriken gelir vergisi matrahı (vergi dilimini belirler) |
| **Gelir vergisi dilimi** | Artan oranlı vergi kademesi; matrah büyüdükçe oran artar |
| **Damga vergisi** | Bordro üzerinden alınan sabit oranlı küçük vergi |
| **İşsizlik primi** | İşsizlik sigortası için kesilen pay (işçi + işveren) |
| **İstisna** | Vergiden/kesintiden muaf tutulan sabit tutar (ör. asgari ücret gelir vergisi istisnası) |
| **İşveren maliyeti** | Şirketin bir personel için toplam ödediği (brüt + işveren SGK/işsizlik) |
| **4/a** | SGK'da işçi statüsü (eski SSK); bordro motoru bu statüye göre hesaplar |
| **Hak ediş / Devir** | Yıllık izin hakkı / önceki yıldan devreden izin günleri |
| **Uyum (compliance)** | Personel evraklarının ve denetim hazırlığının mevzuata uygunluk takibi |

### Teknik terimler

| Terim | Açıklama |
| --- | --- |
| **Clean Architecture** | Bağımlılıkların hep içeriye (Domain'e) aktığı katmanlı mimari |
| **CQRS** | Command (yazma) ve Query (okuma) işlemlerini ayırma deseni |
| **MediatR** | Command/Query'leri Handler'lara ileten kütüphane (+ pipeline behavior) |
| **Handler** | Bir command/query'nin iş mantığını çalıştıran sınıf |
| **DTO** | Data Transfer Object; API'nin dışa verdiği/aldığı veri şekli |
| **EF Core** | Entity Framework Core; .NET ORM'i (nesne↔tablo eşlemesi) |
| **Migration** | Veritabanı şemasının versiyonlu değişim kaydı |
| **View (SQL)** | Kaydedilmiş sorgu; okuma modeli olarak kullanılır |
| **Seed** | Demo/başlangıç verisinin veritabanına eklenmesi |
| **JWT** | JSON Web Token; imzalı, durumsuz erişim bileti |
| **Refresh token** | Erişim bileti dolunca yenisini almak için kullanılan uzun ömürlü gizli anahtar |
| **Policy** | Backend'de bir uca erişim kuralı (ör. HrAdminOnly) |
| **ScopeFor** | Bir sorguyu role göre daraltan uzantı (veri kapsamı) |
| **ProblemDetails** | Standart hata JSON formatı (RFC 7807) |
| **TanStack Query** | Frontend'de sunucu durumunu (cache/invalidation) yöneten kütüphane |
| **Mutation / Query** | TanStack'te yazma / okuma işlemi |
| **invalidateQueries** | Bir mutasyondan sonra ilgili cache'i "bayat" işaretleyip yeniden çektirme |
| **Parite** | Yeni ekranın eski mock uygulamayla piksel-piksel aynı olması |
| **gen:api** | Backend Swagger'ından TypeScript tiplerini üreten komut |
| **Duman testi (smoke)** | Uygulamayı gerçekten çalıştırıp temel akışları uçtan uca doğrulama |
| **Debounce** | Kullanıcı yazmayı bırakınca (ör. 300ms) tek istek atma tekniği |

## 9.2 İlk hafta yol haritası

**Gün 1 — Kur ve gez**
- [ ] Backend + frontend'i çalıştır ([Bölüm 7.2](07-gelistirme-akisi.md)).
- [ ] Üç rolle de giriş yap; her modülü tıkla. Rol farklarını gözlemle.
- [ ] Eski mock uygulamayı (`legacy-frontend`) aç, yeni uygulamayla yan yana koy.

**Gün 2 — Bir isteği baştan sona izle**
- [ ] Frontend'de bir izin talebi oluştur.
- [ ] Ağı (DevTools Network) izle: hangi uç, hangi gövde, hangi yanıt.
- [ ] Backend'de o ucun handler'ını bul (`Features/Leaves/Commands/...`), oku.
- [ ] [Bölüm 2.3](02-mimari.md#23-bir-isteğin-uçtan-uca-yolculuğu) ile eşleştir.

**Gün 3 — Testleri çalıştır ve oku**
- [ ] `npm test -- --run` ve `dotnet test` çalıştır (hepsi yeşil olmalı).
- [ ] Bir modülün testini oku (ör. `LeavesTests.cs`) — davranış nasıl doğrulanıyor?
- [ ] Bir testi kasten boz, ne olduğunu gör, geri al.

**Gün 4 — Küçük bir değişiklik yap**
- [ ] Küçük bir metin/etiket değişikliği yap; test + build yeşil kalsın.
- [ ] Bir dal aç, commit et (mesaj konvansiyonuna uy).

**Gün 5 — İlk gerçek görev**
- [ ] Reçeteyi izle ([Bölüm 7.8/7.9](07-gelistirme-akisi.md)): önce test, sonra kod.
- [ ] `docs/gelistirme-gunlugu.md`'yi oku — "şu an neredeyiz" ile bağlam kur.

## 9.3 "Nereye bakarım?" hızlı referans

| Soru | Cevap |
| --- | --- |
| Bir uç ne yapıyor? | `backend/.../Application/Features/<Modül>/` handler'ı |
| Bir rota kime açık? | `frontend/src/routes.tsx` `roles` matrisi |
| Bir frontend değeri nereden geliyor? | `schema.d.ts` → DTO → handler `Select` → entity/view |
| Bir sayı nasıl hesaplanıyor? | İlgili handler veya `PayrollEngine`/SQL view |
| Neden böyle yapılmış? | [Bölüm 8](08-kararlar-neden.md) |
| Bir hata mesajı nereden? | Backend handler `throw new ...Exception("mesaj")` |
| Şu an ne durumdayız? | `docs/gelistirme-gunlugu.md` en üst |

## 9.4 Sık yapılan hatalar (kaçın)

- ❌ Ham `fetch` kullanmak → ✅ her zaman `apiFetch`/`apiDownload`.
- ❌ Elle DTO tipi yazmak → ✅ `schema.d.ts`'ten al, `gen:api` çalıştır.
- ❌ İş kuralını controller'a koymak → ✅ handler'a koy.
- ❌ Sorguda `ScopeFor` unutmak → ✅ veri sızıntısı! rol kapsamını ekle.
- ❌ `getByText` çoklu eşleşmede patlar → ✅ `getByRole`/`getAllByText`.
- ❌ Renk/px hardcode → ✅ CSS token'ı kullan (parite).
- ❌ Onaylı bordro/dönemi değiştirmeye çalışmak → ✅ guard 409 döndürür (bilinçli).
- ❌ Paylaşımlı test DB'sini kirletmek → ✅ oluşturduğun kaydı `finally`'de temizle.

---

Önceki: [← 8. Kararlar](08-kararlar-neden.md) · Başa dön: [README](README.md)
