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

export type ModuleStatus = "active" | "pilot" | "building";

export const STATUS_META: Record<ModuleStatus, { label: string; description: string; tone: "success" | "warning" | "primary" }> = {
  active: { label: "Kullanımda", description: "Genel kullanıma açık; müşteri ortamlarında kullanılabilir.", tone: "success" },
  pilot: { label: "Pilotta", description: "Seçili müşterilerle pilot kapsamda değerlendiriliyor.", tone: "warning" },
  building: { label: "Geliştiriliyor", description: "Genel kullanıma açılmadan önce geliştirilen modül.", tone: "primary" },
};

export type MarketingModule = { id: string; icon: string; title: string; summary: string; points: string[]; roles: string; status: ModuleStatus };

export const modules: MarketingModule[] = [
  {
    id: "personel", icon: "fa-user-tie", title: "Personel Yönetimi", status: "active",
    summary: "Özlük bilgileri, belgeler ve organizasyon yapısı tek bir kayıt düzeninde tutulur.",
    points: ["Personel kartı ve belge arşivi", "Excel dosyasından toplu içe aktarma", "İşten ayrılış süreci ve kayıt geçmişi"],
    roles: "İK ekipleri",
  },
  {
    id: "izin", icon: "fa-plane-departure", title: "İzin Yönetimi", status: "active",
    summary: "İzin talebi, yönetici onayı ve bakiye takibi aynı akış içinde ilerler.",
    points: ["Çalışanın kendi izin talebini oluşturması", "Yönetici onay akışı", "Güncel izin bakiyeleri"],
    roles: "Tüm çalışanlar",
  },
  {
    id: "puantaj", icon: "fa-clock", title: "Mesai ve Puantaj", status: "pilot",
    summary: "Günlük mesai kayıtları dönem sonunda bordroya hazır bir puantaja dönüşür.",
    points: ["Günlük mesai ve fazla mesai kaydı", "Dönem puantaj özeti", "Puantaj verisinin bordroya aktarımı"],
    roles: "İK ekipleri ve yöneticiler",
  },
  {
    id: "bordro", icon: "fa-money-check-dollar", title: "Bordro", status: "pilot",
    summary: "Dönem bordrosu, yürürlükteki SGK ve gelir vergisi kurallarına göre kontrollü ve izlenebilir biçimde hesaplanır.",
    points: ["Dönem bordrosu ve tekil hesaplama", "SGK, vergi ve istisna parametreleri", "Çalışanların kendi bordro pusulasına erişimi"],
    roles: "İK ve bordro uzmanları",
  },
  {
    id: "ise-alim", icon: "fa-briefcase", title: "İşe Alım", status: "active",
    summary: "Adayları başvurudan teklif aşamasına kadar tek bir süreç içinde takip edin.",
    points: ["Aşama bazlı aday takibi", "Mülakat ve teklif durumları", "İşe alınan adayın davetle sisteme dahil edilmesi"],
    roles: "İK ekipleri",
  },
  {
    id: "risk-merkezi", icon: "fa-shield-heart", title: "Risk Merkezi", status: "building",
    summary: "Devamsızlık, fazla mesai, kullanılmayan izin ve çalışan nabzı verilerinden departman düzeyinde risk sinyalleri üretmeye yönelik modül.",
    points: ["Departman bazında risk skoru ve dönemsel eğilim", "Yönetici yükü ve çalışan nabzı görünümü", "Sinyale bağlı aksiyon önerileri"],
    roles: "İK yöneticileri ve yöneticiler",
  },
];

/** Ana sayfa vitrininde sekme sırası. */
export const SHOWCASE_ORDER = ["personel", "izin", "puantaj", "bordro", "ise-alim", "risk-merkezi"];

