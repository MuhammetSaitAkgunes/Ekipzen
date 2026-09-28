import { useState } from "react";
import type { PersonnelGuideStep } from "./PersonnelGuide";

type DecisionCheck = {
  question: string;
  choices: readonly [string, string, string];
  correct: number;
  explanation: string;
};

const checks: Record<PersonnelGuideStep, DecisionCheck> = {
  "tab-kimlik": {
    question: "Aynı ad ve soyadla iki çalışan kaydı görüyorsunuz. Hangisini seçersiniz?",
    choices: ["İlk sıradaki kaydı", "Kimlik bilgisi ve iş bilgileri doğrulanan kaydı", "Yeni bir kayıt açarım"],
    correct: 1,
    explanation: "Aynı ad yeterli değildir. Kimliği ve ilgili iş bilgilerini dayanak kayıtla karşılaştırın; yanlış kişiye işlem bağlamayın.",
  },
  "tab-iletisim": {
    question: "Acil durumda aranacak kişinin telefonu henüz doğrulanmadı. Ne yaparsınız?",
    choices: ["Tahmini bir numara girerim", "Başka çalışanın numarasını örnek alırım", "Numarayı doğrulamadan girmem ve eksikliği takip ederim"],
    correct: 2,
    explanation: "Doğrulanmamış numara acil durumda yanlış kişiye ulaşılmasına neden olabilir. Bilgiyi ilgili kişiyle teyit edin.",
  },
  "tab-is": {
    question: "Çalışanın sözleşme türü henüz teyit edilmedi. Hangi yol doğrudur?",
    choices: ["Varsayılan türü seçip devam ederim", "İmzalı iş belgesini yetkili kişiyle doğrularım", "Başka çalışanın türünü kopyalarım"],
    correct: 1,
    explanation: "Sözleşme türü sonraki süreçleri etkiler. Seçimi gerçek iş belgesine dayandırın; bilinmeyen bilgiyi varsaymayın.",
  },
  "tab-mali": {
    question: "Çalışan kartı kaydedildi, fakat ücret paketi bulunmuyor. Ne anlama gelir?",
    choices: ["Bordro hazırlığı tamamdır", "Ücret paketi ve yürürlük bilgisi ayrıca doğrulanmalıdır", "Başka çalışanın ücretini kopyalayabilirim"],
    correct: 1,
    explanation: "Kartın oluşması ücret paketini kaydetmez. Tutarı, yürürlük tarihini ve kaynak referansını ayrı adımda kontrol edin.",
  },
  "tab-ozluk": {
    question: "Çalışma koşuluna uygunluk değerlendirmesi henüz yapılmadı. Nasıl ilerlersiniz?",
    choices: ["Kutunun değerinden uygunluk sonucu çıkarırım", "Değerlendirme sonucunu yetkili uzmanla netleştiririm", "Önceki çalışanın seçimini kullanırım"],
    correct: 1,
    explanation: "Form alanı tek başına uygunluk kararı değildir. Değerlendirilmemiş durumu doğrulanmış sonuç gibi sunmayın.",
  },
  "tab-evrak": {
    question: "Çalışan kartı açıldı, fakat gerekli belge henüz gelmedi. Ne yaparsınız?",
    choices: ["İşe girişi tümüyle tamamlandı sayarım", "Boş bir dosya yüklerim", "Belge eksikliğini takip edip gerçek dosya geldiğinde doğru türe yüklerim"],
    correct: 2,
    explanation: "Kartın varlığı belgeyi veya uygulama dışındaki adımları kanıtlamaz. Eksikliği görünür tutun ve gerçek dosyayı kontrol edin.",
  },
};

export function PersonnelDecisionCheck({ step }: { step: PersonnelGuideStep }) {
  const [selected, setSelected] = useState<number | null>(null);
  const check = checks[step];

  return <details className="ez-personnel-decision-check">
    <summary>Kısa karar alıştırması</summary>
    <fieldset>
      <legend>{check.question}</legend>
      {check.choices.map((choice, index) => <label key={choice}>
        <input type="radio" name={`personnel-check-${step}`} checked={selected === index}
          onChange={() => setSelected(index)} />
        {choice}
      </label>)}
    </fieldset>
    {selected !== null && <p role="status"><strong>{selected === check.correct ? "Doğru karar." : "Bir daha düşünün."}</strong> {check.explanation}</p>}
    <small>Bu alıştırmanın yanıtı kaydedilmez; gerçek işlem kanıtı veya kalıcı öğrenme ilerlemesi değildir.</small>
  </details>;
}
