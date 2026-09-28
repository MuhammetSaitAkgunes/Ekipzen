import { useEffect, useRef, useState } from "react";
import { ApiError, apiDownload } from "../../api/client";
import { useToast } from "../../layout/ToastProvider";
import { PageError, PageLoading } from "../shared/PageState";
import {
  useChangePassword, useSettings, useUpdateCompany, useUpdateNotifications,
  useUploadLogo, type NotificationSettingsDto,
} from "./queries";

import { TenantSetupPanel } from "./TenantSetupPanel";
import { DepartmentsPanel } from "./DepartmentsPanel";
import { CompanyPreparationGuide } from "./CompanyPreparationGuide";

const SECTIONS: [string, string, string][] = [
  ["general", "fa-building", "Şirket Bilgileri"],
  ["notif", "fa-bell", "Bildirimler"],
  ["security", "fa-shield-halved", "Güvenlik & Yetki"],
  ["billing", "fa-credit-card", "Abonelik & Fatura"],
];

const NOTIF_ROWS: [keyof NotificationSettingsDto & string, string, string][] = [
  ["newPersonnelEmail", "Yeni Personel Kaydı", "Sisteme yeni biri eklendiğinde yöneticilere bildir."],
  ["leaveRequestEmail", "İzin Talepleri", "Personel izin talebi oluşturduğunda anında e-posta gönder."],
  ["weeklyReportEmail", "Haftalık Rapor", "Henüz desteklenmiyor; otomatik rapor gönderilmez."],
];

