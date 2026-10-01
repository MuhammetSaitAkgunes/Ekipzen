import { useQuery } from "@tanstack/react-query";
import { Link } from "react-router-dom";
import { apiFetch } from "../../api/client";
import type { components } from "../../api/schema";
import { appPath } from "../../paths";
import { useDepartments } from "../personnel/queries";
import { GuidanceStepLessons } from "../guidance/GuidanceStepLessons";

type SetupStatus = components["schemas"]["TenantSetupStatusDto"];

export function CompanyPreparationGuide({ companyName }: { companyName: string }) {
  const setup = useQuery({ queryKey: ["tenant-setup"], queryFn: () => apiFetch<SetupStatus>("/setup") });
  const departments = useDepartments();

  return <section className="set-card ez-company-preparation" aria-labelledby="company-preparation-title">
    <span className="ez-overline">İK REHBERİ · ŞİRKET HAZIRLIĞI</span>
    <h3 id="company-preparation-title">İlk çalışan kaydından önce</h3>
    <p>Başlangıç kayıtları, şirket profili ve departmanlar sonraki işlemlerin temelidir. Aşağıdaki sıra, gerçek kayıtları kontrol etmenize yardımcı olur; bir kaydın varlığı içeriğinin onaylandığını göstermez.</p>
    <ol>
      <li>
        <strong>Başlangıç kayıtlarını inceleyin</strong>
        <p>{setup.isPending ? "Kurulum durumu okunuyor…" : setup.isError
          ? "Kurulum durumu okunamadı; aşağıdaki panelden yeniden kontrol edin."
          : setup.data.needsInitialization
            ? "Eksik başlangıç kayıtları var. Oluşturulacak kayıtları gözden geçirin."
            : "Başlangıç kayıtları mevcut; izin türü, tatil ve bordro ayarlarının uygunluğu ayrıca incelenmeli."}</p>
        <p className="ez-company-preparation-example"><strong>Neden?</strong> Eksik temel kayıtlar izin ve bordro hazırlığını etkiler. Şablon, şirketin gerçek kurallarının onayı değildir.</p>
        <a href="#settings-setup">Kurulum durumuna git</a>
      </li>
      <li>
        <strong>Şirket profilini doğrulayın</strong>
        <p>{companyName.trim() ? `Kayıtlı şirket adı: ${companyName} — adı ve iletişim bilgilerini gerçek şirket bilgileriyle karşılaştırın.`
          : "Şirket adı boş görünüyor. Kaydetmeden önce yetkili kişiyle doğrulayın."}</p>
        <p className="ez-company-preparation-example"><strong>Örnek:</strong> Sistem e-postası eskiyse tahmin ederek değiştirmeyin; doğru adresi yetkili kişiyle teyit edin.</p>
        <a href="#settings-company">Şirket bilgilerine git</a>
      </li>
      <li>
        <strong>Departmanı oluşturun veya düzeltin</strong>
        <p>{departments.isPending ? "Departman listesi okunuyor…" : departments.isError
          ? "Departman listesi okunamadı; aşağıdaki panelden yeniden deneyin."
          : departments.data.length === 0
            ? "Henüz departman yok. Çalışan kartından önce gerçek ekibin departmanını ekleyin."
            : `${departments.data.length} departman kayıtlı. Çalışanı eklemeden önce doğru ekibin listede bulunduğunu kontrol edin.`}</p>
        <p className="ez-company-preparation-example"><strong>Neden?</strong> Yanlış departman, çalışan kartındaki ekip bilgisini ve sonraki filtreleri yanıltır. Ad veya kod sonradan düzeltilebilir.</p>
        <a href="#settings-departments-title">Departmanlara git</a>
      </li>
    </ol>
    <p className="ez-company-preparation-next">Bu kontrollerden sonra <Link to={appPath("/personnel/new")}>çalışan kartı rehberine geçin</Link>. Buradaki durumlar kişisel öğrenme ilerlemesi veya kurulum onayı değildir.</p>
    <GuidanceStepLessons process="company" />
  </section>;
}