export const trustPoints = [
  { icon: "fa-scale-balanced", label: "KVKK gereklilikleri gözetilerek tasarlandı" },
  { icon: "fa-database", label: "Şirkete özel veritabanı" },
  { icon: "fa-user-lock", label: "Rol bazlı yetkilendirme" },
  { icon: "fa-file-invoice", label: "Bordro mevzuatı esas alınarak geliştirildi" },
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
  { q: "Ekipzen hangi şirketlere uygun?", a: "Personel kayıtlarını, izin taleplerini ve işe alımı ortak bir çalışma alanında yönetmek isteyen küçük ve orta ölçekli şirketler için tasarlanmıştır." },
  { q: "Hesabı kendim oluşturabilir miyim?", a: "Şirket hesabı, ekibimiz tarafından şirkete özel bir ortamda hazırlanır. Demo talebinizin ardından kurulum kapsamını birlikte değerlendiririz." },
  { q: "Verilerimiz nerede ve nasıl saklanıyor?", a: "İK verileriniz şirketinize ait ayrı bir veritabanında tutulur; başka müşterilerin İK verileriyle aynı tabloda bulunmaz. Erişim rol bazlıdır ve tüm bağlantılar şifrelidir." },
  { q: "Mevcut personel listemizi aktarabilir miyiz?", a: "Evet. Personel kayıtlarınızı bir Excel dosyasından toplu olarak içe aktarabilirsiniz." },
  { q: "Çalışanlar sisteme nasıl dahil oluyor?", a: "Her çalışan kendisine gönderilen davet bağlantısıyla şifresini belirler ve yalnız rolünün izin verdiği ekranlara erişir." },
];

export type Plan = { id: string; name: string; audience: string; highlighted?: boolean; features: { label: string; status?: ModuleStatus }[] };

export const plans: Plan[] = [
  { id: "baslangic", name: "Başlangıç", audience: "Personel kayıtlarını ve izin süreçlerini düzenli yönetmek isteyen ekipler için",
    features: [
      { label: "Personel yönetimi" }, { label: "İzin yönetimi" },
      { label: "Çalışan self-servis ekranları" }, { label: "E-posta desteği" },
    ] },
  { id: "buyuyen", name: "Büyüyen", audience: "İşe alımı aynı kayıt düzenine eklemek, puantaj ve bordroyu pilot kapsamda değerlendirmek isteyen şirketler için", highlighted: true,
    features: [
      { label: "Başlangıç paketinin tüm kapsamı" }, { label: "Mesai ve puantaj", status: "pilot" },
      { label: "Bordro", status: "pilot" }, { label: "İşe alım" }, { label: "Öncelikli destek" },
    ] },
  { id: "kurumsal", name: "Kurumsal", audience: "Yönetim görünümü ve kuruma özel kapsam gereksinimleri olan organizasyonlar için",
    features: [
      { label: "Büyüyen paketinin tüm kapsamı" }, { label: "Risk Merkezi", status: "building" },
      { label: "Yönetici konsolu" }, { label: "Kurulum ve veri aktarımında destek" },
    ] },
];

export const comparisonRows: { label: string; status?: ModuleStatus; values: [boolean | string, boolean | string, boolean | string] }[] = [
  { label: "Personel ve belge yönetimi", values: [true, true, true] },
  { label: "İzin talebi ve onayı", values: [true, true, true] },
  { label: "Çalışan self-servis ekranları", values: [true, true, true] },
  { label: "Mesai ve puantaj", status: "pilot", values: [false, true, true] },
  { label: "Bordro hesaplama", status: "pilot", values: [false, true, true] },
  { label: "İşe alım", values: [false, true, true] },
  { label: "Risk Merkezi", status: "building", values: [false, false, true] },
  { label: "Yönetici konsolu", values: [false, false, true] },
  { label: "Destek", values: ["E-posta", "Öncelikli", "Atanmış ekip"] },
];

export const pricingFaqs = [
  { q: "Fiyatlar neden sitede yer almıyor?", a: "Fiyat, çalışan sayınıza ve kullanacağınız modüllere göre belirlenir. Bilgilerinizi paylaştığınızda size özel teklifi iletiyoruz." },
  { q: "Paket değiştirmek mümkün mü?", a: "Evet. İhtiyaçlarınız değiştiğinde paketinizi yükseltebilirsiniz; mevcut verileriniz olduğu gibi korunur." },
  { q: "Kurulum ayrıca ücretlendiriliyor mu?", a: "Kurulum ve veri aktarımının kapsamı teklifte ayrıca belirtilir." },
];

