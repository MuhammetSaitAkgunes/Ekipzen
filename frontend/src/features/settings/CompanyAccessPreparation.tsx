import { Link } from "react-router-dom";
import { useAuth } from "../../auth/AuthContext";
import { appPath } from "../../paths";
import { PublishedGuidancePractice } from "../guidance/PublishedGuidancePractice";

export function CompanyAccessPreparation() {
  const { user } = useAuth();
  return <section id="settings-access" className="set-card" aria-labelledby="access-preparation-title">
    <div className="card-head"><h3 id="access-preparation-title">Erişimi hazırlayın ve doğrulayın</h3></div>
    <p>Bu oturumun rolü: {user?.roleLabel || user?.role || "Oturum bilgisi alınamadı"}. İşlem yetkisini sunucu denetler; çalışan kartındaki unvan yetki vermez.</p>
    <ol>
      <li><strong>Doğru kişiyi ve ekip bağlantısını kontrol edin.</strong> <Link to={appPath("/personnel")}>Personel kartını açın</Link>; departman ve yönetici bilgilerini gerçek organizasyonla karşılaştırın. Kart düzeltmesi yönetici erişimini etkileyebilir.</li>
      <li><strong>Yeni çalışanın hesap hazırlığını takip edin.</strong> <Link to={appPath("/recruitment")}>İşe alım ekranında</Link> çalışan kaydını ve hesap hazırlama durumunu ayrı inceleyin. Başarısız hazırlama için desteklenen yeniden deneme yolunu kullanın; çalışanı ikinci kez oluşturmayın.</li>
      <li><strong>Girişi ayrıca doğrulayın.</strong> Hesap hazırlama sonucu, davet e-postasının teslimi veya kişinin giriş yaptığı anlamına gelmez. Aşağıdaki şifre değişikliği yalnız kendi oturumunuz içindir.</li>
    </ol>
    <p>Örnek: “Yönetici” unvanı olan kişinin doğru ekibe erişebildiğini ayrıca kontrol edin. Yanlış bağlantıyı personel kartında düzeltin; rastgele daha geniş yetki vermeyin. Bu ekranda genel rol atama, davet teslim takibi veya SMS doğrulama işlemi yoktur.</p>
    <PublishedGuidancePractice process="company" step="access-preparation" />
    <p>Bu açıklamayı okumak gerçek hesap işlemi veya yeterlilik kanıtı oluşturmaz.</p>
  </section>;
}
