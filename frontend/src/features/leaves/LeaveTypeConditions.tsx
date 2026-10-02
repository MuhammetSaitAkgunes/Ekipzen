import type { LeaveTypeDto } from "./queries";

const conditions: Record<string, { check: string; example: string; source: string; title: string }> = {
  Yıllık: {
    check: "Çalışma dönemini, hak edişi, önceki kullanım ve bekleyen rezervasyonları karşılaştırın. Ekrandaki şirket hak edişi kanuni süre hesabının veya uzman incelemesinin yerine geçmez.",
    example: "Defterde 14 gün kalırken bekleyen 3 günlük talep varsa yeni talebin uygunluğunu yalnız 14 sayısına bakarak kararlaştırmayın; başlangıç tarihine göre sunucu ön kontrolünü alın.",
    source: "https://www.csgb.gov.tr/Media/trddpibj/y%C4%B1ll%C4%B1k-%C3%BCcretli-izin-y%C3%B6netmeli%C4%9Fi.pdf", title: "Yıllık Ücretli İzin Yönetmeliği",
  },
  Mazeret: {
    check: "Gerçek gerekçe, tarih ve ilgili belgeyi doğrulayın. Genel “Mazeret” seçeneği, her mazeretin aynı süre veya koşula sahip olduğunu göstermez; belirsiz durumda İK sorumlusuyla netleştirin.",
    example: "Açıklamada evlilik, kayıtta başka bir mazeret bulunuyorsa sırf bakiye düşmüyor diye göndermeyin. Gerçek olayı, tarihleri ve işyeri kuralını karşılaştırın.",
    source: "https://www.csgb.gov.tr/Media/crldhi51/4857-say%C4%B1l%C4%B1-i%C5%9F-kanunu.pdf", title: "4857 sayılı İş Kanunu · ek madde 2",
  },
  Raporlu: {
    check: "Gerçek raporun kişi ve tarihlerini doğrulayın. Bu ekranda raporun geçerliliği, SGK bildirimi veya ödenek hakkı otomatik doğrulanmaz; sağlık ayrıntılarını açıklamaya gereksiz yere yazmayın.",
    example: "Raporda 5–7 Ekim yazıyorsa 5–9 Ekim seçimini düzeltin. Ekranın doğrudan onaylı kaydetmesi raporun veya dış bildirimin incelendiğini göstermez.",
    source: "https://www.sgk.gov.tr/Content/Post/e33fffdb-16b9-4722-a9c1-2bd0a2a220d1/Hastalik-Hali-2025-02-27-11-27-26", title: "SGK · hastalık hali ve istirahat raporu",
  },
  Uzaktan: {
    check: "Çalışma planını, tarihleri ve işyeri düzenlemesini doğrulayın. Uzaktan çalışma işin yürütülme biçimidir; bu seçeneğin izin ekranında bulunması yıllık izin kullanıldığı anlamına gelmez.",
    example: "Salı günü evden çalışılacaksa yıllık izin yerine doğrulanmış çalışma planıyla ilerleyin. Bu kayıt sözleşmeyi veya puantajı kendiliğinden düzenlemez.",
    source: "https://www.csgb.gov.tr/Media/cyxd4g3n/uzaktan-%C3%A7al%C4%B1%C5%9Fma-y%C3%B6netmeli%C4%9Fi.pdf", title: "Uzaktan Çalışma Yönetmeliği · madde 4–5",
  },
};

export function LeaveTypeConditions({ type }: { type?: LeaveTypeDto }) {
  if (!type) return <p>Türü seçince ilgili kayıt ve koşul kontrolünü okuyun. Tür adı tek başına hukuki uygunluk kanıtı değildir.</p>;
  const condition = conditions[type.code ?? ""];
  return <section className="ez-leave-request-guide" aria-label="Seçilen türün koşul kontrolü">
    <h4>{type.name} · Neyi doğrulayacağım?</h4>
    <p>{condition?.check ?? "Bu şirket türünün kaynak ve koşul eşlemesi tanımlı değil. Gerçek gerekçe, tarih ve şirket kuralını İK sorumlusuyla doğrulayın; adından otomatik koşul çıkarmayın."}</p>
    {condition && <><p><strong>Örnek:</strong> {condition.example}</p>
      <a href={condition.source} target="_blank" rel="noopener noreferrer">{condition.title}</a></>}
    <p>Bakiye ve onay etiketleri şirket kaydındaki uygulama davranışıdır. Kaynaklı dersin güncel uzman incelemesini ayrıca kontrol edin.</p>
  </section>;
}
