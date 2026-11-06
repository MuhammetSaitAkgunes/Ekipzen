import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, expect, test, vi } from "vitest";
import { stubApi } from "../test/apiStub";
import { renderPage } from "../test/renderPage";
import { AuthProvider } from "../auth/AuthContext";
import { SESSION_KEY } from "../api/session";
import { useLocation } from "react-router-dom";
import { GlobalSearch } from "./GlobalSearch";

const results = [
  { type: "personnel", label: "Ahmet Yılmaz", hint: "Senior Developer · Yazılım", routeKey: "personnel", entityId: 3 },
  { type: "action", label: "SGK matrah kontrolü", hint: "Bordro aksiyonu", routeKey: "actions", entityId: 1 },
];

beforeEach(() => {
  localStorage.clear();
  localStorage.setItem(SESSION_KEY, JSON.stringify({
    token: "T", refreshToken: "R",
    user: { id: "u", name: "X", email: "x@x", role: "hr-admin", roleLabel: "X", initials: "XX", employeeId: 5 },
  }));
  stubApi({ "/api/search": results });
});
afterEach(() => vi.unstubAllGlobals());

test("arama düğmesi sorguyu koruyarak kapanıp açılır ve klavyeyle kayıt seçilir", async () => {
  renderPage(<AuthProvider><GlobalSearch /></AuthProvider>);
  await userEvent.click(screen.getByRole("button", { name: "Genel aramayı aç" }));
  const input = screen.getByRole("combobox");
  expect(input).toHaveFocus();
  await userEvent.type(input, "ahmet");
  await screen.findByRole("option", { name: /Ahmet Yılmaz/ });
  expect(input).toHaveAttribute("aria-activedescendant", "global-search-option-0");
  await userEvent.click(screen.getByRole("button", { name: "Genel aramayı kapat" }));
  expect(input).toHaveAttribute("aria-expanded", "false");
  await userEvent.click(screen.getByRole("button", { name: "Genel aramayı aç" }));
  expect(input).toHaveValue("ahmet");
  expect(input).toHaveFocus();
  await userEvent.keyboard("{Enter}");
  expect(input).toHaveValue("");
  expect(screen.getByRole("button", { name: "Genel aramayı aç" })).toHaveAttribute("aria-expanded", "false");
});

test("API sonuçları debounce sonrası listelenir", async () => {
  renderPage(
    <AuthProvider>
      <GlobalSearch />
    </AuthProvider>,
  );
  await userEvent.type(screen.getByLabelText("Personel, aksiyon veya sayfa ara"), "ahmet");
  expect(await screen.findByText("Ahmet Yılmaz")).toBeInTheDocument();
  expect(screen.getByText("Senior Developer · Yazılım")).toBeInTheDocument();
  await waitFor(() => {
    const hit = vi.mocked(fetch).mock.calls.some(([u]) => String(u).startsWith("/api/search?q=ahmet"));
    expect(hit).toBe(true);
  });
});

test("iki karakterden kısa sorguda API çağrısı yapılmaz", async () => {
  renderPage(
    <AuthProvider>
      <GlobalSearch />
    </AuthProvider>,
  );
  await userEvent.type(screen.getByLabelText("Personel, aksiyon veya sayfa ara"), "a");
  await new Promise((resolve) => setTimeout(resolve, 400));
  expect(vi.mocked(fetch).mock.calls.some(([u]) => String(u).includes("/api/search"))).toBe(false);
});

test("personel sonucu profil rotasına, aksiyon sonucu iş kutusuna gider", async () => {
  const Location = () => <span data-testid="location">{useLocation().pathname + useLocation().search}</span>;
  renderPage(<AuthProvider><GlobalSearch /><Location /></AuthProvider>);
  const search = screen.getByLabelText("Personel, aksiyon veya sayfa ara");
  await userEvent.type(search, "ahmet");
  await userEvent.click(await screen.findByRole("option", { name: /Ahmet Yılmaz/ }));
  expect(screen.getByTestId("location")).toHaveTextContent("/app/personnel/3");
  await userEvent.type(search, "sgk");
  await userEvent.click(await screen.findByRole("option", { name: /SGK matrah kontrolü/ }));
  expect(screen.getByTestId("location")).toHaveTextContent("/app/inbox?tab=actions&actionId=1");
});
