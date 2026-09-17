/**
 * Tanıtım sitesi metinleri — tek yerden düzenlenir.
 * İÇERİK: siteInfo alanları yayın öncesi gerçek şirket bilgileriyle doğrulanmalıdır.
 * Kural: sahte müşteri, yorum ya da metrik eklenmez; güvenlik maddeleri üründe var olan
 * özelliklerdir (bkz. docs/superpowers/specs/2026-09-17-yeni-kimlik-landing-design.md).
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
    summary: "Ayrılma, tükenmişlik ve uyum sinyallerini sorun büyümeden görün.",
    points: ["Departman bazında risk skoru ve trend", "Yönetici yükü ve çalışan nabzı görünümü", "Sinyalden aksiyona tek tıkla geçiş"],
    roles: "İK yöneticileri ve yöneticiler",
  },
  {
    id: "personel", icon: "fa-user-tie", title: "Personel Yönetimi",
    summary: "Özlük kayıtları, belgeler ve organizasyon tek, düzenli bir kaynakta.",
    points: ["Personel kartı ve belge arşivi", "Excel'den toplu içe aktarma", "Ayrılış süreci ve kayıt geçmişi"],
    roles: "İK ekipleri",
  },
  {
    id: "izin", icon: "fa-plane-departure", title: "İzin Yönetimi",
    summary: "Talep, onay ve bakiye takibi e-posta trafiği olmadan ilerler.",
    points: ["Çalışanın kendi talebini oluşturması", "Yönetici onay akışı", "Güncel izin bakiyeleri"],
    roles: "Tüm çalışanlar",
  },
  {
    id: "puantaj", icon: "fa-clock", title: "Mesai ve Puantaj",
    summary: "Mesai kayıtlarını ve puantajı bordroya hazır hale getirin.",
    points: ["Günlük mesai ve fazla mesai takibi", "Dönem puantaj özeti", "Bordroya veri aktarımı"],
    roles: "İK ve yöneticiler",
  },
  {
    id: "bordro", icon: "fa-money-check-dollar", title: "Bordro",
    summary: "Türkiye mevzuatına uygun, kontrollü ve izlenebilir bordro hesabı.",
    points: ["Dönem bordrosu ve tekil hesaplama", "SGK, vergi ve istisna ayarları", "Çalışanlara pusula erişimi"],
    roles: "İK ve bordro uzmanları",
  },
  {
    id: "ise-alim", icon: "fa-briefcase", title: "İşe Alım",
    summary: "Aday havuzundan teklif aşamasına kadar süreci tek ekrandan yönetin.",
    points: ["Aşama bazlı aday takibi", "Mülakat ve teklif durumları", "İşe alınan adayı davetle sisteme dahil etme"],
    roles: "İK ekipleri",
  },
];

export const trustPoints = [
  { icon: "fa-scale-balanced", label: "KVKK odaklı tasarım" },
  { icon: "fa-database", label: "Şirket başına izole veritabanı" },
  { icon: "fa-user-lock", label: "Rol bazlı yetkilendirme" },
  { icon: "fa-file-invoice", label: "Türkiye bordro mevzuatı" },
];

export const steps = [
  { title: "Demo talep edin", text: "Formu doldurun; ekibimiz ihtiyaçlarınızı dinlemek için sizinle iletişime geçsin." },
  { title: "Hesabınız hazırlansın", text: "Şirketinize ait, diğer müşterilerden izole bir ortam kurulur ve yönetici davetiniz gönderilir." },
  { title: "Ekibinizi davet edin", text: "Personelinizi içe aktarın, rolleri belirleyin ve süreçleri tek yerden yönetmeye başlayın." },
];

export const securityPillars = [
  { icon: "fa-database", title: "İzole veri", text: "Her şirketin verisi kendi veritabanında tutulur; müşteriler arasında paylaşılan tablo yoktur." },
  { icon: "fa-user-lock", title: "Rol bazlı erişim", text: "İK yöneticisi, yönetici ve çalışan rolleri yalnız yetkili oldukları verileri görür; kontrol sunucu tarafında da uygulanır." },
  { icon: "fa-lock", title: "Şifreli iletişim", text: "Trafik HTTPS ile şifrelenir; HSTS ve sıkı içerik güvenliği politikası uygulanır." },
  { icon: "fa-key", title: "Güçlü kimlik doğrulama", text: "Kısa ömürlü oturum anahtarları, güçlü parola kuralları ve giriş denemelerine karşı sınırlama." },
  { icon: "fa-clock-rotate-left", title: "Denetim izi", text: "Kayıtlardaki değişiklikler kim ve ne zaman bilgisiyle izlenir." },
  { icon: "fa-file-export", title: "Veri taşınabilirliği", text: "Her kullanıcı kendi kişisel verisini tek tıkla dışa aktarabilir." },
];

export const faqs = [
  { q: "Ekipzen hangi şirketler için uygun?", a: "Personel, izin, puantaj ve bordro süreçlerini tek yerde toplamak isteyen küçük ve orta ölçekli şirketler için tasarlandı." },
  { q: "Kendim kaydolabilir miyim?", a: "Hayır. Güvenlik gereği her şirket hesabı ekibimiz tarafından ayrı bir ortamda kurulur. Demo talebi bıraktığınızda sizinle iletişime geçiyoruz." },
  { q: "Verilerim nerede ve nasıl saklanıyor?", a: "Her şirketin verisi kendi izole veritabanında tutulur. Erişim rol bazlıdır ve tüm trafik şifrelidir." },
  { q: "Mevcut personel listemi aktarabilir miyim?", a: "Evet. Personel kayıtlarınızı Excel dosyasından toplu olarak içe aktarabilirsiniz." },
  { q: "Çalışanlar sisteme nasıl dahil oluyor?", a: "Her çalışan kendine özel bir davet bağlantısıyla şifresini belirler ve yalnız rolünün izin verdiği ekranları görür." },
];

export type Plan = { id: string; name: string; audience: string; highlighted?: boolean; features: string[] };

export const plans: Plan[] = [
  { id: "baslangic", name: "Başlangıç", audience: "Süreçlerini dijitale taşımaya başlayan küçük ekipler",
    features: ["Personel yönetimi", "İzin yönetimi", "Çalışan self-servis", "E-posta desteği"] },
  { id: "buyuyen", name: "Büyüyen", audience: "Bordro ve puantajı tek yerde toplamak isteyen şirketler", highlighted: true,
    features: ["Başlangıç paketindeki her şey", "Mesai ve puantaj", "Bordro", "İşe alım", "Öncelikli destek"] },
  { id: "kurumsal", name: "Kurumsal", audience: "Risk analitiği ve özel ihtiyaçları olan organizasyonlar",
    features: ["Büyüyen paketindeki her şey", "Risk Merkezi", "Yönetici konsolu", "Kurulum ve veri aktarımı desteği"] },
];

export const comparisonRows: { label: string; values: [boolean | string, boolean | string, boolean | string] }[] = [
  { label: "Personel ve belge yönetimi", values: [true, true, true] },
  { label: "İzin talep ve onay", values: [true, true, true] },
  { label: "Çalışan self-servis", values: [true, true, true] },
  { label: "Mesai ve puantaj", values: [false, true, true] },
  { label: "Bordro hesaplama", values: [false, true, true] },
  { label: "İşe alım", values: [false, true, true] },
  { label: "Risk Merkezi", values: [false, false, true] },
  { label: "Yönetici konsolu", values: [false, false, true] },
  { label: "Destek", values: ["E-posta", "Öncelikli", "Atanmış ekip"] },
];

export const pricingFaqs = [
  { q: "Neden fiyatlar sitede yok?", a: "Fiyat, çalışan sayınıza ve seçtiğiniz modüllere göre belirlenir. Size özel teklifi kısa sürede iletiyoruz." },
  { q: "Paketler arasında geçiş yapabilir miyim?", a: "Evet. İhtiyacınız değiştikçe paketinizi yükseltebilirsiniz; verileriniz olduğu gibi korunur." },
  { q: "Kurulum ücreti var mı?", a: "Kurulum kapsamı teklifinizde açıkça belirtilir; sürpriz kalem çıkmaz." },
];

export const roleViews = [
  { title: "İK yöneticisi", text: "Tüm süreçleri ve riskleri tek panelden yönetir.", points: ["Risk Merkezi ve aksiyonlar", "Personel, bordro ve işe alım", "Şirket ayarları"] },
  { title: "Yönetici", text: "Ekibinin durumunu ve onay bekleyen işleri görür.", points: ["Ekip izin ve mesai onayları", "Yönetici konsolu", "Ekip risk sinyalleri"] },
  { title: "Çalışan", text: "Kendi işlerini İK'ya e-posta atmadan halleder.", points: ["İzin talebi ve bakiye", "Bordro pusulası", "Kişisel veri dışa aktarımı"] },
];

export const values = [
  { title: "Önce güven", text: "İK verisi bir şirketin en hassas verisidir. Mimariyi bu sorumlulukla, izolasyon ve en az yetki ilkesiyle kurduk." },
  { title: "Sadelik", text: "Her ekran bir işi bitirmek için var. Gereksiz adımları ve kalabalığı ayıklıyoruz." },
  { title: "Yerel uygunluk", text: "Türkiye'nin bordro ve KVKK gerekliliklerini sonradan eklenen bir katman olarak değil, temelden ele alıyoruz." },
];
