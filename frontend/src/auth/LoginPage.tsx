import { useState, type FormEvent } from "react";
import { useNavigate } from "react-router-dom";
import { ApiError } from "../api/client";
import { useAuth } from "./AuthContext";
import { roleHomeFor } from "../routes";

export function LoginPage() {
  const { login } = useAuth();
  const navigate = useNavigate();
  const [email, setEmail] = useState(import.meta.env.DEV ? "ik@hrmaster.local" : "");
  const [password, setPassword] = useState(import.meta.env.DEV ? "demo123" : "");
  const [error, setError] = useState<string | null>(null);

  const run = async (event: FormEvent, action: () => Promise<unknown>) => {
    event.preventDefault();
    setError(null);
    try {
      const user = (await action()) as { role?: string };
      navigate(roleHomeFor(user?.role));
    } catch (e) {
      setError(e instanceof ApiError ? e.message : "Beklenmeyen bir hata oluştu.");
    }
  };

  return (
    <main className="auth-shell">
      <section className="auth-visual">
        <div className="auth-brand">
          <div className="brand-mark"><i aria-hidden="true" className="fa-solid fa-users-gear" /></div>
          <div>
            <strong>İK Pro</strong>
            <span>HR MASTER Suite</span>
          </div>
        </div>
        <div className="auth-copy">
          <span className="status-pill info">{import.meta.env.DEV ? "Demo erişim" : "Şirket hesabı"}</span>
          <h1>Risk, bordro ve İK operasyonlarını tek merkezden yönetin.</h1>
          <p>Şirketinizin size tanımladığı hesapla giriş yapın.</p>
        </div>
        <div className="auth-insight-grid">
          <div><strong>Personel</strong><span>özlük kayıtları</span></div>
          <div><strong>İzin</strong><span>talep ve onay</span></div>
          <div><strong>Bordro</strong><span>kontrollü hesap</span></div>
        </div>
      </section>

      <section className="auth-panel">
        {/* Bu ekranda KAYIT YOKTUR — ne kullanıcı ne şirket. Hesap açmanın tek yolu
            operatörün platform anahtarıyla kiracı provizyonlaması, ardından gelen
            davet e-postasıdır (/accept-invite). Kiracı başına ayrı veritabanına
            geçildiğinde "kaydol" bir satır değil bir VERİTABANI oluşturur; anonim
            bir düğmenin bunu tetiklemesi kaynak tüketme açığıdır. */}
        <form id="auth-login" className="auth-form active" onSubmit={(e) => run(e, () => login(email, password))}>
          <h2>Hoş geldiniz</h2>
          <p>{import.meta.env.DEV ? "Demo hesaba giriş yaparak uygulamayı inceleyebilirsiniz." : "Kurumsal e-posta adresiniz ve şifrenizle giriş yapın."}</p>
          <div className="input-group">
            <label htmlFor="login-email">E-posta</label>
            <input id="login-email" type="email" autoComplete="username" required className="input-control" value={email} onChange={(e) => setEmail(e.target.value)} />
          </div>
          <div className="input-group">
            <label htmlFor="login-password">Şifre</label>
            <input id="login-password" autoComplete="current-password" required className="input-control" type="password" value={password} onChange={(e) => setPassword(e.target.value)} />
          </div>
          {error && <p className="form-error" role="alert">{error}</p>}
          <button type="submit" className="btn btn-primary auth-submit">
            <i aria-hidden="true" className="fa-solid fa-arrow-right-to-bracket" /> Giriş yap
          </button>
          <p className="auth-alt">
            Şirketiniz yok mu? Hesabınız İK Pro ekibi tarafından açılır — davet
            e-postanızdaki bağlantıyla şifrenizi belirleyin.
          </p>
        </form>
      </section>
    </main>
  );
}
