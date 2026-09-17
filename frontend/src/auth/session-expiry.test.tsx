import { render, screen, waitFor } from "@testing-library/react";
import { RouterProvider, createMemoryRouter } from "react-router-dom";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { afterEach, beforeEach, expect, test, vi } from "vitest";
import { buildRouteObjects } from "../routes";
import { AuthProvider } from "./AuthContext";
import { ToastProvider } from "../layout/ToastProvider";
import { SESSION_KEY } from "../api/session";

// Bayat oturum: token backend tarafından reddedilir, refresh de geçersizdir
// (ör. access token süresi dolmuş + refresh token DB'den silinmiş/süresi geçmiş).
const staleSession = JSON.stringify({
  token: "SURESI-DOLMUS",
  refreshToken: "GECERSIZ",
  user: { id: "u", name: "İK Yöneticisi", email: "ik@hrmaster.local", role: "hr-admin", roleLabel: "İK Admin", initials: "İK", employeeId: null },
});

const renderShellAt = (path: string) => {
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

beforeEach(() => {
  localStorage.clear();
  // Her uç (veri + refresh) 401 döner: oturum gerçekten düşmüştür.
  vi.stubGlobal("fetch", vi.fn().mockResolvedValue(
    new Response(JSON.stringify({ title: "Yetkisiz" }), { status: 401, headers: { "Content-Type": "application/json" } }),
  ));
});
afterEach(() => vi.unstubAllGlobals());

test("oturum 401 ile düşünce login ekranı gösterilir, sonsuz 'Yükleniyor'da kalınmaz", async () => {
  localStorage.setItem(SESSION_KEY, staleSession);

  renderShellAt("/app/dashboard");

  // Oturum düştüğü an kullanıcı login ekranına inmeli.
  await waitFor(() =>
    expect(screen.getAllByRole("button", { name: /Giriş yap/ }).length).toBeGreaterThan(0),
  );
  expect(screen.queryByText("Yükleniyor")).not.toBeInTheDocument();
});
