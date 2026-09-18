import { useState, type FormEvent } from "react";
import { Link, useNavigate } from "react-router-dom";
import { ApiError } from "../api/client";
import { useAuth } from "./AuthContext";
import { roleHomeFor } from "../paths";
import { Badge, Button, Field, Input, usePageMeta } from "../ui";
import { AuthLayout } from "./AuthLayout";

export function LoginPage() {
  usePageMeta("Giriş yap", "Ekipzen hesabınıza giriş yapın.");
  const { login } = useAuth();
  const navigate = useNavigate();
  const [email, setEmail] = useState(import.meta.env.DEV ? "ik@hrmaster.local" : "");
  const [password, setPassword] = useState(import.meta.env.DEV ? "demo123" : "");
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    setError(null);
    setBusy(true);
    try {
      const user = await login(email, password);
      navigate(roleHomeFor(user?.role));
    } catch (e) {
      setError(e instanceof ApiError ? e.message : "Beklenmeyen bir hata oluştu.");
    } finally {
      setBusy(false);
    }
  };

  return (
    <AuthLayout
      title="Hesabınıza giriş yapın"
      subtitle={import.meta.env.DEV ? "Demo hesabıyla uygulamayı inceleyebilirsiniz." : "Kurumsal e-posta adresiniz ve şifrenizle devam edin."}
      badge={<Badge tone="primary">{import.meta.env.DEV ? "Demo erişim" : "Şirket hesabı"}</Badge>}
    >
      {/* Bu ekranda KAYIT YOKTUR — ne kullanıcı ne şirket. Hesap açmanın tek yolu
          operatörün platform anahtarıyla kiracı provizyonlaması, ardından gelen
          davet e-postasıdır (/davet). Kiracı başına ayrı veritabanında "kaydol" bir
          VERİTABANI oluşturur; anonim bir düğmenin bunu tetiklemesi kaynak tüketme açığıdır. */}
      <form id="auth-login" className="ez-auth__form" onSubmit={submit}>
        <Field label="E-posta" id="login-email">
          <Input type="email" autoComplete="username" required value={email} onChange={(e) => setEmail(e.target.value)} />
        </Field>
        <Field label="Şifre" id="login-password">
          <Input type="password" autoComplete="current-password" required value={password} onChange={(e) => setPassword(e.target.value)} />
        </Field>
        {error && <p className="ez-auth__error" role="alert">{error}</p>}
        <Button type="submit" size="lg" block disabled={busy}>
          {busy ? "Giriş yapılıyor…" : "Giriş yap"}
        </Button>
      </form>
      <p className="ez-auth__alt">
        Şirketinizin henüz bir hesabı yoksa <Link to="/iletisim">demo talep edin</Link>.
        <br />
        Hesabınız oluşturulduysa davet e-postanızdaki bağlantıyla şifrenizi belirleyebilirsiniz.
      </p>
    </AuthLayout>
  );
}
