import { useEffect, useRef, useState } from "react";
import { ApiError } from "../../api/client";
import { useToast } from "../../layout/ToastProvider";
import { PageError, PageLoading } from "../shared/PageState";
import { DocumentsTab } from "./DocumentsTab";
import { PersonnelGuide } from "./PersonnelGuide";
import {
  useDepartments,
  useEmployee,
  useEmployeeEmploymentFoundation,
  useRehireEmployee,
  useSaveCompensationPackage,
  useSaveEmployee,
  useUploadPhoto,
  type EmployeeDetailDto,
  type EmployeeUpsertModel,
} from "./queries";

type TabId = "tab-kimlik" | "tab-iletisim" | "tab-is" | "tab-mali" | "tab-ozluk" | "tab-evrak";

const TABS: { id: TabId; icon: string; label: string }[] = [
  { id: "tab-kimlik", icon: "fa-regular fa-id-card", label: "Kimlik Bilgileri" },
  { id: "tab-iletisim", icon: "fa-solid fa-phone", label: "İletişim & Adres" },
  { id: "tab-is", icon: "fa-solid fa-briefcase", label: "İş & Kurumsal" },
  { id: "tab-mali", icon: "fa-solid fa-wallet", label: "Mali Bilgiler" },
  { id: "tab-ozluk", icon: "fa-solid fa-shield-heart", label: "Özlük & Sağlık" },
  { id: "tab-evrak", icon: "fa-solid fa-folder-tree", label: "Evraklar" },
];

type FormState = {
  nationalId: string; birthDate: string; firstName: string; lastName: string;
  gender: string; maritalStatus: string; bloodType: string;
  mobilePhone: string; personalEmail: string; homeAddress: string;
  emergencyContactName: string; emergencyContactPhone: string;
  departmentId: string; title: string; hireDate: string; employmentType: string; contractType: string;
  rehireEligibility: string; exitCode: string;
  iban: string; bankName: string; salaryType: string; pensionStatus: string; mealCard: string;
  tshirtSize: string; pantsSize: string; coatSize: string; shoeSize: string;
  canWorkAtHeight: boolean; canWorkNightShift: boolean; canLiftHeavyLoads: boolean; healthNotes: string;
};

const emptyForm: FormState = {
  nationalId: "", birthDate: "", firstName: "", lastName: "",
  gender: "", maritalStatus: "", bloodType: "",
  mobilePhone: "", personalEmail: "", homeAddress: "",
  emergencyContactName: "", emergencyContactPhone: "",
  departmentId: "", title: "", hireDate: "", employmentType: "", contractType: "",
  rehireEligibility: "Değerlendirilmedi", exitCode: "Yok",
  iban: "", bankName: "", salaryType: "", pensionStatus: "", mealCard: "",
  tshirtSize: "", pantsSize: "", coatSize: "", shoeSize: "",
  canWorkAtHeight: false, canWorkNightShift: false, canLiftHeavyLoads: false, healthNotes: "",
};

const formFrom = (detail: EmployeeDetailDto): FormState => ({
  ...emptyForm,
  nationalId: detail.nationalId ?? "",
  firstName: detail.firstName ?? "",
  lastName: detail.lastName ?? "",
  departmentId: String(detail.departmentId ?? ""),
  title: detail.title ?? "",
  hireDate: detail.hireDate ?? "",
  birthDate: detail.profile?.birthDate ?? "",
  gender: detail.profile?.gender ?? emptyForm.gender,
  maritalStatus: detail.profile?.maritalStatus ?? emptyForm.maritalStatus,
  bloodType: detail.profile?.bloodType ?? emptyForm.bloodType,
  mobilePhone: detail.profile?.mobilePhone ?? "",
  personalEmail: detail.profile?.personalEmail ?? "",
  homeAddress: detail.profile?.homeAddress ?? "",
  emergencyContactName: detail.profile?.emergencyContactName ?? "",
  emergencyContactPhone: detail.profile?.emergencyContactPhone ?? "",
  employmentType: detail.profile?.employmentType ?? emptyForm.employmentType,
  contractType: detail.contractType ?? emptyForm.contractType,
  rehireEligibility: detail.profile?.rehireEligibility ?? emptyForm.rehireEligibility,
  exitCode: detail.profile?.exitCode ?? emptyForm.exitCode,
  iban: detail.profile?.iban ?? "",
  bankName: detail.profile?.bankName ?? "",
  salaryType: detail.profile?.salaryType ?? emptyForm.salaryType,
  pensionStatus: detail.profile?.pensionStatus ?? emptyForm.pensionStatus,
  mealCard: detail.profile?.mealCard ?? "",
  tshirtSize: detail.profile?.tshirtSize ?? emptyForm.tshirtSize,
  pantsSize: detail.profile?.pantsSize ?? emptyForm.pantsSize,
  coatSize: detail.profile?.coatSize ?? emptyForm.coatSize,
  shoeSize: detail.profile?.shoeSize ?? emptyForm.shoeSize,
  canWorkAtHeight: detail.profile?.canWorkAtHeight ?? false,
  canWorkNightShift: detail.profile?.canWorkNightShift ?? false,
  canLiftHeavyLoads: detail.profile?.canLiftHeavyLoads ?? false,
  healthNotes: detail.profile?.healthNotes ?? "",
});

