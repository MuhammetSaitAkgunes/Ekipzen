import { PersonnelDecisionCheck } from "./PersonnelDecisionCheck";
import { Link } from "react-router-dom";
import { appPath } from "../../paths";

export type PersonnelGuideStep =
  | "tab-kimlik" | "tab-iletisim" | "tab-is"
  | "tab-mali" | "tab-ozluk" | "tab-evrak";

type GuideContent = {
  title: string;
  purpose: string;
  action: string;
  reason: string;
  example: string;
  check: string;
};

const content: Record<PersonnelGuideStep, GuideContent> = {
  "tab-kimlik": {
    title: "Kişiyi doğru kaydet",
    purpose: "Bu kart, sonraki izin, puantaj ve bordro işlemlerinde aynı çalışanı bulmak için kullanılır.",
    action: "Adı, soyadı ve varsa kimlik numarasını çalışanın sunduğu belgeyle karşılaştırın.",
    reason: "Yanlış kişi bilgisi sonraki kayıtlara da taşınabilir. Emin olmadığınız bilgiyi tahmin ederek doldurmayın.",
    example: "Soyadı belgede iki kelimeyse ikisini de yazın; başka bir çalışanın kartından kopyalamayın.",
    check: "Kaydetmeden önce adı, soyadı ve kimlik numarasını tekrar okuyun.",
  },
  "tab-iletisim": {
    title: "Ulaşılabilir bilgileri gir",
    purpose: "İletişim bilgileri çalışanla ve acil durumda belirttiği kişiyle bağlantı kurmayı sağlar.",
    action: "Yalnız çalışanın doğruladığı iletişim ve acil durum bilgilerini girin.",
    reason: "Eski veya varsayılan bir numara, gerektiğinde yanlış kişiye ulaşılmasına neden olabilir.",
    example: "Acil durum kişisinin adı biliniyor, telefonu bilinmiyorsa telefon uydurmak yerine boş bırakın.",
    check: "Telefonu ve e-posta adresini çalışanla doğrulayın; paylaşımı yetkili kişilerle sınırlayın.",
  },
  "tab-is": {
    title: "Çalışma ilişkisini tanımla",
    purpose: "Departman, görev, başlangıç tarihi ve sözleşme türü sonraki süreçlerin başlangıç bilgisidir.",
    action: "İşe giriş tarihini ve sözleşme türünü onaylı iş belgeleriyle karşılaştırın.",
    reason: "Buradaki yanlış seçim izin, puantaj ve bordro hazırlığını etkileyebilir.",
    example: "Başlangıç tarihi kesinleşmediyse bir tarih tahmin ederek kaydetmeyin; önce yetkili kişiyle netleştirin.",
    check: "Sözleşme türünü yalnız ekrandaki açıklamaya bakarak seçmeyin; imzalı belgeyle doğrulayın.",
  },
  "tab-mali": {
    title: "Ödeme hazırlığını kontrol et",
    purpose: "Banka ve ücret bilgileri ödeme ve bordro hazırlığında kullanılır.",
    action: "Çalışanın doğruladığı IBAN ve banka bilgilerini girin. Ücret paketini kayıt oluştuktan sonra ayrıca tanımlayın.",
    reason: "Personel kartını kaydetmek ücret paketini veya bordro dönemini kendiliğinden tamamlamaz.",
    example: "IBAN henüz teyit edilmediyse başka bir çalışanın hesabını örnek olarak girmeyin.",
    check: "Bordro hazırlığına geçmeden önce ödeme bilgileri ve yürürlük tarihli ücret paketini ayrıca inceleyin.",
  },
  "tab-ozluk": {
    title: "Gerekli ek bilgileri ayır",
    purpose: "Özlük, ekipman ve çalışma koşullarına ilişkin bilgiler farklı ihtiyaçlar için tutulur.",
    action: "Sağlık ve çalışma koşulu alanlarını yalnız doğrulanmış bilgiyle doldurun.",
    reason: "Bir kutunun işaretli olması, çalışanın o işe uygunluğunun ayrıca değerlendirildiği anlamına gelmez.",
    example: "Yüksekte çalışma bilgisi bilinmiyorsa varsayılan seçimle karar vermeyin.",
    check: "Hassas bilgileri yalnız gerekli olduğu ölçüde ve yetkili kişilerle işleyin.",
  },
  "tab-evrak": {
    title: "Belgeleri kayda bağla",
    purpose: "Evraklar ilgili çalışan kartında izlenir; dosya yüklemek için önce kart oluşturulmalıdır.",
    action: "Kart kaydedildikten sonra gerekli belgeyi doğru türle yükleyin ve yüklenen dosyayı kontrol edin.",
    reason: "Kartın açılması, evrakların veya uygulama dışındaki bildirimlerin tamamlandığını göstermez.",
    example: "İmzalı sözleşme henüz gelmediyse varmış gibi işaretlemeyin; eksikliği takip edin.",
    check: "Evrak listesini ve uygulama dışındaki kalan işleri ayrıca gözden geçirin.",
  },
};

export function PersonnelGuide({ step, index }: { step: PersonnelGuideStep; index: number }) {
  const item = content[step];
  return <section className="ez-personnel-guide" aria-label="İşe giriş rehberi">
    <div className="ez-personnel-guide-heading">
      <span>İŞE GİRİŞ REHBERİ · {index}/6</span>
      <h3>{item.title}</h3>
    </div>
    <p>{item.purpose}</p>
    <p><strong>Şimdi ne yapmalı?</strong> {item.action}</p>
    <details>
      <summary>Neden, örnek ve kontrol noktası</summary>
      <p><strong>Neden?</strong> {item.reason}</p>
      <p><strong>Örnek:</strong> {item.example}</p>
      <p><strong>Kontrol:</strong> {item.check}</p>
    </details>
    <PersonnelDecisionCheck key={step} step={step} />
    <Link to={appPath("/guidance/catalog?process=personnel-entry")}>İşe girişin kaynaklı rehberini aç</Link>
    <small>Kaynaklı metinler bağımsız inceleme ve yayın onayından sonra katalogda görünür.</small>
  </section>;
}
