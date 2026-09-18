/**
 * Tanıtım sitesi metinleri — tek yerden düzenlenir.
 * İÇERİK: siteInfo alanları yayın öncesi gerçek şirket bilgileriyle doğrulanmalıdır.
 * Kural: sahte müşteri, yorum ya da metrik eklenmez; güvenlik maddeleri üründe var olan
 * özelliklerdir (bkz. docs/superpowers/specs/2026-09-17-yeni-kimlik-landing-design.md).
 * Üslup: ölçülü ve kurumsal; İK terimleriyle konuşur, sonuç anlatır, abartı ve süre/sayı vaadi içermez.
 */
export const siteInfo = {
  brand: "Ekipzen",
  legalName: "Ekipzen",
  email: "iletisim@ekipzen.com",
  city: "Türkiye",
};

export const navItems = [
  { to: "/ozellikler", label: "Özellikler" },
  { to: "/fiyatlandirma", label: "Fiyatlandırma" },
  { to: "/guvenlik", label: "Güvenlik" },
  { to: "/hakkimizda", label: "Hakkımızda" },
];

export type MarketingModule = { id: string; icon: string; title: string; summary: string; points: string[]; roles: string };

export const modules: MarketingModule[] = [
  {
    id: "risk-merkezi", icon: "fa-shield-heart", title: "Risk Merkezi",
    summary: "Ayrılma, tükenmişlik ve uyum sinyallerini departman düzeyinde izleyin; her sinyali bir aksiyonla takibe alın.",
    points: ["Departman bazında risk skoru ve dönemsel eğilim", "Yönetici yükü ve çalışan nabzı görünümü", "Sinyale bağlı aksiyon önerileri ve takibi"],
    roles: "İK yöneticileri ve yöneticiler",
  },
  {
    id: "personel", icon: "fa-user-tie", title: "Personel Yönetimi",
    summary: "Özlük bilgileri, belgeler ve organizasyon yapısı tek bir kayıt düzeninde tutulur.",
    points: ["Personel kartı ve belge arşivi", "Excel dosyasından toplu içe aktarma", "İşten ayrılış süreci ve kayıt geçmişi"],
    roles: "İK ekipleri",
  },
  {
    id: "izin", icon: "fa-plane-departure", title: "İzin Yönetimi",
    summary: "İzin talebi, yönetici onayı ve bakiye takibi aynı akış içinde ilerler.",
    points: ["Çalışanın kendi izin talebini oluşturması", "Yönetici onay akışı", "Güncel izin bakiyeleri"],
    roles: "Tüm çalışanlar",
  },
  {
    id: "puantaj", icon: "fa-clock", title: "Mesai ve Puantaj",
    summary: "Günlük mesai kayıtları dönem sonunda bordroya hazır bir puantaja dönüşür.",
    points: ["Günlük mesai ve fazla mesai kaydı", "Dönem puantaj özeti", "Puantaj verisinin bordroya aktarımı"],
    roles: "İK ekipleri ve yöneticiler",
  },
  {
    id: "bordro", icon: "fa-money-check-dollar", title: "Bordro",
    summary: "Dönem bordrosu, yürürlükteki SGK ve gelir vergisi kurallarına göre kontrollü ve izlenebilir biçimde hesaplanır.",
    points: ["Dönem bordrosu ve tekil hesaplama", "SGK, vergi ve istisna parametreleri", "Çalışanların kendi bordro pusulasına erişimi"],
    roles: "İK ve bordro uzmanları",
  },
  {
    id: "ise-alim", icon: "fa-briefcase", title: "İşe Alım",
    summary: "Adayları başvurudan teklif aşamasına kadar tek bir süreç içinde takip edin.",
    points: ["Aşama bazlı aday takibi", "Mülakat ve teklif durumları", "İşe alınan adayın davetle sisteme dahil edilmesi"],
    roles: "İK ekipleri",
  },
];

export const trustPoints = [
  { icon: "fa-scale-balanced", label: "KVKK gereklilikleri gözetilerek tasarlandı" },
  { icon: "fa-database", label: "Şirkete özel veritabanı" },
  { icon: "fa-user-lock", label: "Rol bazlı yetkilendirme" },
  { icon: "fa-file-invoice", label: "Güncel bordro mevzuatı" },
];

export const steps = [
  { title: "Demo talep edin", text: "Formu doldurun; ekibimiz süreçlerinizi ve ihtiyaçlarınızı konuşmak için sizinle iletişime geçer." },
  { title: "Hesabınız kurulur", text: "Şirketiniz için ayrı bir çalışma ortamı hazırlanır ve İK yöneticinize davet gönderilir." },
  { title: "Ekibinizi dahil edin", text: "Personel kayıtlarını içe aktarın, rolleri tanımlayın ve süreçleri Ekipzen üzerinden yürütmeye başlayın." },
];

export const securityPillars = [
  { icon: "fa-database", title: "Ayrı veritabanı", text: "İK verileriniz şirketinize ait ayrı bir veritabanında tutulur; başka müşterilerin İK verileriyle aynı tabloda bulunmaz." },
  { icon: "fa-user-lock", title: "Rol bazlı erişim", text: "İK yöneticisi, yönetici ve çalışan yalnız rolünün gerektirdiği verilere erişir; yetki kontrolü sunucu tarafında da uygulanır." },
  { icon: "fa-lock", title: "Şifreli bağlantı", text: "Tüm trafik HTTPS üzerinden şifrelenir; HSTS ve içerik güvenliği politikası (CSP) uygulanır." },
  { icon: "fa-key", title: "Kimlik doğrulama", text: "Kısa ömürlü oturum anahtarları, güçlü parola kuralları ve giriş denemelerine karşı sınırlama." },
  { icon: "fa-clock-rotate-left", title: "Denetim izi", text: "Kayıtlardaki değişiklikler, değişikliği yapan kişi ve zaman bilgisiyle birlikte saklanır." },
  { icon: "fa-file-export", title: "Veri taşınabilirliği", text: "Her kullanıcı kendi kişisel verisini uygulama içinden dışa aktarabilir." },
];