const modelFrom = (form: FormState, existing: EmployeeDetailDto | undefined): EmployeeUpsertModel => ({
  firstName: form.firstName,
  lastName: form.lastName,
  title: form.title,
  departmentId: Number(form.departmentId) || undefined,
  hireDate: form.hireDate || undefined,
  nationalId: form.nationalId,
  managerId: existing?.managerId ?? null,
  status: existing?.status ?? "active",
  contractType: form.contractType,
  profile: {
    birthDate: form.birthDate || null,
    gender: form.gender || null, maritalStatus: form.maritalStatus || null, bloodType: form.bloodType || null,
    mobilePhone: form.mobilePhone || null, personalEmail: form.personalEmail || null,
    homeAddress: form.homeAddress || null,
    emergencyContactName: form.emergencyContactName || null,
    emergencyContactRelation: null,
    emergencyContactPhone: form.emergencyContactPhone || null,
    employmentType: form.employmentType || null,
    rehireEligibility: form.rehireEligibility, exitCode: form.exitCode,
    iban: form.iban || null, bankName: form.bankName || null,
    salaryType: form.salaryType || null, pensionStatus: form.pensionStatus || null,
    mealCard: form.mealCard || null,
    tshirtSize: form.tshirtSize || null, pantsSize: form.pantsSize || null, coatSize: form.coatSize || null,
    shoeSize: form.shoeSize || null,
    canWorkAtHeight: form.canWorkAtHeight, canWorkNightShift: form.canWorkNightShift,
    canLiftHeavyLoads: form.canLiftHeavyLoads,
    healthNotes: form.healthNotes || null,
  },
});

type CompensationForm = {
  effectiveFrom: string;
  monthlyGrossSalary: string;
  regularPremium: string;
  roadAllowance: string;
  mealAllowance: string;
  otherRegularBenefits: string;
  sourceReference: string;
};

const emptyCompensation: CompensationForm = {
  effectiveFrom: "",
  monthlyGrossSalary: "",
  regularPremium: "0",
  roadAllowance: "0",
  mealAllowance: "0",
  otherRegularBenefits: "0",
  sourceReference: "",
};

const tryNumber = (value: string): number => {
  const result = Number(value);
  return Number.isFinite(result) ? result : 0;
};

const currency = new Intl.NumberFormat("tr-TR", { style: "currency", currency: "TRY" });

