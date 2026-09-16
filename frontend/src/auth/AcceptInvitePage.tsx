import { useState, type FormEvent } from "react";
import { useNavigate, useSearchParams } from "react-router-dom";
import { apiFetch, ApiError } from "../api/client";
import { useAuth } from "./AuthContext";
import { roleHomeFor } from "../routes";

/**
 * Davet bağlantısı hedefi (/#/accept-invite?email=...&token=...). Provizyonlanan
 * hr-admin ve işe alınan personel şifresiz oluşturulur; bu ekran davet token'ıyla
 * ilk şifreyi belirler, ardından otomatik giriş yapıp rol ana sayfasına götürür.
 */
export function AcceptInvitePage() {
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
    <main className="auth-shell">
      <section className="auth-visual">
        <div className="auth-brand">
          <div className="brand-mark"><i aria-hidden="true" className="fa-solid fa-users-gear" /></div>
          <div>
            <strong>Ekipzen</strong>
            <span>HR MASTER Suite</span>
          </div>
        </div>
        <div className="auth-copy">
          <span className="status-pill info">Hesap etkinleştirme</span>
          <h1>Şifrenizi belirleyin ve Ekipzen hesabınızı etkinleştirin.</h1>
          <p>Bu bağlantı size özeldir; şifrenizi belirledikten sonra otomatik giriş yapılır.</p>
        </div>
      </section>

      <section className="auth-panel">
        <form className="auth-form active" onSubmit={submit}>
          <h2>Hesabınızı etkinleştirin</h2>
          {missingLink ? (
            <p className="form-error" role="alert">
              Davet bağlantısı geçersiz veya eksik. Lütfen e-postanızdaki bağlantıyı yeniden açın.
            </p>
          ) : (
            <>
              <p>{email} için bir şifre belirleyin.</p>
              {!import.meta.env.DEV && <p>En az 12 karakter, büyük harf ve rakam kullanın.</p>}
              <div className="input-group">
                <label htmlFor="invite-password">Yeni şifre</label>
                <input
                  id="invite-password"
                  className="input-control"
                  type="password"
                  autoComplete="new-password"
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                />
              </div>
              <div className="input-group">
                <label htmlFor="invite-confirm">Şifre (tekrar)</label>
                <input
                  id="invite-confirm"
                  className="input-control"
                  type="password"
                  autoComplete="new-password"
                  value={confirm}
                  onChange={(e) => setConfirm(e.target.value)}
                />
              </div>
              {error && <p className="form-error" role="alert">{error}</p>}
              <button type="submit" className="btn btn-primary auth-submit" disabled={busy}>
                <i aria-hidden="true" className="fa-solid fa-circle-check" />{" "}
                {busy ? "Etkinleştiriliyor…" : "Şifreyi belirle ve giriş yap"}
              </button>
            </>
          )}
        </form>
      </section>
    </main>
  );
}
