import type { Role } from "../../routes";

export const guidanceStepTitles: Record<string, Record<string, string>> = {
  "personnel-entry": { "tab-kimlik": "Kimlik ve temel bilgiler", "tab-iletisim": "İletişim",
    "tab-is": "İş ve kurumsal bilgiler", "tab-mali": "Mali bilgiler", "tab-ozluk": "Özlük bilgileri", "tab-evrak": "Belgeler" },
  recruitment: { "candidate-create": "Adayı oluşturma", "first-review": "Adayın ilk incelemesi" },
  leave: { "review-request": "İzin talebini inceleme", "decision-effect": "İzin kararının etkisi" },
  company: { "initial-setup": "Başlangıç kayıtlarını inceleme", "company-profile": "Şirket profilini doğrulama",
    departments: "Departman hazırlığı", "access-preparation": "Erişim sorumlularını belirleme" },
  personnel: { "record-review": "Doğru çalışanı bulma ve bilgiyi kontrol etme", "import-review": "İçe aktarma önizlemesi ve sonuç kontrolü", rehire: "İşe dönüşü ayrı dönem olarak hazırlama" },
  attendance: { "entry-review": "Puantaj girişini önizleme", "entry-correction": "Mevcut günü düzeltme", "period-summary": "Aylık özet ve bordro etkisini kontrol etme" },
};
const managerSteps: Record<string, string[]> = {
  leave: ["review-request", "decision-effect"], personnel: ["record-review"],
  attendance: ["entry-review", "entry-correction", "period-summary"],
};
export function guidanceStepAudience(process: string, step: string): Role[] {
  return managerSteps[process]?.includes(step) ? ["hr-admin", "manager"] : ["hr-admin"];
}
