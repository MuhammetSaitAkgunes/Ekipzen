import { useRef, useState, type FormEvent } from "react";
import { Link } from "react-router-dom";
import { useMutation } from "@tanstack/react-query";
import { ApiError } from "../api/client";
import { Button, Checkbox, Field, Input, Select, Textarea } from "../ui";
import {
  COMPANY_SIZES, serverFieldErrors, submitDemoRequest, validateDemoRequest,
  type DemoRequest, type DemoRequestErrors,
} from "./demoRequest";

const FIELD_ORDER: (keyof DemoRequest)[] = ["fullName", "email", "company", "companySize", "phone", "message", "kvkkConsent"];

export function DemoRequestForm({ initialMessage = "" }: { initialMessage?: string }) {
  const [values, setValues] = useState<DemoRequest>({
    fullName: "", email: "", company: "", companySize: "", phone: "", message: initialMessage, kvkkConsent: false, website: "",
  });
  const [errors, setErrors] = useState<DemoRequestErrors>({});
  const [formError, setFormError] = useState<string | null>(null);
  const formRef = useRef<HTMLFormElement>(null);

  const mutation = useMutation({
    mutationFn: submitDemoRequest,
    onError: (error) => {
      const fieldErrors = serverFieldErrors(error);
      setErrors(fieldErrors);
      if (Object.keys(fieldErrors).length > 0) return;
      setFormError(error instanceof ApiError && error.status === 429
        ? "Çok fazla talep gönderildi. Lütfen bir süre sonra tekrar deneyin."
        : "Talebiniz şu anda iletilemedi. Lütfen tekrar deneyin ya da bize e-posta ile ulaşın.");
    },
  });

  const set = <K extends keyof DemoRequest>(key: K, value: DemoRequest[K]) => {
    setValues((v) => ({ ...v, [key]: value }));
    if (errors[key]) setErrors((e) => ({ ...e, [key]: undefined }));
  };

  const submit = (event: FormEvent) => {
    event.preventDefault();
    setFormError(null);
    const found = validateDemoRequest(values);
    setErrors(found);
    const first = FIELD_ORDER.find((key) => found[key]);
    if (first) {
      formRef.current?.querySelector<HTMLElement>(`[name="${first}"]`)?.focus();
      return;
    }
    mutation.mutate(values);
  };

  if (mutation.isSuccess) {
    return (
      <div className="ez-form-success" role="status">
        <span className="ez-icon"><i aria-hidden="true" className="fa-solid fa-check" /></span>
        <h2>Talebiniz alındı</h2>
        <p>Teşekkür ederiz. Ekibimiz {values.email} adresi üzerinden sizinle iletişime geçecektir.</p>
        <Link to="/ozellikler" className="ez-link">Modülleri inceleyin</Link>
      </div>
    );
  }

  return (
    <form ref={formRef} className="ez-form" onSubmit={submit} noValidate aria-describedby="demo-form-note">
      <p id="demo-form-note" className="ez-form__note">* ile işaretli alanlar zorunludur.</p>
      <div className="ez-form__grid">
        <Field label="Ad soyad" required error={errors.fullName}>
          <Input name="fullName" autoComplete="name" value={values.fullName} onChange={(e) => set("fullName", e.target.value)} />
        </Field>
        <Field label="İş e-postası" required error={errors.email}>
          <Input name="email" type="email" autoComplete="email" inputMode="email" placeholder="ad@sirket.com" value={values.email} onChange={(e) => set("email", e.target.value)} />
        </Field>
        <Field label="Şirket adı" required error={errors.company}>
          <Input name="company" autoComplete="organization" value={values.company} onChange={(e) => set("company", e.target.value)} />
        </Field>
        <Field label="Çalışan sayısı" required error={errors.companySize}>
          <Select name="companySize" value={values.companySize} onChange={(e) => set("companySize", e.target.value)}>
            <option value="">Seçin</option>
            {COMPANY_SIZES.map((size) => <option key={size} value={size}>{size} çalışan</option>)}
          </Select>
        </Field>
        <Field label="Telefon" hint="İsteğe bağlı" error={errors.phone}>
          <Input name="phone" type="tel" autoComplete="tel" inputMode="tel" value={values.phone} onChange={(e) => set("phone", e.target.value)} />
        </Field>
      </div>
      <Field label="Mesajınız" hint="İsteğe bağlı. Görüşmede öncelikli ele almak istediğiniz süreçleri belirtebilirsiniz." error={errors.message}>
        <Textarea name="message" rows={4} value={values.message} onChange={(e) => set("message", e.target.value)} />
      </Field>
      <div className="ez-honeypot" aria-hidden="true">
        <label htmlFor="demo-website">Web siteniz</label>
        <input id="demo-website" name="website" tabIndex={-1} autoComplete="off" value={values.website} onChange={(e) => set("website", e.target.value)} />
      </div>
      <div className={errors.kvkkConsent ? "ez-field ez-field--error" : "ez-field"}>
        <Checkbox
          name="kvkkConsent"
          checked={values.kvkkConsent}
          onChange={(e) => set("kvkkConsent", e.target.checked)}
          aria-invalid={errors.kvkkConsent ? true : undefined}
          aria-describedby={errors.kvkkConsent ? "kvkk-error" : undefined}
        >
          <Link to="/kvkk-aydinlatma" target="_blank" rel="noopener">KVKK aydınlatma metnini</Link> okudum; talebimin yanıtlanması amacıyla kişisel verilerimin işlenmesini kabul ediyorum. *
        </Checkbox>
        {errors.kvkkConsent && <p id="kvkk-error" className="ez-field__error" role="alert">{errors.kvkkConsent}</p>}
      </div>
      {formError && <p className="ez-form__error" role="alert">{formError}</p>}
      <Button type="submit" size="lg" disabled={mutation.isPending}>
        {mutation.isPending ? "Gönderiliyor…" : "Demo talebini gönder"}
      </Button>
    </form>
  );
}
