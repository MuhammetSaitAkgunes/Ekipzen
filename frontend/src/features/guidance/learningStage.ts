export type LearningStage = { key: string; title: string; next: string };
const readingSteps = new Set(["company/access-preparation", "attendance/period-summary", "payroll/payslip-review", "daily/task-review", "leave/review-request"]);
export function learningStage(process: string, step: string, read: boolean, correct: boolean | null, count: number): LearningStage {
  if (readingSteps.has(process + "/" + step)) return {
    key: read ? "reading-recorded" : "reading-start", title: read ? "İnceleme beyanı kayıtlı" : "İncelemeye başlayın",
    next: "Bu adım okuma ve inceleme içindir; yapay işlem kanıtı beklenmez. Gerçek karar gerekiyorsa ilgili işlem adımına geçin.",
  };
  if (correct === true && count > 0) return { key: "practice-and-operation", title: "Alıştırma ve uygulama kanıtı birlikte",
    next: "Güncel sürümde iki ayrı kanıt var. Yeni veya karmaşık durumun koşullarını yine kontrol edin; bağımsız yeterlilik için insan değerlendirmesi gerekir." };
  if (count > 0) return { key: "operation-recorded", title: "Uygulama kaydı var",
    next: "Gerçek işlem kaydı doğruluğu tek başına kanıtlamaz. Bu sürümün alıştırmasını ve karar dayanağını ayrıca inceleyin." };
  if (correct === true) return { key: "practice-correct", title: "Alıştırma yanıtı doğru",
    next: "Alıştırma gerçek işlem yapmaz. Uygun bir gerçek iş oluştuğunda koşulları ve sonucu kontrol ederek ilgili işlemi yapın." };
  if (correct === false) return { key: "practice-review", title: "Alıştırmayı yeniden çalışın",
    next: "Gerekçe, dayanak ve hata düzeltme açıklamasını inceleyip yeni deneme yapın. Deneme sayısı yeterlilik puanı değildir." };
  return { key: read ? "read" : "start", title: read ? "İnceleme aşaması" : "Başlangıç",
    next: "Güncel adımın koşullarını ve dayanağını inceleyin; hazır olduğunuzda alıştırmayı yanıtlayın. Yayın veya ziyaret sayısı kişisel başarı değildir." };
}
