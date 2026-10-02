import type { Role } from "../../routes";

export const guidanceStepTitles: Record<string, Record<string, string>> = {
  "personnel-entry": { "tab-kimlik": "Kimlik ve temel bilgiler", "tab-iletisim": "İletişim",
    "tab-is": "İş ve kurumsal bilgiler", "tab-mali": "Mali bilgiler", "tab-ozluk": "Özlük bilgileri", "tab-evrak": "Belgeler" },
  recruitment: { "candidate-create": "Adayı oluşturma", "first-review": "Aday incelemesi ve aşama kararı", "hire-preparation": "İşe alma ve giriş hesabını izleme" },
  leave: { "review-request": "İzin talebini inceleme", "decision-effect": "İzin kararının etkisi" },
  company: { "initial-setup": "Başlangıç kayıtlarını inceleme", "company-profile": "Şirket profilini doğrulama",
    departments: "Departman hazırlığı", "access-preparation": "Erişim sorumlularını belirleme" },
  personnel: { "record-review": "Doğru çalışanı bulma ve bilgiyi kontrol etme", "import-review": "İçe aktarma önizlemesi ve sonuç kontrolü", rehire: "İşe dönüşü ayrı dönem olarak hazırlama" },
  attendance: { "entry-review": "Puantaj girişini önizleme", "entry-correction": "Mevcut günü düzeltme", "period-summary": "Aylık özet ve bordro etkisini kontrol etme" },
  payroll: { "settings-review": "Hesap ayarlarının kaynak ve sürümünü inceleme", "period-create": "Doğru bordro dönemini açma",
    "row-review": "Çalışan girdilerini ve hesap sonucunu kontrol etme", approval: "Satır ve dönem onayını ayırma",
    "payslip-review": "Ücret pusulasını ve geçerli sürümü kontrol etme", "correction-external": "Onaylı hata ve dış işlem farkını izleme" },
  compliance: { "record-review": "Gerçek belgeyi ve takip kaydını inceleme", "status-review": "Doğrulanmış belge durumunu işleme",
    "record-correction": "Belge bilgisi ve sorumlu hatasını düzeltme" },
  offboarding: { preparation: "Ayrılış dosyasını hazırlama", "settlement-review": "Parasal taslağı ve dayanaklarını inceleme",
    completion: "Ayrılışı tamamlayıp dış işlemi ayırma", "reversal-review": "Geri alma kapsamını kontrol etme",
    "correction-external": "Parasal düzeltme ve ayrı dış kanıtları izleme" },
  daily: { "task-review": "İşin nedenini, kapsamını ve tarihini inceleme", "task-action": "Gerçek kayıtta işlemi yapıp sonucunu doğrulama",
    "external-followup": "Ödeme ve bildirim kanıtlarını ayrı takip etme" },
};
const managerSteps: Record<string, string[]> = {
  leave: ["review-request", "decision-effect"], personnel: ["record-review"],
  attendance: ["entry-review", "entry-correction", "period-summary"],
  compliance: ["record-review"],
  daily: ["task-review", "task-action"],
};
export function guidanceStepAudience(process: string, step: string): Role[] {
  return managerSteps[process]?.includes(step) ? ["hr-admin", "manager"] : ["hr-admin"];
}
