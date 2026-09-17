import { Link } from "react-router-dom";
import { usePageMeta } from "../../ui";
import { siteInfo } from "../content";

// İÇERİK: Bu metin genel bir çerçevedir; yayın öncesi hukuk danışmanı tarafından
// veri sorumlusu unvanı, adresi ve saklama süreleriyle kesinleştirilmelidir.
export function KvkkPage() {
  usePageMeta("KVKK aydınlatma metni", "Ekipzen web sitesi ve demo talep formu kapsamında kişisel verilerin işlenmesine ilişkin aydınlatma metni.");
  return (
    <section className="ez-section">
      <div className="ez-container ez-narrow ez-prose">
        <p className="ez-eyebrow">Yasal</p>
        <h1>KVKK aydınlatma metni</h1>
        <p>
          Bu metin, 6698 sayılı Kişisel Verilerin Korunması Kanunu'nun 10. maddesi uyarınca, Ekipzen web sitesini
          ziyaret eden ve demo talep formunu dolduran kişileri bilgilendirmek amacıyla hazırlanmıştır.
        </p>
        <h2>Veri sorumlusu</h2>
        <p>{siteInfo.legalName} ("Ekipzen"). İletişim: <a href={`mailto:${siteInfo.email}`}>{siteInfo.email}</a></p>
        <h2>İşlenen kişisel veriler</h2>
        <p>Demo talep formunda paylaştığınız ad soyad, iş e-posta adresi, şirket adı, çalışan sayısı aralığı, telefon numarası ve mesajınız.</p>
        <h2>İşleme amaçları ve hukuki sebep</h2>
        <p>
          Verileriniz; demo talebinize yanıt vermek, sizinle iletişime geçmek ve talep ettiğiniz teklifi hazırlamak
          amacıyla, KVKK m.5/2-c (bir sözleşmenin kurulmasıyla doğrudan ilgili olması) ve m.5/2-f (meşru menfaat)
          hukuki sebeplerine dayanılarak işlenir.
        </p>
        <h2>Aktarım</h2>
        <p>Verileriniz, talebinizi size iletmek için kullanılan e-posta hizmet sağlayıcısı dışında üçüncü kişilerle paylaşılmaz.</p>
        <h2>Toplama yöntemi</h2>
        <p>Verileriniz, web sitesindeki form aracılığıyla elektronik ortamda toplanır.</p>
        <h2>Saklama süresi</h2>
        <p>
          Demo talebinizi içeren veriler web sitesinde ayrıca saklanmaz; talebiniz e-posta ile iletilir. Bu e-posta,
          talebinizin sonuçlandırılması ve yasal yükümlülüklerin yerine getirilmesi için gerekli süre boyunca
          saklanır, ardından silinir.
        </p>
        <h2>İlgili kişi olarak haklarınız</h2>
        <p>
          KVKK m.11 uyarınca verilerinizin işlenip işlenmediğini öğrenme, bilgi talep etme, düzeltilmesini veya
          silinmesini isteme ve itiraz etme haklarına sahipsiniz. Başvurularınızı{" "}
          <a href={`mailto:${siteInfo.email}`}>{siteInfo.email}</a> adresine iletebilirsiniz.
        </p>
        <p><Link to="/iletisim">İletişim sayfasına dön</Link></p>
      </div>
    </section>
  );
}