export const roleViews = [
  { title: "İK yöneticisi", text: "Personel kayıtlarını, izin süreçlerini ve işe alımı şirket genelinde yönetir.", points: ["Risk Merkezi (geliştiriliyor)", "Personel, bordro ve işe alım", "Şirket ayarları"] },
  { title: "Yönetici", text: "Ekibinin taleplerini ve onay süreçlerini tek çalışma alanından takip eder.", points: ["Ekip izin ve mesai onayları", "Yönetici konsolu", "Ekibe ait risk sinyalleri (geliştiriliyor)"] },
  { title: "Çalışan", text: "Kendisiyle ilgili İK işlemlerini doğrudan uygulama üzerinden yürütür.", points: ["İzin talebi ve bakiye", "Bordro pusulası", "Kişisel verilerin dışa aktarımı"] },
];

export type RoadmapStage = "now" | "next" | "planned";

export const ROADMAP_STAGES: { id: RoadmapStage; title: string; subtitle: string }[] = [
  { id: "now", title: "Şimdi", subtitle: "Pilotta doğrulanıyor" },
  { id: "next", title: "Sıradaki", subtitle: "Geliştirme sırasında" },
  { id: "planned", title: "Planlanan", subtitle: "Yol haritasında" },
];

export const roadmap: { stage: RoadmapStage; title: string; text: string }[] = [
  { stage: "now", title: "Mesai ve puantaj için çalışma düzeni profilleri", text: "Çalışma haftası, mola ve fazla mesai kurallarının şirket düzenine göre tanımlanması." },
  { stage: "now", title: "Bordro için paralel doğrulama dönemi", text: "İlk bordroların mevcut bordroyla paralel hesaplanıp uzman kabulünden geçirilmesi." },
  { stage: "next", title: "Canlı İK verisinden risk sinyalleri", text: "Risk Merkezi'nin şirketin kendi izin, mesai ve devamsızlık kayıtlarıyla çalışması." },
  { stage: "next", title: "Yedekleme ve geri dönüş güvencesi", text: "Düzenli yedek, sunucu dışı kopya ve denenmiş geri dönüş prosedürü." },
  { stage: "planned", title: "Çok faktörlü kimlik doğrulama", text: "Girişte ikinci doğrulama adımı." },
  { stage: "planned", title: "Genişleyen mevzuat kapsamı", text: "Farklı çalışan statüleri ve bordro senaryoları için desteğin artırılması." },
  { stage: "planned", title: "Ek raporlar", text: "İK ve yönetim için hazır rapor setleri." },
];

export const ROADMAP_NOTE = "Yol haritası, planlanan çalışmaları gösterir; yayın tarihi taahhüdü içermez.";

export const devProcess = [
  { title: "İhtiyaç", text: "İK ekiplerinden gelen gerçek iş akışları ve geri bildirimler." },
  { title: "Geliştirme", text: "Küçük, gözden geçirilebilir adımlar." },
  { title: "Otomatik testler", text: "Her değişiklik birim ve entegrasyon testlerinden geçer." },
  { title: "Pilotta doğrulama", text: "Gerçek verilerle, kontrollü bir müşteri ortamında." },
  { title: "Uzman kabulü", text: "Bordro gibi hesaplamalar alan uzmanının onayından geçer." },
  { title: "Genel kullanım", text: "Doğrulanan modül tüm müşterilere açılır." },
];

export const principles = [
  { icon: "fa-scale-balanced", title: "Önce doğruluk", text: "Hesaplamalar doğrulanmadan canlı kullanım için önerilmez." },
  { icon: "fa-database", title: "Veri ayrımı", text: "Her şirketin verisi kendi veritabanında tutulur." },
  { icon: "fa-comments", title: "Açık iletişim", text: "Modüllerin kapsamını ve kullanım durumunu açıkça paylaşırız." },
  { icon: "fa-layer-group", title: "Sadelik", text: "Her ekran belirli bir işi tamamlamaya hizmet eder." },
];

