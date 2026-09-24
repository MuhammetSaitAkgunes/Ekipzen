import { Link } from "react-router-dom";
import { appPath } from "../../paths";
import { useEmployeeDocuments, useEmployeeEmploymentFoundation } from "./queries";

export function PersonnelEntryFollowUp({ employeeId, verifiedCreation, onOpenDocuments, onOpenCompensation }: {
  employeeId: number;
  verifiedCreation: boolean;
  onOpenDocuments: () => void;
  onOpenCompensation: () => void;
}) {
  const documentsQ = useEmployeeDocuments(employeeId);
  const foundationQ = useEmployeeEmploymentFoundation(employeeId);
  const activePeriod = foundationQ.data?.employmentPeriods?.find((period) => period.endDate === null);
  const currentPackages = foundationQ.data?.compensationPackages?.filter(
    (item) => item.employmentPeriodId === activePeriod?.id,
  ) ?? [];

  return <section className="ez-panel ez-entry-follow-up" aria-labelledby="entry-follow-up-title">
    <span className="ez-overline">İŞE GİRİŞ · KARTTAN SONRA</span>
    <h2 id="entry-follow-up-title">Çalışan kartı oluşturuldu; kalan adımları kontrol edin</h2>
    <p>Bu panel yalnız mevcut kayıtları gösterir. Dosya veya ücret paketi kaydı bulunması, gerekli içeriğin uygunluğunu ya da tüm işe giriş yükümlülüklerinin tamamlandığını kanıtlamaz.</p>
    <ol>
      <li>
        <strong>Özlük belgeleri</strong>
        <p>{documentsQ.isPending ? "Dosya listesi yükleniyor…" : documentsQ.isError
          ? "Dosya listesi okunamadı; durumu doğrulamak için yeniden deneyin."
          : documentsQ.data.length === 0
            ? "Bu çalışan için henüz dosya yüklenmemiş."
            : String(documentsQ.data.length) + " dosya yüklenmiş. Türü, içeriği ve hangi belgenin gerektiğini ayrıca doğrulayın."}</p>
        <button className="btn btn-ghost btn-sm" onClick={onOpenDocuments}>Belgeleri aç</button>
      </li>
      <li>
        <strong>Ücret paketi</strong>
        <p>{foundationQ.isPending ? "Çalışma dönemi ve ücret kayıtları yükleniyor…" : foundationQ.isError
          ? "Ücret kayıtları okunamadı; durumu doğrulamak için yeniden deneyin."
          : !activePeriod
            ? "Etkin çalışma dönemi belirlenemedi; ücret hazırlığını tamamlandı saymayın."
            : currentPackages.length === 0
              ? "Etkin çalışma dönemi için ücret paketi kaydı bulunmuyor."
              : String(currentPackages.length) + " ücret paketi kaydı var. Yürürlük tarihini, tutarı ve kaynak referansını kontrol edin."}</p>
        <button className="btn btn-ghost btn-sm" onClick={onOpenCompensation}>Ücret bölümünü aç</button>
      </li>
      <li>
        <strong>Uygulama dışı adımlar</strong>
        <p>Gerekli bildirim veya dış sistem işlemlerini yetkili kişiyle ayrıca kontrol edin. Buradaki kayıtlar o işlemlerin yapıldığını doğrulamaz.</p>
      </li>
    </ol>
    {verifiedCreation && <small>Kartı oluşturma işleminiz sunucuda doğrulandı. <Link to={appPath("/guidance")}>Öğrenme yolculuğumu gör</Link></small>}
  </section>;
}