export const faqs = [
  { q: "Ekipzen hangi şirketlere uygun?", a: "Personel, izin, puantaj ve bordro süreçlerini tek bir kayıt düzeninde yürütmek isteyen küçük ve orta ölçekli şirketler için tasarlanmıştır." },
  { q: "Hesabı kendim oluşturabilir miyim?", a: "Hayır. Her şirket hesabı, güvenlik gereği ekibimiz tarafından ayrı bir ortamda kurulur. Demo talebinizin ardından sizinle iletişime geçiyoruz." },
  { q: "Verilerimiz nerede ve nasıl saklanıyor?", a: "İK verileriniz şirketinize ait ayrı bir veritabanında tutulur; başka müşterilerin İK verileriyle aynı tabloda bulunmaz. Erişim rol bazlıdır ve tüm bağlantılar şifrelidir." },
  { q: "Mevcut personel listemizi aktarabilir miyiz?", a: "Evet. Personel kayıtlarınızı bir Excel dosyasından toplu olarak içe aktarabilirsiniz." },
  { q: "Çalışanlar sisteme nasıl dahil oluyor?", a: "Her çalışan kendisine gönderilen davet bağlantısıyla şifresini belirler ve yalnız rolünün izin verdiği ekranlara erişir." },
];

export type Plan = { id: string; name: string; audience: string; highlighted?: boolean; features: string[] };

export const plans: Plan[] = [
  { id: "baslangic", name: "Başlangıç", audience: "Personel ve izin süreçlerini dijital ortama taşıyan ekipler için",
    features: ["Personel yönetimi", "İzin yönetimi", "Çalışan self-servis ekranları", "E-posta desteği"] },
  { id: "buyuyen", name: "Büyüyen", audience: "Puantaj ve bordroyu aynı sistemde yürütmek isteyen şirketler için", highlighted: true,
    features: ["Başlangıç paketinin tüm kapsamı", "Mesai ve puantaj", "Bordro", "İşe alım", "Öncelikli destek"] },
  { id: "kurumsal", name: "Kurumsal", audience: "Risk analitiği ve kuruma özel gereksinimleri olan organizasyonlar için",
    features: ["Büyüyen paketinin tüm kapsamı", "Risk Merkezi", "Yönetici konsolu", "Kurulum ve veri aktarımında destek"] },
];

export const comparisonRows: { label: string; values: [boolean | string, boolean | string, boolean | string] }[] = [
  { label: "Personel ve belge yönetimi", values: [true, true, true] },
  { label: "İzin talebi ve onayı", values: [true, true, true] },
  { label: "Çalışan self-servis ekranları", values: [true, true, true] },
  { label: "Mesai ve puantaj", values: [false, true, true] },
  { label: "Bordro hesaplama", values: [false, true, true] },
  { label: "İşe alım", values: [false, true, true] },
  { label: "Risk Merkezi", values: [false, false, true] },
  { label: "Yönetici konsolu", values: [false, false, true] },
  { label: "Destek", values: ["E-posta", "Öncelikli", "Atanmış ekip"] },
];

export const pricingFaqs = [
  { q: "Fiyatlar neden sitede yer almıyor?", a: "Fiyat, çalışan sayınıza ve kullanacağınız modüllere göre belirlenir. Bilgilerinizi paylaştığınızda size özel teklifi iletiyoruz." },
  { q: "Paket değiştirmek mümkün mü?", a: "Evet. İhtiyaçlarınız değiştiğinde paketinizi yükseltebilirsiniz; mevcut verileriniz olduğu gibi korunur." },
  { q: "Kurulum ayrıca ücretlendiriliyor mu?", a: "Kurulum ve veri aktarımının kapsamı teklifte ayrıca belirtilir." },
];

export const roleViews = [
  { title: "İK yöneticisi", text: "Şirket genelindeki İK süreçlerini ve risk sinyallerini yönetir.", points: ["Risk Merkezi ve aksiyonlar", "Personel, bordro ve işe alım", "Şirket ayarları"] },
  { title: "Yönetici", text: "Ekibinin durumunu izler, onay bekleyen talepleri sonuçlandırır.", points: ["Ekip izin ve mesai onayları", "Yönetici konsolu", "Ekibe ait risk sinyalleri"] },
  { title: "Çalışan", text: "Kendisiyle ilgili İK işlemlerini doğrudan uygulama üzerinden yürütür.", points: ["İzin talebi ve bakiye", "Bordro pusulası", "Kişisel verilerin dışa aktarımı"] },
];

export const values = [
  { title: "Güven", text: "İK verisi, bir şirketin en hassas verileri arasındadır. Mimariyi veri ayrımı ve en az yetki ilkesi üzerine kurduk." },
  { title: "Sadelik", text: "Her ekranın belirli bir işi tamamlamaya hizmet etmesini, gereksiz adımların ayıklanmasını önemsiyoruz." },
  { title: "Mevzuata uyum", text: "Bordro ve kişisel verilerin korunmasına ilişkin gereklilikleri sonradan eklenen bir katman olarak değil, ürünün temelinde ele alıyoruz." },
];