export function SettingsPage() {
  const { showToast } = useToast();
  const settingsQ = useSettings();
  const updateCompany = useUpdateCompany();
  const updateNotifications = useUpdateNotifications();
  const uploadLogo = useUploadLogo();
  const changePassword = useChangePassword();
  const logoInputRef = useRef<HTMLInputElement>(null);

  const [section, setSection] = useState("general");
  const [company, setCompany] = useState<Record<string, string> | null>(null);
  const [passwords, setPasswords] = useState({ current: "", next: "", repeat: "" });
  const [passwordError, setPasswordError] = useState<string | null>(null);
  const [logoUrl, setLogoUrl] = useState<string | null>(null);

  useEffect(() => {
    if (company === null && settingsQ.data?.company) {
      const profile = settingsQ.data.company;
      setCompany({
        name: profile.name ?? "", website: profile.website ?? "",
        systemEmail: profile.systemEmail ?? "", phone: profile.phone ?? "",
        headquartersAddress: profile.headquartersAddress ?? "",
      });
    }
  }, [company, settingsQ.data]);

  // <img src> Bearer gönderemez; korumalı logo apiDownload ile blob URL'e çevrilir.
  const logoPath = settingsQ.data?.company?.logoPath ?? null;
  useEffect(() => {
    let objectUrl: string | null = null;
    if (logoPath) {
      apiDownload("/settings/company/logo")
        .then(({ blob }) => {
          objectUrl = URL.createObjectURL(blob);
          setLogoUrl(objectUrl);
        })
        .catch(() => setLogoUrl(null));
    } else {
      setLogoUrl(null);
    }
    return () => {
      if (objectUrl) URL.revokeObjectURL(objectUrl);
    };
  }, [logoPath]);

  if (settingsQ.isError) return <PageError error={settingsQ.error} />;
  if (settingsQ.isPending || company === null) return <PageLoading />;

  const data = settingsQ.data;
  const notifications = data.notifications ?? {};
  const subscription = data.subscription ?? {};

  const setCompanyField = (key: string) =>
    (e: React.ChangeEvent<HTMLInputElement | HTMLTextAreaElement>) =>
      setCompany((f) => ({ ...f!, [key]: e.target.value }));

  const saveCompany = async () => {
    try {
      await updateCompany.mutateAsync({
        name: company.name.trim(), website: company.website.trim() || null,
        systemEmail: company.systemEmail.trim() || null, phone: company.phone.trim() || null,
        headquartersAddress: company.headquartersAddress.trim() || null,
      });
      showToast("Ayarlar başarıyla kaydedildi.", "success");
    } catch (e) {
      showToast(e instanceof ApiError ? e.message : "Ayarlar kaydedilemedi.", "error");
    }
  };

  const toggleNotification = async (key: keyof NotificationSettingsDto & string) => {
    try {
      await updateNotifications.mutateAsync({
        newPersonnelEmail: notifications.newPersonnelEmail ?? false,
        leaveRequestEmail: notifications.leaveRequestEmail ?? false,
        weeklyReportEmail: false,
        [key]: !notifications[key],
      });
      showToast("Bildirim tercihi güncellendi.", "success");
    } catch (e) {
      showToast(e instanceof ApiError ? e.message : "Tercih kaydedilemedi.", "error");
    }
  };

  const submitPassword = async () => {
    setPasswordError(null);
    if (!passwords.next || !passwords.repeat) {
      setPasswordError("Yeni şifre alanlarını doldurun.");
      return;
    }
    if (passwords.next !== passwords.repeat) {
      setPasswordError("Şifreler eşleşmiyor, kontrol edin.");
      return;
    }
    try {
      await changePassword.mutateAsync({ currentPassword: passwords.current, newPassword: passwords.next });
      setPasswords({ current: "", next: "", repeat: "" });
      showToast("Şifreniz güncellendi.", "success");
    } catch (e) {
      setPasswordError(e instanceof ApiError ? e.message : "Şifre güncellenemedi.");
    }
  };

  const onLogoPick = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;
    try {
      await uploadLogo.mutateAsync(file);
      showToast("Logo yüklendi.", "success");
    } catch (err) {
      showToast(err instanceof ApiError ? err.message : "Logo yüklenemedi.", "error");
    } finally {
      e.target.value = "";
    }
  };

  return (
    <div id="settings-screen">
      <div className="page-header">
        <div>
          <h2>Sistem Ayarları</h2>
          <p>Şirket profili, bildirimler, güvenlik ve abonelik tercihlerini yönetin.</p>
        </div>
        <button className="btn btn-primary" onClick={saveCompany} disabled={updateCompany.isPending}>
          <i aria-hidden="true" className="fa-solid fa-save" /> Değişiklikleri Kaydet
        </button>
      </div>

      {section === "general" && <CompanyPreparationGuide companyName={data.company?.name ?? ""} />}
      <TenantSetupPanel onInitialized={() => setCompany(null)} />
      <div className="settings-layout">
        <aside className="settings-sidebar">
          <nav className="set-nav">
            {SECTIONS.map(([key, icon, label]) => (
              <button key={key} className={`set-link ${section === key ? "active" : ""}`} onClick={() => setSection(key)}>
                <i aria-hidden="true" className={`fa-solid ${icon}`} /> {label}
              </button>
            ))}
          </nav>
        </aside>

        <main className="settings-content">
          {section === "general" && (
            <div id="set-general" className="set-section active">
              <DepartmentsPanel />
              <div id="settings-company" className="set-card mt-4">
                <div className="card-head">
                  <h3>Marka & Görünüm</h3>
                  <p>Sistemde görünecek şirket adı ve marka varlıkları.</p>
                </div>
                <div className="form-row">
                  <div className="logo-upload">
                    <div className="current-logo">
                      {logoUrl
                        ? <img src={logoUrl} alt="Şirket logosu" style={{ maxWidth: "100%", maxHeight: "100%" }} />
                        : <i aria-hidden="true" className="fa-solid fa-building" />}
                    </div>
                    <div>
                      <input ref={logoInputRef} type="file" accept="image/png,image/jpeg" className="sr-only" aria-label="Logo dosyası" onChange={onLogoPick} />
                      <button className="btn-outline" onClick={() => logoInputRef.current?.click()} disabled={uploadLogo.isPending}>Logo Yükle</button>
                      <small>PNG, JPG, maksimum 2 MB</small>
                    </div>
                  </div>
                </div>
                <div className="form-grid-2">
                  <div className="input-group">
                    <label htmlFor="set-company-name">Şirket Adı</label>
                    <input id="set-company-name" type="text" className="input-control" value={company.name} onChange={setCompanyField("name")} />
                  </div>
                  <div className="input-group">
                    <label htmlFor="set-company-web">Web Sitesi</label>
                    <input id="set-company-web" type="text" className="input-control" value={company.website} onChange={setCompanyField("website")} />
                  </div>
                </div>
              </div>

              <div className="set-card mt-4">
                <div className="card-head"><h3>İletişim Bilgileri</h3></div>
                <div className="form-grid-2">
                  <div className="input-group">
                    <label htmlFor="set-company-email">E-Posta (Sistem)</label>
                    <input id="set-company-email" type="email" className="input-control" value={company.systemEmail} onChange={setCompanyField("systemEmail")} />
                  </div>
                  <div className="input-group">
                    <label htmlFor="set-company-phone">Telefon</label>
                    <input id="set-company-phone" type="tel" className="input-control" value={company.phone} onChange={setCompanyField("phone")} />
                  </div>
                  <div className="input-group col-span-2">
                    <label htmlFor="set-company-address">Merkez Adres</label>
                    <textarea id="set-company-address" rows={2} className="input-control" value={company.headquartersAddress} onChange={setCompanyField("headquartersAddress")} />
                  </div>
                </div>
              </div>
            </div>
          )}

          {section === "notif" && (
            <div id="set-notif" className="set-section active">
              <div className="set-card">
                <div className="card-head"><h3>E-Posta Bildirimleri</h3></div>
                {NOTIF_ROWS.map(([key, title, description], index) => (
                  <div key={key}>
                    {index > 0 && <div className="divider" />}
                    <div className="toggle-row">
                      <div><strong>{title}</strong><p>{description}</p></div>
                      <label className="switch">
                        <input
                          type="checkbox"
                          aria-label={title}
                          checked={key === "weeklyReportEmail" ? false : notifications[key] ?? false}
                          disabled={key === "weeklyReportEmail" || updateNotifications.isPending}
                          onChange={() => toggleNotification(key)}
                        />
                        <span className="slider round" />
                      </label>
                    </div>
                  </div>
                ))}
              </div>
            </div>
          )}

          {section === "security" && (
            <div id="set-security" className="set-section active">
              <div className="set-card">
                <div className="card-head"><h3>Giriş Güvenliği</h3></div>
                {passwordError && <p className="form-error" role="alert">{passwordError}</p>}
                <div className="form-grid-2">
                  <div className="input-group">
                    <label htmlFor="current-password">Mevcut Şifre</label>
                    <input id="current-password" type="password" placeholder="••••••••" className="input-control" value={passwords.current} onChange={(e) => setPasswords((p) => ({ ...p, current: e.target.value }))} />
                  </div>
                </div>
                <div className="form-grid-2 mt-3">
                  <div className="input-group">
                    <label htmlFor="new-password">Yeni Şifre</label>
                    <input id="new-password" type="password" className="input-control" value={passwords.next} onChange={(e) => setPasswords((p) => ({ ...p, next: e.target.value }))} />
                  </div>
                  <div className="input-group">
                    <label htmlFor="new-password-repeat">Yeni Şifre (Tekrar)</label>
                    <input id="new-password-repeat" type="password" className="input-control" value={passwords.repeat} onChange={(e) => setPasswords((p) => ({ ...p, repeat: e.target.value }))} />
                  </div>
                </div>
                <button className="btn btn-secondary mt-4" onClick={submitPassword} disabled={changePassword.isPending}>Şifreyi Güncelle</button>
              </div>

              <div className="set-card mt-4">
                <div className="card-head"><h3>İki Aşamalı Doğrulama</h3></div>
                <div className="toggle-row">
                  <div><strong>SMS ile doğrulama</strong><p>Henüz desteklenmiyor. Bu sürüm SMS koduyla giriş doğrulaması yapmaz.</p></div>
                  <label className="switch">
                    <input
                      type="checkbox"
                      aria-label="SMS ile doğrulama"
                      checked={false}
                      disabled
                    />
                    <span className="slider round" />
                  </label>
                </div>
              </div>
            </div>
          )}

          {section === "billing" && (
            <div id="set-billing" className="set-section active">
              <div className="plan-banner">
                <div className="pb-info">
                  <span className="badge-pro">{(subscription.plan ?? "PRO").toLocaleUpperCase("tr-TR")} PLAN</span>
                  <h3>{subscription.planName}</h3>
                  <p>{subscription.billingCycle} ödeme planı aktif. Bir sonraki yenileme: <strong>{subscription.renewalDate}</strong></p>
                </div>
                <div className="pb-price">₺{(subscription.price ?? 0).toLocaleString("tr-TR")}<small>/yıl</small></div>
              </div>

              <div className="set-card mt-4">
                <div className="card-head"><h3>Ödeme Yöntemi</h3></div>
                <div className="cc-preview">
                  <div className="cc-icon"><i aria-hidden="true" className="fa-brands fa-cc-mastercard" /></div>
                  <span>{subscription.paymentMethodMasked}</span>
                  <button className="btn-text" onClick={() => showToast("Ödeme yöntemi değişikliği demo kapsamı dışındadır.", "info")}>Değiştir</button>
                </div>
              </div>
            </div>
          )}
        </main>
      </div>
    </div>
  );
}
