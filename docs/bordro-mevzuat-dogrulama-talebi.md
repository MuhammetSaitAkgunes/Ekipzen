# Bordro Mevzuat Doğrulama Talebi

> **Amaç:** İK Pro bordro motorunu üretime almadan önce, hesapların mevzuata
> uygunluğunu bağımsız bir mali müşavir doğrulamasıyla sabitlemek.
>
> **Kime:** Mali müşavir / SMMM
> **Kimden:** İK Pro geliştirme
> **Durum:** ⬜ Doldurulmayı bekliyor

## Neden bu belge var

Bordro motoru şu an eski bir arayüz prototipinin hesap mantığını birebir taklit
ediyor; test edilen değerler o prototipten üretildi, **mevzuattan değil**. Bu
yüzden motorun bugünkü çıktısı "tutarlı" ama "doğrulanmış" değil.

Aşağıdaki tabloları doldurduğunuzda, verdiğiniz her satırı otomatik teste
çeviriyoruz. O testler bundan sonra her kod değişikliğinde çalışır; hesap sapması
olursa derleme kırılır. Yani bu belge tek seferlik bir kontrol değil, kalıcı bir
güvenlik ağı olur.

---

## Bölüm 1 — Parametreler

Hangi dönem için doğrulama yapıyorsak o dönemin resmî değerleri. Sistemdeki
mevcut değerler "Sistemde" sütununda; yanlışsa lütfen düzeltin.