export function PersonnelModal({ employeeId, readOnly, onClose, embedded = false, onEdit, onCreated, guided = false, initialTab }:
  { employeeId: number | null; readOnly: boolean; onClose: () => void; embedded?: boolean; onEdit?: () => void;
    onCreated?: (id: number) => void; guided?: boolean; initialTab?: TabId }) {
  const { showToast } = useToast();
  const [activeTab, setActiveTab] = useState<TabId>(initialTab ?? "tab-kimlik");
  const [form, setForm] = useState<FormState>(emptyForm);
  const [error, setError] = useState<string | null>(null);
  const [compensation, setCompensation] = useState<CompensationForm>(emptyCompensation);
  const [rehireDate, setRehireDate] = useState("");
  const [rehireContractType, setRehireContractType] = useState("indefinite");
  const photoInputRef = useRef<HTMLInputElement>(null);

  const detailQ = useEmployee(employeeId);
  const departmentsQ = useDepartments();
  const foundationQ = useEmployeeEmploymentFoundation(employeeId, !readOnly);
  const save = useSaveEmployee();
  const rehire = useRehireEmployee();
  const saveCompensation = useSaveCompensationPackage();
  const uploadPhoto = useUploadPhoto();

  useEffect(() => {
    if (detailQ.data) setForm(formFrom(detailQ.data));
  }, [detailQ.data]);

  const set = <K extends keyof FormState>(key: K, value: FormState[K]) =>
    setForm((current) => ({ ...current, [key]: value }));

  const setCompensationField = <K extends keyof CompensationForm>(key: K, value: CompensationForm[K]) =>
    setCompensation((current) => ({ ...current, [key]: value }));

  const isEdit = employeeId !== null;
  const title = isEdit && detailQ.data ? (embedded ? detailQ.data.name : `Personel Kartı — ${detailQ.data.name}`) : (embedded ? "Çalışan ekle" : "Yeni Personel Kartı");
  const description = isEdit
    ? "Kayıtlı özlük bilgilerini görüntüleyin ve güncelleyin."
    : "Gerekli alanları tamamlayarak özlük kaydını oluşturun.";

  const handleSave = async () => {
    setError(null);
    if (!isEdit && (!form.contractType || !form.employmentType)) {
      setActiveTab("tab-is");
      setError(!form.contractType
        ? "Sözleşme türünü imzalı belgeyle doğrulayıp seçin."
        : "Çalışma şeklini doğrulayıp seçin.");
      return;
    }
    try {
      const saved = await save.mutateAsync({ id: employeeId, model: modelFrom(form, detailQ.data) });
      showToast(isEdit ? "Personel kaydı güncellendi." : "Personel kaydı başarıyla oluşturuldu.", "success");
      if (!isEdit && onCreated && typeof saved.id === "number" && saved.id > 0) {
        onCreated(saved.id);
        return;
      }
      onClose();
    } catch (e) {
      setError(e instanceof ApiError ? e.message : "Beklenmeyen bir hata oluştu.");
    }
  };

  const handleCompensationSave = async () => {
    if (employeeId === null) return;
    setError(null);
    if (!compensation.effectiveFrom || tryNumber(compensation.monthlyGrossSalary) <= 0
      || !compensation.sourceReference.trim()) {
      setError("Ücret paketi için başlangıç tarihi, pozitif brüt maaş ve kaynak referansı zorunludur.");
      return;
    }

    try {
      await saveCompensation.mutateAsync({
        employeeId,
        body: {
          effectiveFrom: compensation.effectiveFrom,
          monthlyGrossSalary: tryNumber(compensation.monthlyGrossSalary),
          regularPremium: tryNumber(compensation.regularPremium),
          roadAllowance: tryNumber(compensation.roadAllowance),
          mealAllowance: tryNumber(compensation.mealAllowance),
          otherRegularBenefits: tryNumber(compensation.otherRegularBenefits),
          sourceReference: compensation.sourceReference.trim(),
        },
      });
      setCompensation(emptyCompensation);
      showToast("Yeni ücret paketi yürürlük tarihiyle kaydedildi.", "success");
    } catch (cause) {
      setError(cause instanceof ApiError ? cause.message : "Ücret paketi kaydedilemedi.");
    }
  };

  const handleRehire = async () => {
    if (employeeId === null) return;
    setError(null);
    if (!rehireDate) {
      setError("İşe dönüş tarihi zorunludur.");
      return;
    }

    try {
      await rehire.mutateAsync({
        employeeId,
        body: { rehireDate, contractType: rehireContractType },
      });
      showToast("Yeni çalışma dönemi açıldı; eski izin ve ücret kayıtları taşınmadı.", "success");
      setRehireDate("");
    } catch (cause) {
      setError(cause instanceof ApiError ? cause.message : "İşe dönüş işlemi tamamlanamadı.");
    }
  };

  const handlePhoto = async (file: File | undefined) => {
    if (!file || employeeId === null) return;
    try {
      await uploadPhoto.mutateAsync({ id: employeeId, file });
      showToast("Fotoğraf yüklendi.", "success");
    } catch (e) {
      showToast(e instanceof ApiError ? e.message : "Fotoğraf yüklenemedi.", "error");
    }
  };

  if (isEdit && detailQ.isPending) return <div className={embedded ? "ez-personnel-profile" : "fullscreen-modal"} style={{ display: "flex" }}><PageLoading /></div>;
  if (isEdit && detailQ.isError) return <div className="fullscreen-modal" style={{ display: "flex" }}><PageError error={detailQ.error} /></div>;

  return (
    <div id="personnel-modal" className="fullscreen-modal" style={{ display: "flex" }}>
      <div className="modal-header">
        <div>
          <h2>{title}</h2>
          <p>{description}</p>
          {error && <p className="form-error" role="alert">{error}</p>}
        </div>
        <div className="modal-actions">
          <button className="btn btn-ghost" onClick={onClose}>{readOnly ? "Listeye dön" : "Vazgeç"}</button>
          {readOnly && onEdit && <button className="btn btn-primary" onClick={onEdit}>Profili düzenle</button>}
          {!readOnly && (
            <button className="btn btn-primary" onClick={handleSave} disabled={save.isPending}>
              <i aria-hidden="true" className="fa-solid fa-check" /> Kaydet
            </button>
          )}
        </div>
      </div>

      <div className="modal-body">
        <aside className="modal-sidebar" aria-label="Profil bölümleri">
          {TABS.map((tab) => (
            <button key={tab.id} aria-pressed={activeTab === tab.id} className={`nav-btn ${activeTab === tab.id ? "active" : ""}`} onClick={() => setActiveTab(tab.id)}>
              <i aria-hidden="true" className={tab.icon} /> {tab.label}
            </button>
          ))}
        </aside>

        <section className="modal-content-area">
          {!readOnly && (guided || !isEdit) && <PersonnelGuide step={activeTab} index={TABS.findIndex((tab) => tab.id === activeTab) + 1} />}
          <div id="tab-kimlik" className={`content-section ${activeTab === "tab-kimlik" ? "active" : ""}`}>
            <div className="section-head">
              <div>
                <h3>Kimlik & Kişisel Bilgiler</h3>
                <span>Nüfus bilgilerini resmi evraklarla uyumlu girin.</span>
              </div>
            </div>
            <div className="form-grid">
              <div className="photo-upload col-12">
                <div className="photo-preview"><i aria-hidden="true" className="fa-solid fa-user" /></div>
                <div>
                  {!readOnly && <span
                    className="upload-btn"
                    role="button"
                    tabIndex={0}
                    onClick={() => {
                      if (readOnly) return;
                      if (employeeId === null) { showToast("Fotoğraf için önce kaydı oluşturun.", "info"); return; }
                      photoInputRef.current?.click();
                    }}
                    onKeyDown={(e) => {
                      if (e.key === "Enter" || e.key === " ") e.currentTarget.click();
                    }}
                  >
                    Fotoğraf Yükle
                  </span>}
                  {!readOnly && <small>JPG/PNG, maksimum 2 MB</small>}
                  <input ref={photoInputRef} type="file" accept="image/jpeg,image/png" hidden onChange={(e) => handlePhoto(e.target.files?.[0])} />
                </div>
              </div>
              <div className="input-group col-6">
                <label className="input-label" htmlFor="pm-tc">TC Kimlik No *</label>
                <input id="pm-tc" type="text" className="input-control" maxLength={11} placeholder="11 haneli numara" value={form.nationalId} disabled={readOnly} onChange={(e) => set("nationalId", e.target.value)} />
              </div>
              <div className="input-group col-6">
                <label className="input-label" htmlFor="pm-birth">Doğum Tarihi</label>
                <input id="pm-birth" type="date" className="input-control" value={form.birthDate} disabled={readOnly} onChange={(e) => set("birthDate", e.target.value)} />
              </div>
              <div className="input-group col-6">
                <label className="input-label" htmlFor="pm-first">Adı</label>
                <input id="pm-first" type="text" className="input-control" value={form.firstName} disabled={readOnly} onChange={(e) => set("firstName", e.target.value)} />
              </div>
              <div className="input-group col-6">
                <label className="input-label" htmlFor="pm-last">Soyadı</label>
                <input id="pm-last" type="text" className="input-control" value={form.lastName} disabled={readOnly} onChange={(e) => set("lastName", e.target.value)} />
              </div>
              <div className="input-group col-4">
                <label className="input-label" htmlFor="pm-gender">Cinsiyet</label>
                <select id="pm-gender" className="input-control" value={form.gender} disabled={readOnly} onChange={(e) => set("gender", e.target.value)}>
                  <option value="">Belirtilmedi</option><option>Erkek</option><option>Kadın</option>
                </select>
              </div>
              <div className="input-group col-4">
                <label className="input-label" htmlFor="pm-marital">Medeni Durum</label>
                <select id="pm-marital" className="input-control" value={form.maritalStatus} disabled={readOnly} onChange={(e) => set("maritalStatus", e.target.value)}>
                  <option value="">Belirtilmedi</option><option>Evli</option><option>Bekar</option>
                </select>
              </div>
              <div className="input-group col-4">
                <label className="input-label" htmlFor="pm-blood">Kan Grubu</label>
                <select id="pm-blood" className="input-control" value={form.bloodType} disabled={readOnly} onChange={(e) => set("bloodType", e.target.value)}>
                  <option value="">Belirtilmedi</option><option>0 Rh+</option><option>A Rh+</option><option>B Rh+</option>
                </select>
              </div>
            </div>
          </div>

          <div id="tab-iletisim" className={`content-section ${activeTab === "tab-iletisim" ? "active" : ""}`}>
            <div className="section-head"><div><h3>İletişim Bilgileri</h3><span>Personelin ulaşılabilir iletişim kanalları.</span></div></div>
            <div className="form-grid">
              <div className="input-group col-6"><label className="input-label" htmlFor="pm-phone">Cep Telefonu</label><input id="pm-phone" type="tel" className="input-control" placeholder="(5XX) ..." value={form.mobilePhone} disabled={readOnly} onChange={(e) => set("mobilePhone", e.target.value)} /></div>
              <div className="input-group col-6"><label className="input-label" htmlFor="pm-email">Kişisel E-Posta</label><input id="pm-email" type="email" className="input-control" value={form.personalEmail} disabled={readOnly} onChange={(e) => set("personalEmail", e.target.value)} /></div>
              <div className="input-group col-12"><label className="input-label" htmlFor="pm-address">Ev Adresi</label><textarea id="pm-address" className="input-control" rows={3} value={form.homeAddress} disabled={readOnly} onChange={(e) => set("homeAddress", e.target.value)} /></div>
              <div className="input-group col-6"><label className="input-label" htmlFor="pm-emg-name">Acil Durum Kişisi</label><input id="pm-emg-name" type="text" className="input-control" placeholder="Ad Soyad" value={form.emergencyContactName} disabled={readOnly} onChange={(e) => set("emergencyContactName", e.target.value)} /></div>
              <div className="input-group col-6"><label className="input-label" htmlFor="pm-emg-phone">Yakınlık / Telefon</label><input id="pm-emg-phone" type="text" className="input-control" placeholder="Örn: Eşi - 0532..." value={form.emergencyContactPhone} disabled={readOnly} onChange={(e) => set("emergencyContactPhone", e.target.value)} /></div>
            </div>
          </div>

          <div id="tab-is" className={`content-section ${activeTab === "tab-is" ? "active" : ""}`}>
            <div className="section-head"><div><h3>Kurumsal Bilgiler</h3><span>Pozisyon, çalışma şekli ve organizasyon bilgileri.</span></div></div>
            <div className="form-grid">
              <div className="input-group col-6">
                <label className="input-label" htmlFor="pm-dept">Departman</label>
                <select id="pm-dept" className="input-control" value={form.departmentId} disabled={readOnly} onChange={(e) => set("departmentId", e.target.value)}>
                  <option value="">Seçin</option>
                  {(departmentsQ.data ?? []).map((dept) => <option key={dept.id} value={dept.id}>{dept.name}</option>)}
                </select>
              </div>
              <div className="input-group col-6"><label className="input-label" htmlFor="pm-title">Ünvan / Görev</label><input id="pm-title" type="text" className="input-control" value={form.title} disabled={readOnly} onChange={(e) => set("title", e.target.value)} /></div>
              <div className="input-group col-6"><label className="input-label" htmlFor="pm-hire">İşe Giriş Tarihi</label><input id="pm-hire" type="date" className="input-control" value={form.hireDate} disabled={readOnly} onChange={(e) => set("hireDate", e.target.value)} /></div>
              <div className="input-group col-6">
                <label className="input-label" htmlFor="pm-contract">Sözleşme Türü</label>
                <select id="pm-contract" className="input-control" value={form.contractType} disabled={readOnly} onChange={(e) => set("contractType", e.target.value)}>
                  <option value="">Sözleşmeden seçin</option>
                  <option value="indefinite">Belirsiz süreli</option>
                  <option value="fixed-term">Belirli süreli</option>
                </select>
              </div>
              <div className="input-group col-6">
                <label className="input-label" htmlFor="pm-employment">Çalışma Şekli</label>
                <select id="pm-employment" className="input-control" value={form.employmentType} disabled={readOnly} onChange={(e) => set("employmentType", e.target.value)}>
                  <option value="">Belirtilmedi</option><option>Tam Zamanlı</option><option>Yarı Zamanlı</option><option>Uzaktan</option>
                </select>
              </div>
              <div className="notice-card col-12">
                <strong>Önceki çalışma geçmişi</strong>
                <p>Yeniden işe alım kararları için değerlendirme notu bırakın.</p>
                <div className="form-grid-2">
                  <div className="input-group">
                    <label className="input-label" htmlFor="pm-rehire">Tekrar Çalışma Durumu</label>
                    <select id="pm-rehire" className="input-control" value={form.rehireEligibility} disabled={readOnly} onChange={(e) => set("rehireEligibility", e.target.value)}>
                      <option>Değerlendirilmedi</option><option>Çalışılabilir</option><option>Kararsız</option><option>Çalışılmaz</option>
                    </select>
                  </div>
                  <div className="input-group">
                    <label className="input-label" htmlFor="pm-exit">Eski Çıkış Kodu</label>
                    <select id="pm-exit" className="input-control" value={form.exitCode} disabled={readOnly} onChange={(e) => set("exitCode", e.target.value)}>
                      <option>Yok</option><option>Kod-03 (İstifa)</option>
                    </select>
                  </div>
                </div>
              </div>
              {isEdit && !readOnly && detailQ.data?.status === "passive" && (
                <div className="notice-card col-12">
                  <strong>Yeni çalışma dönemi aç</strong>
                  <p>İşe dönüş, önceki dönemin izin bakiyesini ve ücret paketini otomatik taşımaz.</p>
                  <div className="form-grid-2">
                    <div className="input-group">
                      <label className="input-label" htmlFor="pm-rehire-date">İşe dönüş tarihi</label>
                      <input id="pm-rehire-date" type="date" className="input-control" value={rehireDate}
                        onChange={(event) => setRehireDate(event.target.value)} />
                    </div>
                    <div className="input-group">
                      <label className="input-label" htmlFor="pm-rehire-contract">Yeni sözleşme türü</label>
                      <select id="pm-rehire-contract" className="input-control" value={rehireContractType}
                        onChange={(event) => setRehireContractType(event.target.value)}>
                        <option value="indefinite">Belirsiz süreli</option>
                        <option value="fixed-term">Belirli süreli</option>
                      </select>
                    </div>
                  </div>
                  <button type="button" className="btn btn-secondary" onClick={handleRehire} disabled={rehire.isPending}>
                    Yeni çalışma dönemini başlat
                  </button>
                </div>
              )}
            </div>
          </div>

          <div id="tab-mali" className={`content-section ${activeTab === "tab-mali" ? "active" : ""}`}>
            <div className="section-head"><div><h3>Mali & Yan Haklar</h3><span>Banka, BES ve yan hak tanımlamaları.</span></div></div>
            <div className="form-grid">
              <div className="input-group col-12"><label className="input-label" htmlFor="pm-iban">IBAN Numarası</label><input id="pm-iban" type="text" className="input-control mono" placeholder="TR..." value={form.iban} disabled={readOnly} onChange={(e) => set("iban", e.target.value)} /></div>
              <div className="input-group col-6"><label className="input-label" htmlFor="pm-bank">Banka Adı</label><input id="pm-bank" type="text" className="input-control" value={form.bankName} disabled={readOnly} onChange={(e) => set("bankName", e.target.value)} /></div>
              <div className="input-group col-6">
                <label className="input-label" htmlFor="pm-salary">Maaş Tipi</label>
                <select id="pm-salary" className="input-control" value={form.salaryType} disabled={readOnly} onChange={(e) => set("salaryType", e.target.value)}>
                  <option value="">Belirtilmedi</option><option>Net Maaş</option><option>Brüt Maaş</option>
                </select>
              </div>
              <div className="input-group col-6">
                <label className="input-label" htmlFor="pm-pension">BES Durumu</label>
                <select id="pm-pension" className="input-control" value={form.pensionStatus} disabled={readOnly} onChange={(e) => set("pensionStatus", e.target.value)}>
                  <option value="">Belirtilmedi</option><option>Otomatik Katılım</option><option>İptal</option><option>Muaf</option>
                </select>
              </div>
              <div className="input-group col-6"><label className="input-label" htmlFor="pm-meal">Yemek Kartı</label><input id="pm-meal" type="text" className="input-control" value={form.mealCard} disabled={readOnly} onChange={(e) => set("mealCard", e.target.value)} /></div>
              {isEdit && !readOnly && detailQ.data?.status === "active" && (
                <div className="notice-card col-12">
                  <strong>Yürürlük tarihli ücret paketleri</strong>
                  <p>
                    Paket bordro dönemine başlangıç girdisi sağlar; bordro satırı yine İK kontrolü
                    ve dönem onayından geçer. Geçmiş kayıtlar değiştirilmez.
                  </p>
                  {foundationQ.isPending && <p>Ücret geçmişi yükleniyor…</p>}
                  {foundationQ.isError && <p className="form-error">Ücret geçmişi yüklenemedi.</p>}
                  {(foundationQ.data?.compensationPackages ?? []).map((item) => (
                    <p key={item.id}>
                      <strong>{item.effectiveFrom}</strong>
                      {item.effectiveTo ? ` – ${item.effectiveTo}` : " – güncel"}
                      {` · ${currency.format(item.monthlyRecurringTotal ?? 0)} · ${item.sourceReference ?? "Referans yok"}`}
                    </p>
                  ))}
                  {foundationQ.data?.compensationPackages?.length === 0 && <p>Henüz ücret paketi yok.</p>}
                  <div className="form-grid-2">
                    <div className="input-group">
                      <label className="input-label" htmlFor="pm-comp-effective">Yürürlük başlangıcı</label>
                      <input id="pm-comp-effective" type="date" className="input-control" value={compensation.effectiveFrom}
                        onChange={(e) => setCompensationField("effectiveFrom", e.target.value)} />
                    </div>
                    <div className="input-group">
                      <label className="input-label" htmlFor="pm-comp-gross">Aylık brüt maaş</label>
                      <input id="pm-comp-gross" type="number" min="0" step="0.01" className="input-control" value={compensation.monthlyGrossSalary}
                        onChange={(e) => setCompensationField("monthlyGrossSalary", e.target.value)} />
                    </div>
                    <div className="input-group">
                      <label className="input-label" htmlFor="pm-comp-premium">Düzenli prim</label>
                      <input id="pm-comp-premium" type="number" min="0" step="0.01" className="input-control" value={compensation.regularPremium}
                        onChange={(e) => setCompensationField("regularPremium", e.target.value)} />
                    </div>
                    <div className="input-group">
                      <label className="input-label" htmlFor="pm-comp-road">Yol yardımı</label>
                      <input id="pm-comp-road" type="number" min="0" step="0.01" className="input-control" value={compensation.roadAllowance}
                        onChange={(e) => setCompensationField("roadAllowance", e.target.value)} />
                    </div>
                    <div className="input-group">
                      <label className="input-label" htmlFor="pm-comp-meal">Yemek yardımı</label>
                      <input id="pm-comp-meal" type="number" min="0" step="0.01" className="input-control" value={compensation.mealAllowance}
                        onChange={(e) => setCompensationField("mealAllowance", e.target.value)} />
                    </div>
                    <div className="input-group">
                      <label className="input-label" htmlFor="pm-comp-other">Diğer düzenli yan hak</label>
                      <input id="pm-comp-other" type="number" min="0" step="0.01" className="input-control" value={compensation.otherRegularBenefits}
                        onChange={(e) => setCompensationField("otherRegularBenefits", e.target.value)} />
                    </div>
                    <div className="input-group form-span-2">
                      <label className="input-label" htmlFor="pm-comp-source">Kaynak / karar referansı</label>
                      <input id="pm-comp-source" className="input-control" value={compensation.sourceReference}
                        onChange={(e) => setCompensationField("sourceReference", e.target.value)} placeholder="Ücret yazısı, kurul kararı veya belge no" />
                    </div>
                  </div>
                  <button type="button" className="btn btn-secondary" onClick={handleCompensationSave} disabled={saveCompensation.isPending}>
                    Yeni ücret paketini kaydet
                  </button>
                </div>
              )}
            </div>
          </div>

          <div id="tab-ozluk" className={`content-section ${activeTab === "tab-ozluk" ? "active" : ""}`}>
            <div className="section-head"><div><h3>Özlük & Sağlık</h3><span>Zimmet ve İSG süreçleri için ek bilgiler.</span></div></div>
            <div className="form-grid">
              <div className="input-group col-3">
                <label className="input-label" htmlFor="pm-tshirt">T-Shirt</label>
                <select id="pm-tshirt" className="input-control" value={form.tshirtSize} disabled={readOnly} onChange={(e) => set("tshirtSize", e.target.value)}>
                  <option value="">Belirtilmedi</option><option>S</option><option>M</option><option>L</option><option>XL</option>
                </select>
              </div>
              <div className="input-group col-3">
                <label className="input-label" htmlFor="pm-pants">Pantolon</label>
                <select id="pm-pants" className="input-control" value={form.pantsSize} disabled={readOnly} onChange={(e) => set("pantsSize", e.target.value)}>
                  <option value="">Belirtilmedi</option><option>30</option><option>32</option><option>34</option>
                </select>
              </div>
              <div className="input-group col-3">
                <label className="input-label" htmlFor="pm-coat">Mont</label>
                <select id="pm-coat" className="input-control" value={form.coatSize} disabled={readOnly} onChange={(e) => set("coatSize", e.target.value)}>
                  <option value="">Belirtilmedi</option><option>M</option><option>L</option>
                </select>
              </div>
              <div className="input-group col-3"><label className="input-label" htmlFor="pm-shoe">Ayakkabı</label><input id="pm-shoe" type="number" className="input-control" value={form.shoeSize} disabled={readOnly} onChange={(e) => set("shoeSize", e.target.value)} /></div>
              <label className="check-card col-4"><input type="checkbox" checked={form.canWorkAtHeight} disabled={readOnly} onChange={(e) => set("canWorkAtHeight", e.target.checked)} /> <span>Yüksekte çalışabilir</span></label>
              <label className="check-card col-4"><input type="checkbox" checked={form.canWorkNightShift} disabled={readOnly} onChange={(e) => set("canWorkNightShift", e.target.checked)} /> <span>Gece vardiyası</span></label>
              <label className="check-card col-4"><input type="checkbox" checked={form.canLiftHeavyLoads} disabled={readOnly} onChange={(e) => set("canLiftHeavyLoads", e.target.checked)} /> <span>Ağır yük taşıma</span></label>
              <div className="input-group col-12"><label className="input-label" htmlFor="pm-health">Bilinen Hastalık / Notlar</label><textarea id="pm-health" className="input-control" rows={2} value={form.healthNotes} disabled={readOnly} onChange={(e) => set("healthNotes", e.target.value)} /></div>
            </div>
          </div>

          <div id="tab-evrak" className={`content-section ${activeTab === "tab-evrak" ? "active" : ""}`}>
            <DocumentsTab employeeId={employeeId} readOnly={readOnly} />
          </div>
        </section>
      </div>
    </div>
  );
}
