import { useState, type FormEvent } from "react";
import { useNavigate, useSearchParams } from "react-router-dom";
import { apiFetch, ApiError } from "../api/client";
import { useAuth } from "./AuthContext";
import { roleHomeFor } from "../paths";
import { Badge, Button, Field, Input, usePageMeta } from "../ui";
import { AuthLayout } from "./AuthLayout";

/**
 * Davet bağlantısı hedefi (/davet?email=...&token=...). Provizyonlanan
 * hr-admin ve işe alınan personel şifresiz oluşturulur; bu ekran davet token'ıyla
 * ilk şifreyi belirler, ardından otomatik giriş yapıp rol ana sayfasına götürür.
 */
export function AcceptInvitePage() {
  usePageMeta("Hesap etkinleştirme");
  const [params] = useSearchParams();
  const { login } = useAuth();
  const navigate = useNavigate();

  const email = params.get("email") ?? "";
  const token = params.get("token") ?? "";

  const [password, setPassword] = useState("");
  const [confirm, setConfirm] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const missingLink = !email || !token;
  const minimumPasswordLength = import.meta.env.DEV ? 6 : 12;

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    setError(null);
    if (password.length < minimumPasswordLength) {
      setError(`Şifre en az ${minimumPasswordLength} karakter olmalı.`);
      return;
    }
    if (password !== confirm) {
      setError("Şifreler eşleşmiyor.");
      return;
    }
    setBusy(true);
    try {
      await apiFetch("/auth/accept-invite", {
        method: "POST",
        body: JSON.stringify({ email, token, newPassword: password }),
      });
      const user = await login(email, password);
      navigate(roleHomeFor(user?.role));
    } catch (e) {
      setError(e instanceof ApiError ? e.message : "Davet kabul edilemedi. Bağlantının süresi geçmiş olabilir.");
    } finally {
      setBusy(false);
    }
  };

  return (
    <AuthLayout
      title="Hesabınızı etkinleştirin"
      subtitle={missingLink ? "Davet bağlantınızı kontrol edin." : <>{email} için bir şifre belirleyin.</>}
      badge={<Badge tone="primary">Hesap etkinleştirme</Badge>}
    >
      <form className="ez-auth__form" onSubmit={submit}>
        {missingLink ? (
          <p className="ez-auth__error" role="alert">
            Davet bağlantısı geçersiz veya eksik. Lütfen e-postanızdaki bağlantıyı yeniden açın.
          </p>
        ) : (
          <>
            {!import.meta.env.DEV && <p className="ez-auth__hint">En az 12 karakter, büyük harf ve rakam kullanın.</p>}
            <Field label="Yeni şifre" id="invite-password">
              <Input type="password" autoComplete="new-password" value={password} onChange={(e) => setPassword(e.target.value)} />
            </Field>
            <Field label="Şifre (tekrar)" id="invite-confirm">
              <Input type="password" autoComplete="new-password" value={confirm} onChange={(e) => setConfirm(e.target.value)} />
            </Field>
            {error && <p className="ez-auth__error" role="alert">{error}</p>}
            <Button type="submit" size="lg" block disabled={busy}>
              {busy ? "Etkinleştiriliyor…" : "Şifreyi belirle ve giriş yap"}
            </Button>
          </>
        )}
      </form>
    </AuthLayout>
  );
}