Aşağıdaki değerler **açık kaynaklardan araştırılıp** sisteme işlendi (kaynaklar
Bölüm 5'te). Lütfen yalnızca **teyit** edin ya da düzeltin.

| Parametre | Sistemde (2026) | Teyit ✓/✗ | Düzeltme |
| --- | --- | --- | --- |
| Asgari ücret (brüt, aylık) | 33.030,00 | | |
| Net asgari ücret (kontrol) | 28.075,50 | | |
| SGK prime esas kazanç **alt** sınırı (aylık) | 33.030,00 | | |
| SGK prime esas kazanç **üst** sınırı (aylık) | 297.270,00 (asgari ücret × **9**) | | |
| SGK işçi payı | %14 | | |
| İşsizlik sigortası işçi payı | %1 | | |
| SGK işveren payı | %20,5 | | |
| İşsizlik sigortası işveren payı | %2 | | |
| Damga vergisi oranı | binde 7,59 | | |
| Asgari ücret **gelir vergisi** istisnası (aylık) | 4.211,33 | | |
| Asgari ücret **damga vergisi** istisnası (aylık) | 250,70 | | |
| Aylık normal çalışma saati | 225 | | |
| Ay içi standart gün sayısı | 30 | | |

> **Not — SGK tavan çarpanı:** 5510 s.K. m.82'deki çarpan 01.01.2026'dan itibaren
> 7,5'ten **9'a** yükseltilmiştir. Sistemdeki 297.270 bu yeni çarpana göredir.
>
> **Not — GV istisnası düzeltmesi:** Sistemde 4.211,00 yazıyordu. İstisna,
> asgari ücret üzerinden hesaplanan gelir vergisine eşit olmalı:
> (33.030 − %15 SGK) × %15 = 28.075,50 × 0,15 = **4.211,325 → 4.211,33**.
> Yuvarlanmış değerle asgari ücretliden 0,325 TL gelir vergisi kesiliyor ve net
> 28.075,175 çıkıyordu; düzeltmeden sonra resmî 28.075,50 tutuyor.

**Gelir vergisi dilimleri (kümülatif matrah):**

Ücret gelirleri tarifesi (ücret dışı gelirlerden 3. dilimde ayrışır):

| # | Üst sınır | Kümülatif vergi | Oran | Teyit ✓/✗ |
| --- | --- | --- | --- | --- |
| 1 | 190.000 | — | %15 | |
| 2 | 400.000 | 28.500 | %20 | |
| 3 | 1.500.000 | 70.500 | %27 | |
| 4 | 5.300.000 | 367.500 | %35 | |
| 5 | üstü | 1.697.500 | %40 | |

---

## Bölüm 2 — Şüphelendiğimiz noktalar

Kod incelemesinde tespit ettiğimiz, **mevzuata aykırı olabileceğinden şüphelendiğimiz**
davranışlar. Her biri için "doğru davranış nedir?" sorusuna yanıt bekliyoruz.

| # | Motorun bugünkü davranışı | Sorumuz |
| --- | --- | --- |
| S1 | SGK **alt** sınırı çalışılan gün ile oranlanıyor, **üst** sınır oranlanmıyor (tam ay uygulanıyor). | Ay içi giriş/çıkışta SGK tavanı da prim gün sayısına göre oranlanmalı mı? |
| S2 | Asgari ücret gelir vergisi ve damga vergisi istisnaları, çalışan ay içinde işe girse bile **tam ay** düşülüyor. | Kısmi aylarda istisnalar gün oranlı mı uygulanır? |
| S3 | Gelir vergisi istisnası **sabit tutar** (4.211) olarak düşülüyor. | İstisna, çalışanın kendi kümülatif vergi dilimine göre asgari ücret üzerinden **hesaplanmalı** mı? Çalışan üst dilime geçtiğinde istisna tutarı değişir mi? |
| S4 | **Yol ve yemek yardımı** brüte ekleniyor; SGK matrahına ve gelir vergisi matrahına tam giriyor — hiçbir istisna uygulanmıyor. | Araştırmada 2026 için şunlar geçiyor: yemek **nakit** günlük 300 TL / **ayni (kart)** 330 TL gelir vergisi istisnası, SGK tarafında günlük 158 TL; yol **ayni** günlük 158 TL, **nakit** ödemede istisna yok. Bu tutarlar ve gelir vergisi ile SGK için farklı sınır uygulanması doğru mu? İstisna fiilen çalışılan gün sayısıyla mı çarpılır? |
| S5 | Fazla mesai için **tek çarpan** var (varsayılan 1,5). | Hafta tatili ve ulusal bayram/genel tatil çalışması için ayrı çarpan (%100) gerekiyor mu? Hesaplama farkı nedir? |
| S6 | İşveren SGK primi **%20,5 tam** hesaplanıyor; hiçbir teşvik/indirim uygulanmıyor. | 5510 sayılı kanun kapsamındaki 5 puanlık indirim hangi koşullarda uygulanır? İşveren maliyetine nasıl yansır? |
| S7 | Hesap sonuçlarında **yuvarlama yapılmıyor** (kuruş sonrası basamaklar taşınıyor). | Hangi kalemler, hangi aşamada, kaç kuruşa yuvarlanmalı? (Her kalem ayrı mı, yoksa sadece net mi?) |
| S8 | **E-5C2 yazılım temeli hazır:** onaylı tarih etkili kural yalnız hesap taslağı üretiyor; harici uzman yolu korunuyor. | İlk üretim kuralındaki bölen, tavan, dahil yan haklar, eşikler ve gerçek vaka beklenen sonuçlarını onaylıyor musunuz? |

---

## Bölüm 3 — Doğrulama senaryoları

**En kritik bölüm.** Her satır için, verdiğimiz girdilerle mevzuata uygun
sonuçları yazmanızı rica ediyoruz. Boş bıraktığınız satırlar test edilmez.

Ortak varsayımlar: 4/a kapsamında, tam zamanlı, ek kesinti yok, teşvik yok
(aksi belirtilmedikçe). "Önceki kümülatif matrah" = yıl içinde önceki aylardan
devreden gelir vergisi matrahı.

### Senaryo A — Asgari ücretli, tam ay

Brüt 33.030 · 30 gün · mesai yok · yardım yok · önceki kümülatif matrah 0

| Kalem | Tutar |
| --- | --- |
| SGK matrahı | |
| SGK işçi payı | |
| İşsizlik işçi payı | |
| Gelir vergisi matrahı | |
| Gelir vergisi (istisna sonrası) | |
| Damga vergisi (istisna sonrası) | |
| **Net ücret** | |
| İşveren SGK payı | |
| İşveren işsizlik payı | |
| **Toplam işveren maliyeti** | |

### Senaryo B — Orta maaş, tam ay

Brüt 60.000 · 30 gün · mesai yok · yardım yok · önceki kümülatif matrah 0

*(Senaryo A ile aynı kalemler)*

### Senaryo C — Ay içi işe giriş (S1 + S2'yi belirler)

Brüt 60.000 · **15 gün** · mesai yok · yardım yok · önceki kümülatif matrah 0

*(Aynı kalemler + ayrıca: SGK matrahı hesaplanırken taban/tavan nasıl oranlandı?)*

### Senaryo D — SGK tavanını aşan maaş

Brüt 320.000 · 30 gün · mesai yok · yardım yok · önceki kümülatif matrah 0

*(Aynı kalemler)*

### Senaryo E — Yıl içi vergi dilimi geçişi (kümülatif doğrulama)

Brüt 60.000 · 30 gün · **önceki kümülatif matrah 185.000** (yani bu ay 1. dilimden 2.'ye geçiliyor)

*(Aynı kalemler + ayrıca: dilim geçişinde vergi nasıl bölündü?)*

### Senaryo F — Fazla mesai + yol/yemek (S4 + S5'i belirler)

Brüt 60.000 · 30 gün · **10 saat fazla mesai** · yol 2.000 · yemek 3.000 · önceki kümülatif matrah 0

| Kalem | Tutar |
| --- | --- |
| Fazla mesai tutarı | |
| Yol yardımının **SGK matrahına giren** kısmı | |
| Yemek yardımının **SGK matrahına giren** kısmı | |
| Yol yardımının **gelir vergisine giren** kısmı | |
| Yemek yardımının **gelir vergisine giren** kısmı | |
| SGK matrahı | |
| Gelir vergisi (istisna sonrası) | |
| Damga vergisi | |
| **Net ücret** | |

---

## Bölüm 4 — Sorumluluk notu

Bu doğrulama, yazılımın hesap mantığını sabitlemek içindir. Doldurulan değerler
otomatik testlere dönüştürülür ve motorun çıktısı bunlara göre düzeltilir.

Mevzuat değiştiğinde (asgari ücret güncellemeleri dahil, yıl içi değişiklikler
de) bu belgenin yenilenmesi ve testlerin güncellenmesi gerekir. Sistem
parametreleri yürürlük tarihiyle sakladığı için geçmiş dönemler etkilenmez.

---

## Bölüm 5 — Araştırma kaynakları

Bölüm 1 ve 3'teki değerler aşağıdaki açık kaynaklardan derlendi. **Bunlar mali
müşavir onayı yerine geçmez**; teyit için sunulmuştur.

- 2026 gelir vergisi tarifesi (31.12.2025 tarih, 33124 sayılı Resmî Gazete) —
  Alomaliye, "2026 Yılı Vergi Dilimleri"
- 2026 asgari ücret ve SGK PEK alt/üst sınırları; 5510 s.K. m.82 çarpanının
  7,5 → 9 değişimi — muhasebetr, Verginet SGK genelge özeti, EY sirküleri
- 2026 yemek/yol istisna tutarları — Kolay İK, PwC Türkiye köşe yazısı
- Temmuz 2026'da ara zam yapılmadığı bilgisi — Uzmanpara/Hürriyet derlemeleri

**Doldurma tarihi:** ⬜
**Doğrulayan (ad / unvan / SMMM no):** ⬜
