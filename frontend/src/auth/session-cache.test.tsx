import { act, render, waitFor } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { beforeEach, expect, test } from "vitest";
import { AuthProvider } from "./AuthContext";
import { SESSION_KEY, clearSession, setSession, type Session } from "../api/session";

// Oturum önbelleği kullanıcıya özeldir: bir kullanıcının verisi, aynı tarayıcıda
// oturum açan bir sonrakine SIZMAMALIDIR (ortak bilgisayar / rol değiştirme).
const sessionFor = (id: string, name: string): Session => ({
  token: `T-${id}`,
  refreshToken: `R-${id}`,
  user: { id, name, email: `${id}@x.com`, role: "hr-admin", roleLabel: "İK Admin", initials: "XX", employeeId: null } as Session["user"],
});

const renderAuth = (queryClient: QueryClient) =>
  render(
    <QueryClientProvider client={queryClient}>
      <AuthProvider>
        <div />
      </AuthProvider>
    </QueryClientProvider>,
  );

beforeEach(() => localStorage.clear());

test("başka bir kullanıcı oturum açınca önceki kullanıcının önbelleği temizlenir", async () => {
  const queryClient = new QueryClient();
  localStorage.setItem(SESSION_KEY, JSON.stringify(sessionFor("u1", "Ayşe")));
  queryClient.setQueryData(["employees"], [{ id: 7, name: "Ayşe'nin personeli" }]);

  renderAuth(queryClient);

  // Rol değiştirme veya yeniden giriş: AppShell logout çağırmadan doğrudan login eder.
  setSession(sessionFor("u2", "Mehmet"));

  await waitFor(() => expect(queryClient.getQueryData(["employees"])).toBeUndefined());
});

test("çıkış yapınca önbellek temizlenir", async () => {
  const queryClient = new QueryClient();
  localStorage.setItem(SESSION_KEY, JSON.stringify(sessionFor("u1", "Ayşe")));
  queryClient.setQueryData(["payroll"], [{ net: 42000 }]);

  renderAuth(queryClient);

  clearSession();

  await waitFor(() => expect(queryClient.getQueryData(["payroll"])).toBeUndefined());
});

test("aynı kullanıcı oturumu tazelenince önbellek korunur", async () => {
  // Token yenileme (refresh) aynı kullanıcıdır; önbelleği boşaltmak gereksiz
  // ağ trafiği ve ekran titremesi yaratır.
  const queryClient = new QueryClient();
  localStorage.setItem(SESSION_KEY, JSON.stringify(sessionFor("u1", "Ayşe")));
  queryClient.setQueryData(["employees"], [{ id: 7 }]);

  renderAuth(queryClient);

  setSession({ ...sessionFor("u1", "Ayşe"), token: "YENI-TOKEN" });

  await new Promise((resolve) => setTimeout(resolve, 20));
  expect(queryClient.getQueryData(["employees"])).toBeDefined();
});

test.each(["role", "tenant"])("aynı kullanıcının %s kapsamı değişince önbellek temizlenir", async change => {
  const client = new QueryClient();
  const initial = { ...sessionFor("u1", "Ayşe"), user: { ...sessionFor("u1", "Ayşe").user, tenantId: 1 } };
  setSession(initial); client.setQueryData(["guidance", "operations"], [{ private: "önceki kapsam" }]);
  renderAuth(client);
  act(() => setSession({ ...initial, user: { ...initial.user, role: change === "role" ? "manager" : "hr-admin", tenantId: change === "tenant" ? 2 : 1 } }));
  await waitFor(() => expect(client.getQueryData(["guidance", "operations"])).toBeUndefined());
});

test("diğer sekmedeki oturum değişimi storage olayıyla önbelleği temizler", async () => {
  const client = new QueryClient(); setSession(sessionFor("u1", "Ayşe"));
  client.setQueryData(["employees"], [{ private: "Ayşe" }]); renderAuth(client);
  act(() => {
    localStorage.setItem(SESSION_KEY, JSON.stringify(sessionFor("u2", "Mehmet")));
    window.dispatchEvent(new StorageEvent("storage", { key: SESSION_KEY }));
  });
  await waitFor(() => expect(client.getQueryData(["employees"])).toBeUndefined());
});
