import { useState, useSyncExternalStore } from "react";
import { useQueryClient } from "@tanstack/react-query";
import { useAuth } from "../auth/AuthContext";
import { Link } from "react-router-dom";
import { appPath } from "../paths";
import { acknowledgeWrite, pendingWrites, recoverySource, sessionIdentity, subscribeWriteRecovery } from "../api/writeRecovery";

export function WriteRecoveryNotice() {
  const { user } = useAuth();
  const writes = useSyncExternalStore(subscribeWriteRecovery, pendingWrites);
  const client = useQueryClient();
  const [checked, setChecked] = useState<string[]>([]);
  const [refreshing, setRefreshing] = useState(false);
  if (!user || !writes.length) return null;
  return <section className="ez-panel ez-write-recovery" role="alert" aria-label="Sonucu belirsiz işlemler">
    <h2>İşlem sonucu doğrulanamadı</h2>
    <p>İşlem kaydedilmiş olabilir. Aynı yazımı yeniden göndermeden ilgili gerçek kaydı, geçmişi ve varsa gönderim makbuzunu kontrol edin. Sayfayı yenilemek veya uyarıyı kaldırmak işlemi geri almaz; kaydedilmemiş form alanları başka cihaza taşınmaz.</p>
    <button type="button" className="btn btn-secondary" disabled={refreshing} onClick={async () => {
      setRefreshing(true); setChecked([]);
      try { await client.invalidateQueries(); } finally { setRefreshing(false); }
    }}>Açık ekranların güncel kayıtlarını yeniden yükle</button>
    <p>Yenileme işlemin sonucunu tek başına doğrulamaz; doğru kişiyi ve kaydı ayrıca inceleyin. Kayıt bulunamıyor veya sonuç anlaşılmıyorsa yetkili sorumluya danışın.</p>
    <ul>{writes.map(item => <li key={item.key}>
      <strong>{recoverySource(item.path).title}</strong>
      {recoverySource(item.path).target && <Link to={appPath(recoverySource(item.path).target!)}>İlgili kayıt ekranını aç</Link>}
      {item.replayId && <p>Gönderim kimliği: <code>{item.replayId}</code>. Sonuç kontrolünde bu kimliği koruyun; yeni kimlik yeni işlem anlamına gelebilir.</p>}
      <p>{new Date(item.at).toLocaleString("tr-TR")} · {item.replayId ? "Mevcut gönderim kimliği korunan işlem; aynı makbuzla sonuç okunabilir." : "Ayrı gönderim makbuzu olmayan yazım; otomatik tekrar yapılmadı."}</p>
      <label><input type="checkbox" checked={checked.includes(JSON.stringify([item.key, item.at]))} onChange={event => setChecked(values => event.target.checked ? [...values, JSON.stringify([item.key, item.at])] : values.filter(x => x !== JSON.stringify([item.key, item.at])))} /> İlgili gerçek kaydı ve sonucu kontrol ettim.</label>
      <button type="button" className="btn btn-ghost" disabled={!checked.includes(JSON.stringify([item.key, item.at])) || refreshing} onClick={() => {
        acknowledgeWrite(item.key, sessionIdentity()); setChecked(values => values.filter(x => x !== JSON.stringify([item.key, item.at])));
      }}>Kontrol uyarısını kaldır</button>
    </li>)}</ul>
    <p>Bu beyan gerçek işlem veya öğrenme kanıtı üretmez. Uyarı bu tarayıcı sekmesinde ve kendi oturum kapsamınızda tutulur.</p>
  </section>;
}
