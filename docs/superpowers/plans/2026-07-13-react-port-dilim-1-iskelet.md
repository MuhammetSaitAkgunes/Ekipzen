# React Port — Dilim 1: İskelet — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** `frontend/` altında Vite+React+TS iskeleti: gerçek API'ye JWT login, rol guard'lı router, birebir AppShell (sidebar/header/tema), tüm modüller için placeholder sayfalar.

**Architecture:** SPA + hash router (eski `#/dashboard` URL'leriyle birebir). Tipler backend swagger.json'dan üretilir; fetch wrapper Bearer + ProblemDetails + 401-refresh akışını tek yerde çözer; oturum `AuthContext`'te. Mevcut CSS aynen kopyalanır, component'ler aynı class adlarıyla aynı DOM'u üretir.

**Tech Stack:** Vite 6, React 18.3, TypeScript (strict), react-router-dom 7 (createHashRouter), @tanstack/react-query 5, openapi-typescript 7, Vitest + React Testing Library.

## Global Constraints

- React sürümü **18.3.x** (spec kararı); Vite şablonu 19 kurarsa 18'e sabitle.
- Mevcut CSS dosyaları **değiştirilmeden** kopyalanır; component'ler **aynı class adları ve DOM yapısını** üretir. Türkçe metinler kaynaktan birebir alınır.
- URL şeması hash tabanlı kalır: `#/dashboard`, `#/login` … (eski uygulamayla parite).
- localStorage anahtarları: tema `ekipzen-theme`, sidebar `ekipzen-sidebar` (eskiyle aynı); oturum **`ekipzen-session`** (yeni — eski `ekipzen-demo-session` mock'a aitti, taşınmaz).
- API erişimi Vite proxy üzerinden: istekler `/api/...` → `http://localhost:5053` (backend http profili). Testlerde `fetch` stub'lanır, gerçek ağ yok.
- Backend'i çalıştırma komutu (tip üretimi ve duman testi için):
  `cd backend && dotnet run --project src/Ekipzen.API --launch-profile http`
- Rol dizeleri backend ile birebir: `hr-admin` | `manager` | `employee`.
- Eski frontend dosyalarına (kökteki `components/`, `styles/` …) **dokunulmaz**.
- Her görev sonunda `cd frontend && npm test -- --run` yeşil olmalı.

---

### Task 1: Vite iskeleti + CSS taşıma + test altyapısı

**Files:**
- Create: `frontend/` (Vite şablonu), `frontend/vite.config.ts`, `frontend/src/main.tsx`, `frontend/src/App.tsx`, `frontend/index.html`, `frontend/src/test/setup.ts`
- Create: `frontend/src/styles/` ← kökteki `styles/*.css` kopyası (11 dosya)
- Test: `frontend/src/App.test.tsx`

**Interfaces:**
- Produces: çalışan Vite+Vitest altyapısı; `src/styles/` altında 11 CSS; `/api` proxy'si.

- [x] **Step 1: Vite projesini oluştur ve sürümleri sabitle**

```bash
cd "C:\Users\Lenovo\OneDrive\Masaüstü\Ekipzen UI\Ekipzen"
npm create vite@latest frontend -- --template react-ts
cd frontend
npm install react@18.3.1 react-dom@18.3.1
npm install react-router-dom@^7 @tanstack/react-query@^5
npm install -D @types/react@^18 @types/react-dom@^18 openapi-typescript@^7 vitest jsdom @testing-library/react @testing-library/jest-dom @testing-library/user-event
```

- [x] **Step 2: CSS dosyalarını kopyala** (değiştirmeden)

```bash
mkdir src/styles
cp ../styles/*.css src/styles/
```

Beklenen: 11 dosya (main, auth, layout, actions, personnel, recruitment, attendance, leaves, payroll, manager, settings).

- [x] **Step 3: `index.html`'i eski head ile hizala** — içeriği tamamen değiştir:

```html
<!doctype html>
<html lang="tr">
  <head>
    <meta charset="UTF-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>Ekipzen - Kurumsal İK Yönetimi</title>
    <link rel="preconnect" href="https://fonts.googleapis.com" />
    <link rel="preconnect" href="https://fonts.gstatic.com" crossorigin />
    <link
      href="https://fonts.googleapis.com/css2?family=IBM+Plex+Sans:wght@400;500;600;700&family=IBM+Plex+Mono:wght@400;500;600&display=swap"
      rel="stylesheet"
    />
    <link
      rel="stylesheet"
      href="https://cdnjs.cloudflare.com/ajax/libs/font-awesome/6.4.0/css/all.min.css"
    />
  </head>
  <body>
    <div id="root"></div>
    <script type="module" src="/src/main.tsx"></script>
  </body>
</html>
```

- [x] **Step 4: `vite.config.ts`** — proxy + Vitest:

```ts
import { defineConfig } from "vitest/config";
import react from "@vitejs/plugin-react";

export default defineConfig({
  plugins: [react()],
  server: {
    proxy: {
      // Backend http profili (launchSettings.json): TLS sertifika derdi olmadan geliştirme.
      "/api": { target: "http://localhost:5053", changeOrigin: true },
    },
  },
  test: {
    environment: "jsdom",
    globals: true,
    setupFiles: "./src/test/setup.ts",
  },
});
```

- [x] **Step 5: `src/test/setup.ts`**

```ts
import "@testing-library/jest-dom/vitest";
```

- [x] **Step 6: Şablon artıklarını temizle, `main.tsx` ve `App.tsx` yaz**

`src/App.css`, `src/index.css`, `src/assets/react.svg`, `public/vite.svg` dosyalarını sil.

`src/main.tsx` (CSS import sırası eski index.html ile aynı):

```tsx
import React from "react";
import ReactDOM from "react-dom/client";
import App from "./App";
import "./styles/main.css";
import "./styles/auth.css";
import "./styles/layout.css";
import "./styles/actions.css";
import "./styles/personnel.css";
import "./styles/recruitment.css";
import "./styles/attendance.css";
import "./styles/leaves.css";
import "./styles/payroll.css";
import "./styles/manager.css";
import "./styles/settings.css";

ReactDOM.createRoot(document.getElementById("root")!).render(
  <React.StrictMode>
    <App />
  </React.StrictMode>,
);
```

`src/App.tsx` (geçici — Task 5'te router'a dönüşecek):

```tsx
export default function App() {
  return <h1>Ekipzen</h1>;
}
```

- [x] **Step 7: Smoke testi yaz** — `src/App.test.tsx`:

```tsx
import { render, screen } from "@testing-library/react";
import App from "./App";

test("uygulama başlığı render edilir", () => {
  render(<App />);
  expect(screen.getByText("Ekipzen")).toBeInTheDocument();
});
```

- [x] **Step 8: Testi ve build'i doğrula**

Run: `npm test -- --run` → Beklenen: 1 test PASS.
Run: `npm run build` → Beklenen: hatasız `dist/` çıktısı.

- [x] **Step 9: package.json script'ine test ekle ve commit**

`package.json` `scripts` içine: `"test": "vitest"`.

```bash
cd "C:\Users\Lenovo\OneDrive\Masaüstü\Ekipzen UI\Ekipzen"
git add frontend/
git commit -m "feat(frontend): Vite+React+TS iskeleti, CSS taşıma, Vitest altyapısı"
```

---

### Task 2: OpenAPI tip üretimi

**Files:**
- Modify: `frontend/package.json` (script)
- Create: `frontend/src/api/schema.d.ts` (üretilen)

**Interfaces:**
- Produces: `import type { components, paths } from "./schema"` — sonraki görevler `components["schemas"]["AuthResponse"]`, `components["schemas"]["UserDto"]` tiplerini kullanır.

- [x] **Step 1: Script ekle** — `package.json` `scripts`:

```json
"gen:api": "openapi-typescript http://localhost:5053/swagger/v1/swagger.json -o src/api/schema.d.ts"
```

- [x] **Step 2: Backend'i başlat (ayrı terminal)**

Run: `cd backend && dotnet run --project src/Ekipzen.API --launch-profile http`
Beklenen: `Now listening on: http://localhost:5053`.

- [x] **Step 3: Tipleri üret ve doğrula**

Run: `cd frontend && npm run gen:api`
Doğrula: `src/api/schema.d.ts` içinde `"/api/auth/login"` ve `AuthResponse` geçiyor:
`grep -c "auth/login\|AuthResponse" src/api/schema.d.ts` → ≥ 2.

- [x] **Step 4: Commit**

```bash
git add frontend/package.json frontend/src/api/schema.d.ts
git commit -m "feat(frontend): swagger'dan openapi-typescript tip üretimi (gen:api)"
```

---

### Task 3: Oturum deposu + fetch wrapper (TDD)

**Files:**
- Create: `frontend/src/api/session.ts`, `frontend/src/api/client.ts`
- Test: `frontend/src/api/client.test.ts`

**Interfaces:**
- Consumes: `components["schemas"]["AuthResponse"]`, `["UserDto"]` (Task 2).
- Produces:
  - `session.ts`: `type Session = { token: string; refreshToken: string; user: UserDto }`, `getSession(): Session | null`, `setSession(s: Session): void`, `clearSession(): void`, `SESSION_KEY = "ekipzen-session"`.
  - `client.ts`: `class ApiError extends Error { status: number; problem?: ProblemDetails }`, `apiFetch<T>(path: string, init?: RequestInit): Promise<T>` (görece yol `/api` öneki alır, Bearer ekler, 204→null, 401'de tek seferlik refresh+retry, refresh düşerse `clearSession()` + `window.location.hash = "/login"`).

- [x] **Step 1: `session.ts` yaz**

```ts
import type { components } from "./schema";

export type UserDto = components["schemas"]["UserDto"];
export type Session = { token: string; refreshToken: string; user: UserDto };

export const SESSION_KEY = "ekipzen-session";

export const getSession = (): Session | null => {
  try {
    return JSON.parse(localStorage.getItem(SESSION_KEY) || "null");
  } catch {
    return null;
  }
};

export const setSession = (session: Session) =>
  localStorage.setItem(SESSION_KEY, JSON.stringify(session));

export const clearSession = () => localStorage.removeItem(SESSION_KEY);
```

- [x] **Step 2: Başarısız testleri yaz** — `client.test.ts`:

```ts
import { afterEach, beforeEach, expect, test, vi } from "vitest";
import { ApiError, apiFetch } from "./client";
import { SESSION_KEY, getSession, setSession } from "./session";

const user = { id: "u1", name: "İK Yöneticisi", email: "ik@hrmaster.local", role: "hr-admin", roleLabel: "İK Admin", initials: "İK", employeeId: null } as never;
const json = (status: number, body: unknown) =>
  new Response(JSON.stringify(body), { status, headers: { "Content-Type": "application/json" } });

beforeEach(() => {
  localStorage.clear();
  vi.stubGlobal("fetch", vi.fn());
});
afterEach(() => vi.unstubAllGlobals());

test("başarılı istek Bearer başlığı taşır ve JSON döner", async () => {
  setSession({ token: "T1", refreshToken: "R1", user });
  vi.mocked(fetch).mockResolvedValueOnce(json(200, { ok: true }));

  const result = await apiFetch<{ ok: boolean }>("/me");

  expect(result.ok).toBe(true);
  const [url, init] = vi.mocked(fetch).mock.calls[0];
  expect(url).toBe("/api/me");
  expect(new Headers(init?.headers).get("Authorization")).toBe("Bearer T1");
});

test("ProblemDetails hatası ApiError olarak fırlar", async () => {
  vi.mocked(fetch).mockResolvedValueOnce(json(409, { title: "Çakışan kayıt.", status: 409 }));

  const error = await apiFetch("/leaves", { method: "POST" }).catch((e) => e);

  expect(error).toBeInstanceOf(ApiError);
  expect(error).toMatchObject({ status: 409, message: "Çakışan kayıt." });
});

test("401'de refresh denenir ve istek yeni token'la tekrarlanır", async () => {
  setSession({ token: "ESKI", refreshToken: "R1", user });
  vi.mocked(fetch)
    .mockResolvedValueOnce(json(401, { title: "Yetkisiz" }))
    .mockResolvedValueOnce(json(200, { token: "YENI", refreshToken: "R2", expiresAtUtc: "2026-07-13T12:00:00Z", user }))
    .mockResolvedValueOnce(json(200, { ok: true }));

  const result = await apiFetch<{ ok: boolean }>("/me");

  expect(result.ok).toBe(true);
  expect(getSession()?.token).toBe("YENI");
  expect(vi.mocked(fetch).mock.calls[1][0]).toBe("/api/auth/refresh");
  expect(new Headers(vi.mocked(fetch).mock.calls[2][1]?.headers).get("Authorization")).toBe("Bearer YENI");
});

test("refresh de düşerse oturum silinir ve login'e yönlenir", async () => {
  setSession({ token: "ESKI", refreshToken: "R1", user });
  vi.mocked(fetch)
    .mockResolvedValueOnce(json(401, { title: "Yetkisiz" }))
    .mockResolvedValueOnce(json(401, { title: "Refresh geçersiz" }));

  await expect(apiFetch("/me")).rejects.toMatchObject({ status: 401 });
  expect(localStorage.getItem(SESSION_KEY)).toBeNull();
  expect(window.location.hash).toBe("#/login");
});
```

- [x] **Step 2b: Testin başarısız olduğunu doğrula**

Run: `npm test -- --run src/api/client.test.ts`
Beklenen: FAIL — `client.ts` yok / export bulunamadı.

- [x] **Step 3: `client.ts` yaz**

```ts
import type { components } from "./schema";
import { clearSession, getSession, setSession } from "./session";

type ProblemDetails = { title?: string; status?: number; errors?: Record<string, string[]> };
type AuthResponse = components["schemas"]["AuthResponse"];

export class ApiError extends Error {
  constructor(
    public status: number,
    message: string,
    public problem?: ProblemDetails,
  ) {
    super(message);
  }
}

const API_BASE = "/api";
let refreshInFlight: Promise<boolean> | null = null;

const rawFetch = async (path: string, init: RequestInit = {}): Promise<Response> => {
  const headers = new Headers(init.headers);
  if (!headers.has("Content-Type") && init.body) headers.set("Content-Type", "application/json");
  const token = getSession()?.token;
  if (token) headers.set("Authorization", `Bearer ${token}`);
  return fetch(`${API_BASE}${path}`, { ...init, headers });
};

// Tek-uçuş refresh: eşzamanlı 401'ler aynı refresh isteğini paylaşır.
const tryRefresh = (): Promise<boolean> =>
  (refreshInFlight ??= (async () => {
    try {
      const session = getSession();
      if (!session?.refreshToken) return false;
      const response = await fetch(`${API_BASE}/auth/refresh`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ refreshToken: session.refreshToken }),
      });
      if (!response.ok) return false;
      const auth = (await response.json()) as AuthResponse;
      setSession({ token: auth.token, refreshToken: auth.refreshToken, user: auth.user });
      return true;
    } catch {
      return false;
    } finally {
      refreshInFlight = null;
    }
  })());

const toError = async (response: Response): Promise<ApiError> => {
  let problem: ProblemDetails | undefined;
  try {
    problem = await response.json();
  } catch {
    /* gövdesiz hata */
  }
  return new ApiError(response.status, problem?.title || `İstek başarısız (${response.status})`, problem);
};

export async function apiFetch<T>(path: string, init: RequestInit = {}): Promise<T> {
  let response = await rawFetch(path, init);

  if (response.status === 401 && !path.startsWith("/auth/")) {
    if (await tryRefresh()) {
      response = await rawFetch(path, init);
    } else {
      clearSession();
      window.location.hash = "/login";
    }
  }

  if (!response.ok) throw await toError(response);
  if (response.status === 204) return null as T;
  return (await response.json()) as T;
}
```

- [x] **Step 4: Testleri doğrula**

Run: `npm test -- --run src/api/client.test.ts` → Beklenen: 4 test PASS.

- [x] **Step 5: Commit**

```bash
git add frontend/src/api/
git commit -m "feat(frontend): Bearer + ProblemDetails + 401-refresh akışlı fetch wrapper (TDD)"
```

---

### Task 4: AuthContext + Login/Signup ekranı (TDD)

**Files:**
- Create: `frontend/src/auth/AuthContext.tsx`, `frontend/src/auth/LoginPage.tsx`
- Test: `frontend/src/auth/LoginPage.test.tsx`

**Interfaces:**
- Consumes: `apiFetch`, `session.ts` (Task 3).
- Produces:
  - `AuthContext.tsx`: `AuthProvider`, `useAuth(): { user: UserDto | null; login(email, password): Promise<UserDto>; register(name, email, password): Promise<UserDto>; logout(): Promise<void> }`.
  - `LoginPage.tsx`: `<LoginPage mode="login" | "signup" />` — eski `AuthScreen` markup paritesi (`auth-shell`, `auth-tabs`, `auth-form active` …). Hata durumu için formun altında `<p className="form-error">{mesaj}</p>` (API zorunlu kıldığı yeni durum).
  - Signup formuna **şifre alanı eklenir** (backend `RegisterCommand(Name, Email, Password, Role?)` zorunlu kılar — eski mock'ta yoktu).

- [x] **Step 1: Başarısız testleri yaz** — `LoginPage.test.tsx`:

```tsx
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
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
afterEach(() => vi.unstubAllGlobals());

const renderLogin = () =>
  render(
    <MemoryRouter>
      <AuthProvider>
        <LoginPage mode="login" />
      </AuthProvider>
    </MemoryRouter>,
  );

test("login formu öndolu demo bilgileriyle render edilir (parite)", () => {
  renderLogin();
  expect(screen.getByLabelText("E-posta")).toHaveValue("ik@hrmaster.local");
  expect(screen.getByLabelText("Şifre")).toHaveValue("demo123");
  expect(screen.getByRole("button", { name: /Giriş yap/ })).toBeInTheDocument();
});

test("başarılı girişte oturum saklanır", async () => {
  vi.mocked(fetch).mockResolvedValueOnce(
    new Response(JSON.stringify(authBody), { status: 200, headers: { "Content-Type": "application/json" } }),
  );
  renderLogin();
  await userEvent.click(screen.getByRole("button", { name: /Giriş yap/ }));
  expect(getSession()?.token).toBe("T1");
  expect(vi.mocked(fetch).mock.calls[0][0]).toBe("/api/auth/login");
});

test("başarısız girişte hata mesajı görünür, oturum yazılmaz", async () => {
  vi.mocked(fetch).mockResolvedValueOnce(
    new Response(JSON.stringify({ title: "E-posta veya şifre hatalı.", status: 401 }), { status: 401 }),
  );
  renderLogin();
  await userEvent.click(screen.getByRole("button", { name: /Giriş yap/ }));
  expect(await screen.findByText("E-posta veya şifre hatalı.")).toBeInTheDocument();
  expect(getSession()).toBeNull();
});
```

Run: `npm test -- --run src/auth` → Beklenen: FAIL (modüller yok).

- [x] **Step 2: `AuthContext.tsx` yaz**

```tsx
import { createContext, useCallback, useContext, useState, type ReactNode } from "react";
import { apiFetch } from "../api/client";
import { clearSession, getSession, setSession, type UserDto } from "../api/session";
import type { components } from "../api/schema";

type AuthResponse = components["schemas"]["AuthResponse"];

type AuthValue = {
  user: UserDto | null;
  login: (email: string, password: string) => Promise<UserDto>;
  register: (name: string, email: string, password: string) => Promise<UserDto>;
  logout: () => Promise<void>;
};

const AuthContext = createContext<AuthValue | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<UserDto | null>(() => getSession()?.user ?? null);

  const accept = useCallback((auth: AuthResponse) => {
    setSession({ token: auth.token, refreshToken: auth.refreshToken, user: auth.user });
    setUser(auth.user);
    return auth.user;
  }, []);

  const login = useCallback(
    async (email: string, password: string) =>
      accept(await apiFetch<AuthResponse>("/auth/login", { method: "POST", body: JSON.stringify({ email, password }) })),
    [accept],
  );

  const register = useCallback(
    async (name: string, email: string, password: string) =>
      accept(await apiFetch<AuthResponse>("/auth/register", { method: "POST", body: JSON.stringify({ name, email, password }) })),
    [accept],
  );

  const logout = useCallback(async () => {
    const refreshToken = getSession()?.refreshToken;
    try {
      if (refreshToken) await apiFetch("/auth/logout", { method: "POST", body: JSON.stringify({ refreshToken }) });
    } catch {
      /* oturum zaten kapanıyor */
    }
    clearSession();
    setUser(null);
  }, []);

  return <AuthContext.Provider value={{ user, login, register, logout }}>{children}</AuthContext.Provider>;
}

export function useAuth(): AuthValue {
  const value = useContext(AuthContext);
  if (!value) throw new Error("useAuth, AuthProvider içinde kullanılmalı.");
  return value;
}
```

- [x] **Step 3: `LoginPage.tsx` yaz** (eski `AuthScreen` DOM paritesi; `roleHome` Task 5'te gelecek — şimdilik `/dashboard`'a yönlendir, Task 5 bunu `roleHomeFor(user.role)` ile değiştirecek):

```tsx
import { useState, type FormEvent } from "react";
import { useNavigate } from "react-router-dom";
import { ApiError } from "../api/client";
import { useAuth } from "./AuthContext";

export function LoginPage({ mode = "login" }: { mode?: "login" | "signup" }) {
  const { login, register } = useAuth();
  const navigate = useNavigate();
  const [email, setEmail] = useState("ik@hrmaster.local");
  const [password, setPassword] = useState("demo123");
  const [name, setName] = useState("İK Yöneticisi");
  const [signupEmail, setSignupEmail] = useState("ik@hrmaster.local");
  const [signupPassword, setSignupPassword] = useState("");
  const [error, setError] = useState<string | null>(null);

  const run = async (event: FormEvent, action: () => Promise<unknown>) => {
    event.preventDefault();
    setError(null);
    try {
      await action();
      navigate("/dashboard");
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
            <strong>Ekipzen</strong>
            <span>HR MASTER Suite</span>
          </div>
        </div>
        <div className="auth-copy">
          <span className="status-pill info">Demo erişim</span>
          <h1>Risk, bordro ve İK operasyonlarını tek merkezden yönetin.</h1>
          <p>Bu giriş ekranı gerçek backend oturumu açar; yetki kontrolü .NET policy katmanındadır.</p>
        </div>
        <div className="auth-insight-grid">
          <div><strong>7</strong><span>kritik aksiyon</span></div>
          <div><strong>5</strong><span>bordro kontrolü</span></div>
          <div><strong>82</strong><span>uyum skoru</span></div>
        </div>
      </section>

      <section className="auth-panel">
        <div className="auth-tabs">
          <button className={`auth-tab ${mode === "login" ? "active" : ""}`} onClick={() => navigate("/login")}>Giriş yap</button>
          <button className={`auth-tab ${mode === "signup" ? "active" : ""}`} onClick={() => navigate("/signup")}>Hesap oluştur</button>
        </div>

        <form id="auth-login" className={`auth-form ${mode === "login" ? "active" : ""}`} onSubmit={(e) => run(e, () => login(email, password))}>
          <h2>Hoş geldiniz</h2>
          <p>Demo hesaba giriş yaparak uygulamayı inceleyebilirsiniz.</p>
          <div className="input-group">
            <label htmlFor="login-email">E-posta</label>
            <input id="login-email" className="input-control" value={email} onChange={(e) => setEmail(e.target.value)} />
          </div>
          <div className="input-group">
            <label htmlFor="login-password">Şifre</label>
            <input id="login-password" className="input-control" type="password" value={password} onChange={(e) => setPassword(e.target.value)} />
          </div>
          {mode === "login" && error && <p className="form-error" role="alert">{error}</p>}
          <button type="submit" className="btn btn-primary auth-submit">
            <i aria-hidden="true" className="fa-solid fa-arrow-right-to-bracket" /> Giriş yap
          </button>
        </form>

        <form id="auth-signup" className={`auth-form ${mode === "signup" ? "active" : ""}`} onSubmit={(e) => run(e, () => register(name, signupEmail, signupPassword))}>
          <h2>Demo hesap oluştur</h2>
          <p>Bilgiler gerçek backend'de kullanıcı kaydı oluşturur.</p>
          <div className="input-group">
            <label htmlFor="signup-name">Ad soyad</label>
            <input id="signup-name" className="input-control" value={name} onChange={(e) => setName(e.target.value)} />
          </div>
          <div className="input-group">
            <label htmlFor="signup-email">İş e-postası</label>
            <input id="signup-email" className="input-control" value={signupEmail} onChange={(e) => setSignupEmail(e.target.value)} />
          </div>
          <div className="input-group">
            <label htmlFor="signup-password">Şifre</label>
            <input id="signup-password" className="input-control" type="password" value={signupPassword} onChange={(e) => setSignupPassword(e.target.value)} />
          </div>
          {mode === "signup" && error && <p className="form-error" role="alert">{error}</p>}
          <button type="submit" className="btn btn-primary auth-submit">
            <i aria-hidden="true" className="fa-solid fa-user-plus" /> Hesap oluştur
          </button>
        </form>
      </section>
    </main>
  );
}
```

- [x] **Step 4: Testleri doğrula**

Run: `npm test -- --run src/auth` → Beklenen: 3 test PASS.

- [x] **Step 5: Commit**

```bash
git add frontend/src/auth/
git commit -m "feat(frontend): AuthContext + gerçek API'ye bağlı login/signup ekranı (parite)"
```

---

### Task 5: Route tablosu + rol guard'ları (TDD)

**Files:**
- Create: `frontend/src/routes.tsx`, `frontend/src/auth/guards.tsx`, `frontend/src/pages/PlaceholderPage.tsx`
- Modify: `frontend/src/App.tsx`, `frontend/src/main.tsx`, `frontend/src/auth/LoginPage.tsx` (roleHome yönlendirmesi)
- Test: `frontend/src/routes.test.tsx`

**Interfaces:**
- Consumes: `useAuth` (Task 4).
- Produces:
  - `routes.tsx`: `type AppRoute = { key: string; path: string; title: string; eyebrow: string; navKey: string; roles: Role[] }`, `type Role = "hr-admin" | "manager" | "employee"`, `appRoutes: AppRoute[]` (eski routes.js'in 17 korumalı rotası birebir: dashboard, overview, actions, personnel, recruitment, attendance, leaves, payroll, payroll-calculator (`/payroll/calculator`), payroll-settings (`/payroll/settings`), manager, settings, attrition-risk (`/risk/attrition`), burnout-risk (`/risk/burnout`), manager-load (`/risk/manager-load`), action-center (`/risk/action-center`), employee-voice (`/risk/employee-voice`), compliance-risk (`/risk/compliance`)), `roleHomeFor(role): string` (hr-admin/manager → `/dashboard`, employee → `/overview`), `navGroups`, `navIcons` (layout.js'ten birebir).
  - `guards.tsx`: `RequireAuth` (oturum yoksa `/login`'e Navigate; login sayfaları oturum varken roleHome'a Navigate — `PublicOnly`), `RouteGate` (rol yetkisi yoksa **redirect değil**, eski davranış paritesiyle shell içinde "Yetki Gerekli" ekranı).
  - `PlaceholderPage`: eski `emptyRouteState` markup'ı (`surface empty-state`, "Bu alan hazırlanıyor").

- [x] **Step 1: Başarısız testleri yaz** — `src/routes.test.tsx`:

```tsx
import { render, screen } from "@testing-library/react";
import { RouterProvider, createMemoryRouter } from "react-router-dom";
import { beforeEach, expect, test } from "vitest";
import { buildRouteObjects } from "./routes";
import { AuthProvider } from "./auth/AuthContext";
import { SESSION_KEY } from "./api/session";

const sessionFor = (role: string, name: string) =>
  JSON.stringify({ token: "T", refreshToken: "R", user: { id: "u", name, email: "x@x", role, roleLabel: name, initials: "XX", employeeId: null } });

const renderAt = (path: string) => {
  const router = createMemoryRouter(buildRouteObjects(), { initialEntries: [path] });
  return render(
    <AuthProvider>
      <RouterProvider router={router} />
    </AuthProvider>,
  );
};

beforeEach(() => localStorage.clear());

test("oturum yokken korumalı rota login'e yönlenir", () => {
  renderAt("/dashboard");
  expect(screen.getByRole("button", { name: /Giriş yap/ })).toBeInTheDocument();
});

test("employee, dashboard'da 'Yetki Gerekli' ekranı görür (redirect yok)", () => {
  localStorage.setItem(SESSION_KEY, sessionFor("employee", "Ahmet Yılmaz"));
  renderAt("/dashboard");
  expect(screen.getByText("Bu alan için yetki gerekli")).toBeInTheDocument();
});

test("hr-admin, settings placeholder'ını görür", () => {
  localStorage.setItem(SESSION_KEY, sessionFor("hr-admin", "İK Yöneticisi"));
  renderAt("/settings");
  expect(screen.getByText("Bu alan hazırlanıyor")).toBeInTheDocument();
});
```

Run: `npm test -- --run src/routes.test.tsx` → Beklenen: FAIL.

- [x] **Step 2: `PlaceholderPage.tsx` yaz**

```tsx
export function PlaceholderPage() {
  return (
    <section className="surface empty-state">
      <i aria-hidden="true" className="fa-regular fa-compass" />
      <h2>Bu alan hazırlanıyor</h2>
      <p>Seçtiğiniz modül henüz React portuna eklenmedi.</p>
    </section>
  );
}
```

- [x] **Step 3: `routes.tsx` yaz** — rota meta tablosu + router fabrikası:

```tsx
import type { RouteObject } from "react-router-dom";
import { LoginPage } from "./auth/LoginPage";
import { PublicOnly, RequireAuth, RouteGate } from "./auth/guards";
import { PlaceholderPage } from "./pages/PlaceholderPage";

export type Role = "hr-admin" | "manager" | "employee";

export type AppRoute = {
  key: string;
  path: string;
  title: string;
  eyebrow: string;
  navKey: string;
  roles: Role[];
};

const ALL: Role[] = ["hr-admin", "manager", "employee"];
const MGMT: Role[] = ["hr-admin", "manager"];

// routes.js ile birebir (sıra dahil).
export const appRoutes: AppRoute[] = [
  { key: "dashboard", path: "/dashboard", title: "Risk Merkezi", eyebrow: "Ekipzen", navKey: "dashboard", roles: MGMT },
  { key: "overview", path: "/overview", title: "Genel Durum", eyebrow: "Ekipzen", navKey: "overview", roles: ALL },
  { key: "actions", path: "/actions", title: "Aksiyonlar", eyebrow: "Ekipzen", navKey: "actions", roles: ALL },
  { key: "personnel", path: "/personnel", title: "Personel Yönetimi", eyebrow: "Ekipzen", navKey: "personnel", roles: MGMT },
  { key: "recruitment", path: "/recruitment", title: "İşe Alım", eyebrow: "Ekipzen", navKey: "recruitment", roles: ["hr-admin"] },
  { key: "attendance", path: "/attendance", title: "Mesai & Puantaj", eyebrow: "Ekipzen", navKey: "attendance", roles: MGMT },
  { key: "leaves", path: "/leaves", title: "İzinlerim", eyebrow: "Ekipzen", navKey: "leaves", roles: ALL },
  { key: "payroll", path: "/payroll", title: "Bordro", eyebrow: "Ekipzen", navKey: "payroll", roles: ["hr-admin", "employee"] },
  { key: "payroll-calculator", path: "/payroll/calculator", title: "Bordro", eyebrow: "Ekipzen", navKey: "payroll", roles: ["hr-admin"] },
  { key: "payroll-settings", path: "/payroll/settings", title: "Bordro", eyebrow: "Ekipzen", navKey: "payroll", roles: ["hr-admin"] },
  { key: "manager", path: "/manager", title: "Yönetici Konsolu", eyebrow: "Ekipzen", navKey: "manager", roles: MGMT },
  { key: "settings", path: "/settings", title: "Ayarlar", eyebrow: "Ekipzen", navKey: "settings", roles: ["hr-admin"] },
  { key: "attrition-risk", path: "/risk/attrition", title: "Ayrılma Riski Detayı", eyebrow: "Risk Merkezi", navKey: "dashboard", roles: MGMT },
  { key: "burnout-risk", path: "/risk/burnout", title: "Tükenmişlik Sinyali", eyebrow: "Risk Merkezi", navKey: "dashboard", roles: MGMT },
  { key: "manager-load", path: "/risk/manager-load", title: "Yönetici Yükü", eyebrow: "Risk Merkezi", navKey: "dashboard", roles: MGMT },
  { key: "action-center", path: "/risk/action-center", title: "Aksiyonlar", eyebrow: "Ekipzen", navKey: "actions", roles: MGMT },
  { key: "employee-voice", path: "/risk/employee-voice", title: "Çalışan Nabzı", eyebrow: "Risk Merkezi", navKey: "dashboard", roles: MGMT },
  { key: "compliance-risk", path: "/risk/compliance", title: "Uyum Risk Merkezi", eyebrow: "Risk Merkezi", navKey: "dashboard", roles: MGMT },
];

export const roleHomeFor = (role: string | undefined): string =>
  role === "employee" ? "/overview" : "/dashboard";

// layout.js ile birebir.
export const navIcons: Record<string, string> = {
  dashboard: "fa-shield-heart", overview: "fa-chart-line", actions: "fa-list-check",
  personnel: "fa-user-tie", recruitment: "fa-briefcase", attendance: "fa-clock",
  leaves: "fa-plane-departure", payroll: "fa-money-check-dollar",
  manager: "fa-chart-pie", settings: "fa-gear",
};

export const navGroups = [
  { label: "Ana Sayfa", keys: ["dashboard", "overview", "actions"] },
  { label: "Çekirdek İK", keys: ["personnel", "recruitment", "attendance", "leaves"] },
  { label: "Bordro", keys: ["payroll"] },
  { label: "Yönetim", keys: ["manager", "settings"] },
];

// Sayfa component eşlemesi: sonraki dilimler PlaceholderPage'i gerçek sayfayla değiştirir.
const pageFor: Record<string, () => JSX.Element> = {};

// Component JSX olarak render edilir (düz fonksiyon çağrısı hook kurallarını bozar).
function GatedPage({ route }: { route: AppRoute }) {
  const Page = pageFor[route.key] ?? PlaceholderPage;
  return (
    <RouteGate route={route}>
      <Page />
    </RouteGate>
  );
}

export function buildRouteObjects(): RouteObject[] {
  return [
    { path: "/login", element: <PublicOnly><LoginPage mode="login" /></PublicOnly> },
    { path: "/signup", element: <PublicOnly><LoginPage mode="signup" /></PublicOnly> },
    {
      element: <RequireAuth />, // AppShell Task 6'da bu elemente sarılacak
      children: appRoutes.map((route) => ({
        path: route.path,
        element: <GatedPage route={route} />,
      })),
    },
    { path: "*", element: <PublicOnly><LoginPage mode="login" /></PublicOnly> },
  ];
}
```

- [x] **Step 4: `guards.tsx` yaz**

```tsx
import type { ReactNode } from "react";
import { Navigate, Outlet } from "react-router-dom";
import { useAuth } from "./AuthContext";
import { roleHomeFor, type AppRoute, type Role } from "../routes";

export function RequireAuth() {
  const { user } = useAuth();
  if (!user) return <Navigate to="/login" replace />;
  return <Outlet />;
}

export function PublicOnly({ children }: { children: ReactNode }) {
  const { user } = useAuth();
  if (user) return <Navigate to={roleHomeFor(user.role)} replace />;
  return <>{children}</>;
}

/** Eski renderRestrictedRoute paritesi: yetkisiz rol redirect edilmez, kilit ekranı görür. */
export function RouteGate({ route, children }: { route: AppRoute; children: ReactNode }) {
  const { user } = useAuth();
  if (user && !route.roles.includes(user.role as Role)) {
    return (
      <section className="surface empty-state">
        <i aria-hidden="true" className="fa-solid fa-lock" />
        <h2>Bu alan için yetki gerekli</h2>
        <p>Bu rol ile seçilen modüle erişim kapalı. Yetki kontrolü backend policy katmanında da uygulanır.</p>
      </section>
    );
  }
  return <>{children}</>;
}
```

- [x] **Step 5: `App.tsx` + `main.tsx` router'a bağla; LoginPage roleHome kullansın**

`App.tsx`:

```tsx
import { RouterProvider, createHashRouter } from "react-router-dom";
import { AuthProvider } from "./auth/AuthContext";
import { buildRouteObjects } from "./routes";

const router = createHashRouter(buildRouteObjects());

export default function App() {
  return (
    <AuthProvider>
      <RouterProvider router={router} />
    </AuthProvider>
  );
}
```

`LoginPage.tsx` içinde `navigate("/dashboard")` satırını değiştir:

```tsx
import { roleHomeFor } from "../routes";
// run() içinde:
const user = (await action()) as { role?: string };
navigate(roleHomeFor(user?.role));
```

(`login`/`register` `UserDto` döndürür — Task 4 imzaları bunu sağlıyor.)

`App.test.tsx`'i güncelle (router artık login'i açar):

```tsx
import { render, screen } from "@testing-library/react";
import App from "./App";

test("oturum yokken giriş ekranı açılır", () => {
  localStorage.clear();
  render(<App />);
  expect(screen.getAllByText("Giriş yap").length).toBeGreaterThan(0);
});
```

- [x] **Step 6: Testleri doğrula**

Run: `npm test -- --run` → Beklenen: tüm testler PASS (App + api + auth + routes).

- [x] **Step 7: Commit**

```bash
git add frontend/src/
git commit -m "feat(frontend): hash router, rol matrisi ve guard'lar (routes.js paritesi)"
```

---

### Task 6: AppShell — sidebar/header/tema/rozet/arama/toast paritesi

**Files:**
- Create: `frontend/src/layout/AppShell.tsx`, `frontend/src/layout/GlobalSearch.tsx`, `frontend/src/layout/ToastProvider.tsx`
- Modify: `frontend/src/routes.tsx` (RequireAuth çocuklarını AppShell'e sar), `frontend/src/App.tsx` (QueryClientProvider + ToastProvider)
- Test: `frontend/src/layout/AppShell.test.tsx`

**Interfaces:**
- Consumes: `useAuth`, `apiFetch`, `appRoutes/navGroups/navIcons/roleHomeFor`.
- Produces:
  - `AppShell`: eski `Layout()` DOM paritesi — `.app-container > aside.sidebar + header.header + main.main-content` içinde `<Outlet />`. Tema/sidebar tercihi localStorage (`ekipzen-theme`, `ekipzen-sidebar`) + `data-theme` attribute; rozet `GET /api/actions/badge` (TanStack Query, `{ openCount: number }`); rol değiştirici demo kimlikle **gerçek login** yapar; sayfa başlığı/eyebrow aktif rotadan.
  - `ToastProvider` + `useToast(): { showToast(message, type?) }` — eski `.toast-region/.toast` markup'ı; sonraki dilimlerin tümü kullanır.
  - `GlobalSearch`: bu dilimde yalnız **sayfa** sonuçları (rol-erişilebilir rota başlıkları); `/api/search` dilim 7'de eklenecek. Ctrl+K ve `/` kısayolu, ok tuşları + Enter, Escape kapatır.

- [x] **Step 1: Başarısız testi yaz** — `AppShell.test.tsx`:

```tsx
import { render, screen } from "@testing-library/react";
import { RouterProvider, createMemoryRouter } from "react-router-dom";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { beforeEach, expect, test, vi } from "vitest";
import { buildRouteObjects } from "../routes";
import { AuthProvider } from "../auth/AuthContext";
import { ToastProvider } from "./ToastProvider";
import { SESSION_KEY } from "../api/session";

const renderShellAt = (path: string, role: string, name: string) => {
  localStorage.setItem(
    SESSION_KEY,
    JSON.stringify({ token: "T", refreshToken: "R", user: { id: "u", name, email: "x@x", role, roleLabel: name, initials: "XX", employeeId: null } }),
  );
  vi.stubGlobal("fetch", vi.fn().mockResolvedValue(
    new Response(JSON.stringify({ openCount: 5 }), { status: 200, headers: { "Content-Type": "application/json" } }),
  ));
  const router = createMemoryRouter(buildRouteObjects(), { initialEntries: [path] });
  return render(
    <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
      <AuthProvider>
        <ToastProvider>
          <RouterProvider router={router} />
        </ToastProvider>
      </AuthProvider>
    </QueryClientProvider>,
  );
};

beforeEach(() => localStorage.clear());

test("hr-admin tüm menü gruplarını görür, başlık rotadan gelir", async () => {
  renderShellAt("/dashboard", "hr-admin", "İK Yöneticisi");
  expect(await screen.findByText("Risk Merkezi", { selector: ".header-title" })).toBeInTheDocument();
  expect(screen.getByText("Ayarlar")).toBeInTheDocument();
  expect(await screen.findByText("5", { selector: ".header-action-button span" })).toBeInTheDocument();
});

test("employee menüsünde yalnız yetkili modüller görünür", () => {
  renderShellAt("/overview", "employee", "Ahmet Yılmaz");
  expect(screen.queryByText("Ayarlar")).not.toBeInTheDocument();
  expect(screen.queryByText("Personel Yönetimi")).not.toBeInTheDocument();
  expect(screen.getByText("İzinlerim")).toBeInTheDocument();
});
```

Run: `npm test -- --run src/layout` → Beklenen: FAIL.

- [x] **Step 2: `ToastProvider.tsx` yaz**

```tsx
import { createContext, useCallback, useContext, useState, type ReactNode } from "react";

type ToastType = "success" | "error" | "warning" | "info";
type Toast = { id: number; message: string; type: ToastType };

const icons: Record<ToastType, string> = {
  success: "fa-circle-check", error: "fa-circle-xmark",
  warning: "fa-triangle-exclamation", info: "fa-circle-info",
};

const ToastContext = createContext<{ showToast: (message: string, type?: ToastType) => void } | null>(null);
let nextId = 1;

export function ToastProvider({ children }: { children: ReactNode }) {
  const [toasts, setToasts] = useState<Toast[]>([]);

  const showToast = useCallback((message: string, type: ToastType = "success") => {
    const id = nextId++;
    setToasts((current) => [...current, { id, message, type }]);
    setTimeout(() => setToasts((current) => current.filter((t) => t.id !== id)), 3200);
  }, []);

  return (
    <ToastContext.Provider value={{ showToast }}>
      {children}
      <div id="toast-region" className="toast-region" aria-live="polite">
        {toasts.map((toast) => (
          <div key={toast.id} className={`toast toast-${toast.type} visible`}>
            <i aria-hidden="true" className={`fa-solid ${icons[toast.type]}`} />
            <span>{toast.message}</span>
          </div>
        ))}
      </div>
    </ToastContext.Provider>
  );
}

export function useToast() {
  const value = useContext(ToastContext);
  if (!value) throw new Error("useToast, ToastProvider içinde kullanılmalı.");
  return value;
}
```

- [x] **Step 3: `GlobalSearch.tsx` yaz** (yalnız sayfa sonuçları; markup paritesi):

```tsx
import { useEffect, useRef, useState } from "react";
import { useNavigate } from "react-router-dom";
import { useAuth } from "../auth/AuthContext";
import { appRoutes, navIcons, type Role } from "../routes";

type Item = { label: string; hint: string; icon: string; path: string };

export function GlobalSearch() {
  const { user } = useAuth();
  const navigate = useNavigate();
  const [query, setQuery] = useState("");
  const [open, setOpen] = useState(false);
  const [activeIndex, setActiveIndex] = useState(0);
  const inputRef = useRef<HTMLInputElement>(null);
  const wrapRef = useRef<HTMLDivElement>(null);

  const items: Item[] = !query.trim()
    ? []
    : appRoutes
        .filter((r) => r.navKey === r.key && r.roles.includes((user?.role ?? "employee") as Role))
        .filter((r) => r.title.toLocaleLowerCase("tr-TR").includes(query.trim().toLocaleLowerCase("tr-TR")))
        .map((r) => ({ label: r.title, hint: "Sayfaya git", icon: navIcons[r.key] || "fa-compass", path: r.path }));

  useEffect(() => {
    const onKey = (event: KeyboardEvent) => {
      const isTyping = ["INPUT", "TEXTAREA", "SELECT"].includes(document.activeElement?.tagName ?? "");
      if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === "k") {
        event.preventDefault();
        inputRef.current?.focus();
      } else if (event.key === "/" && !isTyping) {
        event.preventDefault();
        inputRef.current?.focus();
      } else if (event.key === "Escape") {
        setOpen(false);
      }
    };
    const onClick = (event: MouseEvent) => {
      if (!wrapRef.current?.contains(event.target as Node)) setOpen(false);
    };
    document.addEventListener("keydown", onKey);
    document.addEventListener("click", onClick);
    return () => {
      document.removeEventListener("keydown", onKey);
      document.removeEventListener("click", onClick);
    };
  }, []);

  const select = (item: Item) => {
    navigate(item.path);
    setQuery("");
    setOpen(false);
  };

  return (
    <div className="header-search" ref={wrapRef}>
      <i aria-hidden="true" className="fa-solid fa-magnifying-glass" />
      <label className="sr-only" htmlFor="global-search-input">Personel, aksiyon veya sayfa ara</label>
      <input
        id="global-search-input"
        ref={inputRef}
        type="text"
        placeholder="Ara: personel, aksiyon, sayfa… (Ctrl+K)"
        autoComplete="off"
        role="combobox"
        aria-expanded={open && items.length > 0}
        aria-controls="global-search-results"
        value={query}
        onChange={(e) => { setQuery(e.target.value); setOpen(true); setActiveIndex(0); }}
        onFocus={() => setOpen(true)}
        onKeyDown={(e) => {
          if (e.key === "ArrowDown") { e.preventDefault(); setActiveIndex((i) => Math.min(i + 1, items.length - 1)); }
          if (e.key === "ArrowUp") { e.preventDefault(); setActiveIndex((i) => Math.max(i - 1, 0)); }
          if (e.key === "Enter" && items[activeIndex]) select(items[activeIndex]);
        }}
      />
      <div id="global-search-results" className="search-results" role="listbox" aria-label="Arama sonuçları" hidden={!open || items.length === 0}>
        {items.map((item, index) => (
          <button
            key={item.path}
            type="button"
            className={`search-result ${index === activeIndex ? "active" : ""}`}
            role="option"
            aria-selected={index === activeIndex}
            onClick={() => select(item)}
          >
            <i aria-hidden="true" className={`fa-solid ${item.icon}`} />
            <span>{item.label}</span>
            <small>{item.hint}</small>
          </button>
        ))}
      </div>
    </div>
  );
}
```

- [x] **Step 4: `AppShell.tsx` yaz** (Layout() paritesi):

```tsx
import { useEffect, useState } from "react";
import { NavLink, Outlet, useLocation, useNavigate } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { apiFetch } from "../api/client";
import { useAuth } from "../auth/AuthContext";
import { appRoutes, navGroups, navIcons, roleHomeFor, type AppRoute } from "../routes";
import { GlobalSearch } from "./GlobalSearch";

const DEMO_PASSWORD = "demo123";
const demoEmails: Record<string, string> = {
  "hr-admin": "ik@hrmaster.local",
  manager: "ece.arslan@hrmaster.local",
  employee: "ahmet.yilmaz@hrmaster.local",
};

export function AppShell() {
  const { user, login, logout } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const [theme, setTheme] = useState(() => localStorage.getItem("ekipzen-theme") || "light");
  const [collapsed, setCollapsed] = useState(() => localStorage.getItem("ekipzen-sidebar") === "collapsed");

  useEffect(() => {
    document.documentElement.setAttribute("data-theme", theme);
    localStorage.setItem("ekipzen-theme", theme);
  }, [theme]);
  useEffect(() => {
    localStorage.setItem("ekipzen-sidebar", collapsed ? "collapsed" : "expanded");
  }, [collapsed]);

  const badge = useQuery({
    queryKey: ["actions", "badge"],
    queryFn: () => apiFetch<{ openCount: number }>("/actions/badge"),
  });
  const actionCount = badge.data?.openCount ?? 0;

  const current: AppRoute | undefined = appRoutes.find((r) => r.path === location.pathname);
  const role = (user?.role ?? "employee") as AppRoute["roles"][number];
  const accessibleNav = appRoutes.filter((r) => r.navKey === r.key && r.roles.includes(role));

  const switchDemoRole = async (nextRole: string) => {
    await login(demoEmails[nextRole] ?? demoEmails["hr-admin"], DEMO_PASSWORD);
    navigate(roleHomeFor(nextRole));
  };

  const handleLogout = async () => {
    await logout();
    navigate("/login");
  };

  return (
    <div className={`app-container ${collapsed ? "sidebar-collapsed" : ""}`}>
      <aside className="sidebar">
        <div className="brand">
          <div className="brand-mark"><i aria-hidden="true" className="fa-solid fa-users-gear" /></div>
          <div>
            <strong>Ekipzen</strong>
            <span>HR MASTER Suite</span>
          </div>
        </div>
        <ul className="nav-links">
          {navGroups.map((group) => {
            const routes = group.keys
              .map((key) => accessibleNav.find((r) => r.key === key))
              .filter((r): r is AppRoute => Boolean(r));
            if (!routes.length) return null;
            return (
              <li key={group.label} style={{ display: "contents" }}>
                <span className="nav-group-label">{group.label}</span>
                {routes.map((route) => (
                  <NavLink
                    key={route.key}
                    to={route.path}
                    data-page={route.key}
                    className={() => `nav-item ${current?.navKey === route.key ? "active" : ""}`}
                  >
                    <i aria-hidden="true" className={`fa-solid ${navIcons[route.key]}`} /> <span>{route.title}</span>
                  </NavLink>
                ))}
              </li>
            );
          })}
        </ul>
        <div className="sidebar-insight">
          <span>Bugünkü öncelik</span>
          <strong>{actionCount} açık aksiyon</strong>
          <small>Risk, bordro ve uyum takibi</small>
        </div>
      </aside>

      <header className="header">
        <div className="header-tools">
          <button className="btn-icon shell-toggle" onClick={() => setCollapsed((c) => !c)} title="Menüyü daralt/genişlet" aria-label="Menüyü daralt veya genişlet">
            <i aria-hidden="true" className="fa-solid fa-bars-staggered" />
          </button>
          <button className="btn-icon theme-toggle" onClick={() => setTheme(theme === "dark" ? "light" : "dark")} title="Tema değiştir" aria-label="Açık/koyu tema değiştir">
            <i id="theme-icon" aria-hidden="true" className={`fa-solid ${theme === "dark" ? "fa-sun" : "fa-moon"}`} />
          </button>
        </div>
        <div className="header-heading">
          <span id="page-eyebrow">{current?.eyebrow ?? "Ekipzen"}</span>
          <div className="header-title" id="page-title">{current?.title ?? "Yakında"}</div>
        </div>
        <button className="header-action-button" onClick={() => navigate("/actions")} title="Aksiyon Merkezi" aria-label={`Aksiyon Merkezi: ${actionCount} açık aksiyon`}>
          <i aria-hidden="true" className="fa-solid fa-list-check" />
          <span>{actionCount}</span>
        </button>
        <GlobalSearch />
        <div className="user-profile">
          <div className="user-avatar" aria-hidden="true">{user?.initials}</div>
          <div>
            <strong>{user?.name}</strong>
            <span>{user?.roleLabel}</span>
          </div>
          <select className="role-switcher" value={role} onChange={(e) => switchDemoRole(e.target.value)} title="Demo rol değiştir" aria-label="Demo rol değiştir">
            <option value="hr-admin">İK Admin</option>
            <option value="manager">Yönetici</option>
            <option value="employee">Çalışan</option>
          </select>
          <button className="btn-icon-sm" onClick={handleLogout} title="Çıkış yap" aria-label="Çıkış yap">
            <i aria-hidden="true" className="fa-solid fa-arrow-right-from-bracket" />
          </button>
        </div>
      </header>

      <main className="main-content" id="main-content">
        <Outlet />
      </main>
    </div>
  );
}
```

- [x] **Step 5: Router'a bağla** — `routes.tsx` içinde `RequireAuth` elementini AppShell ile değiştir:

```tsx
import { AppShell } from "./layout/AppShell";
// buildRouteObjects içindeki korumalı blok şu hale gelir:
{
  element: <RequireAuth />,
  children: [
    {
      element: <AppShell />,
      children: appRoutes.map((route) => ({
        path: route.path,
        element: <GatedPage route={route} />,
      })),
    },
  ],
},
```

`App.tsx`'e provider'ları ekle:

```tsx
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { ToastProvider } from "./layout/ToastProvider";

const queryClient = new QueryClient();

export default function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <AuthProvider>
        <ToastProvider>
          <RouterProvider router={router} />
        </ToastProvider>
      </AuthProvider>
    </QueryClientProvider>
  );
}
```

Not: `routes.test.tsx` ve `AppShell.test.tsx` artık shell içinden geçer — `routes.test.tsx`'teki iki korumalı-rota testine QueryClientProvider + ToastProvider sarmalayıcısını ekle (AppShell.test.tsx'teki `renderShellAt` yardımcısının aynısını kullan) ve fetch stub'ı ekle (rozet isteği için).

- [x] **Step 6: Tüm testleri doğrula**

Run: `npm test -- --run` → Beklenen: tümü PASS.

- [x] **Step 7: Commit**

```bash
git add frontend/src/
git commit -m "feat(frontend): AppShell — sidebar/header/tema/rozet/arama/toast paritesi"
```

---

### Task 7: Uçtan uca duman testi + parite kontrolü

**Files:**
- Modify: yok (doğrulama görevi); gerekirse bulunan küçük hatalar düzeltilir.

- [x] **Step 1: Backend + frontend'i başlat**

Terminal 1: `cd backend && dotnet run --project src/Ekipzen.API --launch-profile http`
Terminal 2: `cd frontend && npm run dev` → `http://localhost:5173`

- [x] **Step 2: Üç rolle giriş duman testi**

1. `ik@hrmaster.local / demo123` → `#/dashboard` açılır; sidebar'da 4 grup, 10 modül; rozet > 0.
2. Rol değiştirici → "Çalışan" → `#/overview`; menüde yalnız Genel Durum/Aksiyonlar/İzinlerim/Bordro.
3. Çalışanken adres çubuğuna `#/settings` yaz → "Bu alan için yetki gerekli" ekranı (redirect yok).
4. Yanlış şifreyle giriş → form altında ProblemDetails mesajı.
5. Tema düğmesi → koyu tema; sayfa yenilenince kalıcı. Menü daralt → kalıcı.
6. Ctrl+K → arama; "izin" yaz → "İzinlerim" sonucu; Enter → sayfa.
7. Çıkış → `#/login`.

- [x] **Step 3: Görsel parite kontrolü**

Eski uygulamayı yan sekmede aç (kökteki `index.html` — canlı sunucu veya dosyadan). Login ekranı ve shell (sidebar/header) yan yana karşılaştır: renkler, boşluklar, ikonlar, yazı tipleri birebir olmalı. Fark bulunursa DOM/class farkı olarak düzelt (CSS'e dokunma).

- [x] **Step 4: Kapanış commit'i (varsa düzeltmelerle)**

```bash
git add frontend/
git commit -m "test(frontend): dilim 1 duman testi ve parite düzeltmeleri"
```

---

## Sonraki dilimler

Bu plan spec'in **Dilim 1**'ini kapsar. Dilim 2–8 (Overview+Dashboard, Personel, İzin+Puantaj, Bordro, İşe Alım+Uyum, Aksiyonlar+Arama+Ayarlar, kapanış) her biri kendi planıyla, bir önceki dilim bittikten sonra yazılır — sayfa portları bu iskeletteki `pageFor` eşlemesine gerçek component'lerini kaydeder ve `frontend-design` / `ui-ux-pro-max` becerileriyle parite kontrolünden geçer.