export const commitments = [
  { title: "Kapsamı açıkça tanımlarız.", text: "Her modülün durumu sitede ve üründe açıkça belirtilir." },
  { title: "Verinizin sahibi sizsiniz.", text: "Verileriniz şirketinize ait ayrı bir veritabanında tutulur; her kullanıcı kendi verisini dışa aktarabilir." },
  { title: "Bordroda uzman doğrulamasını esas alırız.", text: "Pilot kapsamındaki bordro çıktıları, ödeme veya beyan öncesinde alan uzmanı tarafından doğrulanır." },
];

export const journeySteps = [
  { id: "ise-alim", title: "İşe alım", text: "Aday işe alındığında davetle sisteme dahil edilir.", field: { label: "Durum", value: "İşe alındı" } },
  { id: "ozluk", title: "Özlük", text: "Personel kartı ve belgeler tek kayıtta toplanır.", field: { label: "Belgeler", value: "4 belge" } },
  { id: "izin", title: "İzin", text: "Talepler aynı kayıt üzerinden onaylanır, bakiye güncellenir.", field: { label: "Yıllık izin bakiyesi", value: "14 gün" } },
  { id: "puantaj", title: "Puantaj", text: "Günlük mesai kayıtları dönem puantajına dönüşür.", field: { label: "Dönem puantajı", value: "21 iş günü" } },
  { id: "bordro", title: "Bordro", text: "Bordro, dönemin her iş gününün puantaj ya da onaylı izinle karşılandığını kontrol eder.", field: { label: "Bordro durumu", value: "Hesaplamaya hazır" } },
];

export const securityLayers = [
  { id: "baglanti", benefit: "Tarayıcınızla sunucu arasındaki veri aktarımı korunur.", title: "Şifreli bağlantı", short: "HTTPS, HSTS, CSP", text: "Tüm trafik HTTPS üzerinden şifrelenir; HSTS ve içerik güvenliği politikası (CSP) uygulanır." },
  { id: "kimlik", benefit: "Hesabınıza erişim, oturum ve parola kontrolleriyle sınırlandırılır.", title: "Kimlik doğrulama", short: "Oturum, parola, deneme sınırı", text: "Kısa ömürlü oturum anahtarları, güçlü parola kuralları ve giriş denemelerine karşı sınırlama." },
  { id: "yetki", benefit: "Çalışanlar yalnız görevleri için gerekli verilere erişir.", title: "Rol bazlı yetki", short: "Arayüzde ve sunucuda", text: "İK yöneticisi, yönetici ve çalışan yalnız rolünün gerektirdiği verilere erişir; yetki kontrolü sunucu tarafında da uygulanır." },
  { id: "veritabani", benefit: "Şirketinizin İK kayıtları diğer müşterilerin kayıtlarından ayrılır.", title: "Şirkete ait veritabanı", short: "Veri ayrımı", text: "İK verileriniz şirketinize ait ayrı bir veritabanında tutulur; başka müşterilerin İK verileriyle aynı tabloda bulunmaz." },
  { id: "denetim", benefit: "Kayıtlarda kimin, ne zaman değişiklik yaptığını takip edebilirsiniz.", title: "Denetim izi", short: "Kim, ne zaman", text: "Kayıtlardaki değişiklikler, değişikliği yapan kişi ve zaman bilgisiyle birlikte saklanır." },
];

export const securityCategories = [
  { id: "veri", icon: "fa-database", title: "Veri", items: ["Şirkete ait ayrı veritabanı", "Denetim izi"] },
  { id: "erisim", icon: "fa-user-lock", title: "Erişim", items: ["Rol bazlı yetki", "Kısa ömürlü oturum anahtarları", "Güçlü parola kuralları"] },
  { id: "isletim", icon: "fa-gears", title: "İşletim", items: ["Şifreli bağlantı (HTTPS, HSTS, CSP)", "Giriş denemesi sınırlaması", "Yayın öncesi otomatik testler"] },
  { id: "gizlilik", icon: "fa-scale-balanced", title: "Gizlilik (KVKK)", items: ["Kişisel verilerin dışa aktarımı", "KVKK aydınlatma metni"] },
];

