type SingleMatch = { id: number; name: string } | null;

export function PersonnelRecordGuide({ resultCount, singleMatch, hasFilters, isHrAdmin,
  onFocusSearch, onClearFilters, onOpenRecord,
}: {
  resultCount: number;
  singleMatch: SingleMatch;
  hasFilters: boolean;
  isHrAdmin: boolean;
  onFocusSearch: () => void;
  onClearFilters: () => void;
  onOpenRecord: (id: number) => void;
}) {
  return <section className="ez-panel ez-personnel-record-guide" aria-labelledby="personnel-record-guide-title">
    <span className="ez-overline">İK REHBERİ · PERSONEL YÖNETİMİ</span>
    <h3 id="personnel-record-guide-title">Mevcut çalışan kaydını kontrol et</h3>
    <p>Önce doğru kişiyi bulun, sonra kartındaki bilgiyi dayanak belgeyle karşılaştırın. Bu filtrelerle erişebildiğiniz <strong>{resultCount} kayıt</strong> bulundu; sonuç sayısı öğrenme ilerlemesi değildir.</p>
    <ol>
      <li><strong>Kişiyi bulun.</strong> Ad, departman veya görevle arayın; aynı adlı kişilerde departman ve işe giriş tarihini karşılaştırın.</li>
      <li><strong>Kartı doğrulayın.</strong> Satırdaki adı açıp değişecek alanı ve mevcut değeri kontrol edin. Yanlış kişide işlem yapmayın.</li>
      <li><strong>Doğru işlemi seçin.</strong> {isHrAdmin
        ? "Bilgi hatasını kartın Düzenle eylemiyle düzeltin. Ayrılış için ilgili kişinin dosyasını, işe dönüş için pasif kişinin kartındaki ayrı akışı kullanın. Kaydettikten sonra sonucu yeniden okuyun."
        : "Bu rolde kartı inceleyebilirsiniz. Bir hata bulursanız düzeltmeyi yetkili İK kullanıcısına iletin."}</li>
    </ol>
    <div className="ez-personnel-record-guide-actions">
      <button className="btn btn-secondary btn-sm" type="button" onClick={onFocusSearch}>Arama alanına git</button>
      {hasFilters && <button className="btn btn-ghost btn-sm" type="button" onClick={onClearFilters}>Filtreleri temizle</button>}
      {singleMatch && <button className="btn btn-primary btn-sm" type="button" onClick={() => onOpenRecord(singleMatch.id)}>{singleMatch.name} kartını aç</button>}
    </div>
    <small>Genel bilgi düzenlemesi çalışanın aktif/pasif durumunu değiştirmez. Toplu seçim yalnız listedeki raporlama içindir.</small>
  </section>;
}
