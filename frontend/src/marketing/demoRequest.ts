import { ApiError, apiFetch } from "../api/client";

export type DemoRequest = {
  fullName: string;
  email: string;
  company: string;
  companySize: string;
  phone: string;
  message: string;
  kvkkConsent: boolean;
  /** Bal küpü: görünmez alan; bot doldurursa sunucu talebi sessizce yok sayar. */
  website: string;
};

export type DemoRequestErrors = Partial<Record<keyof DemoRequest, string>>;

// Sunucudaki SubmitDemoRequestCommandValidator.CompanySizes ile aynı.
export const COMPANY_SIZES = ["1-10", "11-50", "51-200", "201-500", "500+"] as const;

const EMAIL = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

export function validateDemoRequest(r: DemoRequest): DemoRequestErrors {
  const errors: DemoRequestErrors = {};
  if (!r.fullName.trim()) errors.fullName = "Ad soyad zorunludur.";
  else if (r.fullName.length > 100) errors.fullName = "Ad soyad en fazla 100 karakter olabilir.";
  if (!r.email.trim()) errors.email = "E-posta zorunludur.";
  else if (!EMAIL.test(r.email.trim())) errors.email = "Geçerli bir e-posta adresi girin.";
  if (!r.company.trim()) errors.company = "Şirket adı zorunludur.";
  else if (r.company.length > 150) errors.company = "Şirket adı en fazla 150 karakter olabilir.";
  if (!(COMPANY_SIZES as readonly string[]).includes(r.companySize)) errors.companySize = "Geçerli bir çalışan aralığı seçin.";
  if (r.phone.length > 30) errors.phone = "Telefon en fazla 30 karakter olabilir.";
  if (r.message.length > 2000) errors.message = "Mesaj en fazla 2000 karakter olabilir.";
  if (!r.kvkkConsent) errors.kvkkConsent = "KVKK aydınlatma metnini onaylamanız gerekir.";
  return errors;
}

export async function submitDemoRequest(r: DemoRequest): Promise<void> {
  await apiFetch<void>("/public/demo-requests", {
    method: "POST",
    body: JSON.stringify({
      ...r,
      fullName: r.fullName.trim(),
      email: r.email.trim(),
      company: r.company.trim(),
      phone: r.phone.trim() || null,
      message: r.message.trim() || null,
    }),
  });
}

// Formda gerçekten alanı olan anahtarlar. Sunucu "$.fullName" (JSON ayrıştırma hatası) ya
// da "command" (model düzeyi hata) gibi forma eşlenmeyen anahtarlar döndürebilir; bunlar
// hiçbir alanda gösterilemeyeceği için filtrelenir, aksi halde form sessizce hiçbir şey
// göstermez (bkz. final-fix-findings M11).
const KNOWN_FIELDS = new Set<string>(["fullName", "email", "company", "companySize", "phone", "message", "kvkkConsent", "website"]);

/** ValidationProblemDetails anahtarları PascalCase gelir (FullName) → form alanına çevrilir. */
export function serverFieldErrors(error: unknown): DemoRequestErrors {
  if (!(error instanceof ApiError) || !error.problem?.errors) return {};
  const result: DemoRequestErrors = {};
  for (const [key, messages] of Object.entries(error.problem.errors)) {
    const field = key.charAt(0).toLowerCase() + key.slice(1);
    if (!KNOWN_FIELDS.has(field)) continue;
    if (messages[0]) result[field as keyof DemoRequest] = messages[0];
  }
  return result;
}