export const transparency = {
  provided: ["Şirkete ait ayrı veritabanı", "Rol bazlı yetki", "Şifreli bağlantı", "Denetim izi", "Kişisel veri dışa aktarımı", "Giriş denemesi sınırı"],
  inProgress: ["Çok faktörlü kimlik doğrulama", "Yedekleme ve geri dönüş güvencesi"],
};

export const aboutScenes = [
  { icon: "fa-table-cells", title: "Dağınık tablolar ve yazışmalar", text: "Personel bilgileri, izin talepleri ve puantaj farklı dosyalarda ve e-posta zincirlerinde tutulur." },
  { icon: "fa-clone", title: "Aynı bilginin tekrar girilmesi", text: "Bir çalışanın bilgisi her süreçte yeniden girilir; kayıtlar arasında tutarsızlıklar oluşur." },
  { icon: "fa-id-card", title: "Tek bir kayıt düzeni", text: "Ekipzen, personel, izin ve işe alım kayıtlarını ortak bir veri düzeninde buluşturur." },
];

export const demoIntro = "İK süreçlerinizi ve önceliklerinizi paylaşın. Ekibimiz, ürünün iş akışlarını şirketinizin ihtiyaçları üzerinden göstermek üzere sizinle iletişime geçecektir.";
export const demoSteps = [
  { title: "İhtiyaçlarınızı değerlendirelim", text: "Öncelikli İK süreçlerinizi ve kullanmak istediğiniz modülleri birlikte ele alalım." },
  { title: "Ürünü süreçlerinizle inceleyelim", text: "Demo görüşmesinde ilgili ekranları ve iş akışlarını gösterelim." },
  { title: "Kapsamı ve teklifi paylaşalım", text: "Modülleri, kurulum ve veri aktarımı kapsamını içeren teklifi değerlendirin." },
];

export const homeCopy = {
  audience: "Küçük ve orta ölçekli şirketler için İK yönetimi",
  headline: "Personel ve izin süreçlerinizi",
  emphasis: "tek yerden yönetin.",
  lead: "Personel kayıtlarını, izin taleplerini ve işe alımı ortak bir çalışma alanında yönetin. Ekipzen, İK ekiplerine tutarlı bir veri düzeni ve izlenebilir iş akışları sunar.",
  screenCaption: "Örnek verilerle hazırlanmış temsili ürün ekranı",
};
export const homeTrustPoints = [
  { icon: "fa-database", label: "Şirkete özel veritabanı" },
  { icon: "fa-user-lock", label: "Rol bazlı erişim" },
];
export const homeRoles = [
  { title: "İK ekibi", text: "Personel kayıtlarını, belgeleri ve işe alım süreçlerini aynı çalışma alanında yönetin." },
  { title: "Yönetici", text: "Ekibinizin taleplerini izleyin ve onay süreçlerini yönetin." },
  { title: "Çalışan", text: "İzin talebinizi oluşturun, güncel bakiyenizi ve kişisel işlemlerinizi görüntüleyin." },
];
export const onboardingSteps = [
  { title: "İhtiyaç ve kapsam görüşmesi", text: "İK süreçlerinizi ve ihtiyaç duyduğunuz modülleri birlikte değerlendirelim." },
  { title: "Şirketinize özel ortam", text: "Şirket hesabınız, verilerinize ayrılmış bir ortamda hazırlanır." },
  { title: "Kayıt aktarımı ve davetler", text: "Personel kayıtlarınızı Excel üzerinden aktarın; çalışanları davet bağlantısıyla sisteme dahil edin." },
];
export const onboardingNote = "Kurulum ve veri aktarımı desteğinin kapsamı, şirketinize sunulan teklifte belirtilir.";
export const pricingLead = "Paket kapsamı ve fiyatlandırma, çalışan sayınıza ve seçtiğiniz modüllere göre şekillenir. Kurulum ve veri aktarımı teklifte ayrıca ele alınır.";

export const playbackCopy = {
  pause: "Otomatik geçişi durdur", resume: "Otomatik geçişi sürdür",
  reduced: "Hareket azaltma tercihiniz etkin. Adımları kendiniz seçebilirsiniz.",
};
