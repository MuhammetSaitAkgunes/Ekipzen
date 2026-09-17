import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { afterEach, beforeEach, expect, test, vi } from "vitest";
import { AuthProvider } from "./AuthContext";
import { LoginPage } from "./LoginPage";
import { getSession } from "../api/session";

const authBody = {
  token: "T1",
  refreshToken: "R1",
  expiresAtUtc: "2026-07-13T12:00:00Z",
  user: { id: "u1", name: "İK Yöneticisi", email: "ik@hrmaster.local", role: "hr-admin", roleLabel: "İK Admin", initials: "İK", employeeId: null },
};

beforeEach(() => {
  localStorage.clear();
  vi.stubGlobal("fetch", vi.fn());
});
afterEach(() => { vi.unstubAllGlobals(); vi.unstubAllEnvs(); });

// Gerçek ağaçtaki gibi: AuthProvider oturum değişiminde sorgu önbelleğini
// temizlediği için QueryClientProvider altında çalışır (bkz. App.tsx).
const renderLogin = () =>
  render(
    <QueryClientProvider client={new QueryClient()}>
      <MemoryRouter>
        <AuthProvider>
          <LoginPage />
        </AuthProvider>
      </MemoryRouter>
    </QueryClientProvider>,
  );

test("login formu öndolu demo bilgileriyle render edilir (parite)", () => {
  renderLogin();
  const loginForm = document.getElementById("auth-login")!;
  expect(loginForm.querySelector("input#login-email")).toHaveValue("ik@hrmaster.local");
  expect(loginForm.querySelector("input#login-password")).toHaveValue("demo123");
  expect(loginForm.querySelector("button[type='submit']")).toHaveTextContent("Giriş yap");
});

test("başarılı girişte oturum saklanır", async () => {
  vi.mocked(fetch).mockResolvedValueOnce(
    new Response(JSON.stringify(authBody), { status: 200, headers: { "Content-Type": "application/json" } }),
  );
  renderLogin();
  const submitButton = document.querySelector("form#auth-login button[type='submit']")!;
  await userEvent.click(submitButton);
  expect(getSession()?.token).toBe("T1");
  expect(vi.mocked(fetch).mock.calls[0][0]).toBe("/api/auth/login");
});

test("başarısız girişte hata mesajı görünür, oturum yazılmaz", async () => {
  vi.mocked(fetch).mockResolvedValueOnce(
    new Response(JSON.stringify({ title: "E-posta veya şifre hatalı.", status: 401 }), { status: 401 }),
  );
  renderLogin();
  const submitButton = document.querySelector("form#auth-login button[type='submit']")!;
  await userEvent.click(submitButton);
  expect(await screen.findByText("E-posta veya şifre hatalı.")).toBeInTheDocument();
  expect(getSession()).toBeNull();
});

test("kayıt sekmesi yok; hesabı olmayan demo talebine yönlendirilir", () => {
  renderLogin();
  expect(document.getElementById("auth-login")).toHaveClass("ez-auth__form");
  expect(document.getElementById("auth-signup")).toBeNull();
  expect(screen.getByRole("link", { name: "Demo isteyin" })).toHaveAttribute("href", "/iletisim");
  expect(screen.getByRole("link", { name: /Ana sayfaya dön/ })).toHaveAttribute("href", "/");
});

test("üretimde demo kimliği ve yanıltıcı sabit skorlar gösterilmez", () => {
  vi.stubEnv("DEV", false);
  renderLogin();
  expect(screen.getByLabelText("E-posta")).toHaveValue("");
  expect(screen.getByLabelText("Şifre")).toHaveValue("");
  expect(screen.queryByText("Demo erişim")).not.toBeInTheDocument();
  expect(screen.queryByText("82")).not.toBeInTheDocument();
  expect(screen.getByText("Şirket hesabı")).toBeInTheDocument();
});

test("giriş ekranı sayfa başlığını ve erişilebilir başlığı ayarlar", () => {
  renderLogin();
  expect(screen.getByRole("heading", { level: 1, name: "Hesabınıza giriş yapın" })).toBeInTheDocument();
  expect(document.title).toBe("Giriş yap · Ekipzen");
});

test("gönderim sırasında buton devre dışı kalır", async () => {
  let resolve!: (r: Response) => void;
  vi.mocked(fetch).mockReturnValueOnce(new Promise<Response>((r) => { resolve = r; }));
  renderLogin();
  await userEvent.click(screen.getByRole("button", { name: /Giriş yap/ }));
  expect(screen.getByRole("button", { name: /Giriş yapılıyor/ })).toBeDisabled();
  resolve(new Response(JSON.stringify(authBody), { status: 200, headers: { "Content-Type": "application/json" } }));
});
