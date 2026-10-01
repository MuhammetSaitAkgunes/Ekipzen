import { render, screen, waitFor } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { MemoryRouter } from "react-router-dom";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, expect, test, vi } from "vitest";
import { stubApi } from "../../test/apiStub";
import { renderPage } from "../../test/renderPage";
import { AuthProvider } from "../../auth/AuthContext";
import { ToastProvider } from "../../layout/ToastProvider";
import { SESSION_KEY } from "../../api/session";
import { CompliancePage } from "./CompliancePage";

const documents = [
  { id: 1, revision: "document-v1", employeeId: 3, employee: "Ahmet Yılmaz", dept: "Yazılım", document: "İş Sözleşmesi", owner: "Ayşe Demir", dueDate: "2026-07-20", dueLabel: "4 gün", status: "Süresi Yaklaşıyor", level: "high" },
  { id: 2, revision: "document-v2", employeeId: 4, employee: "Ece Arslan", dept: "Yazılım", document: "SGK Girişi", owner: null, dueDate: null, dueLabel: "-", status: "Eksik", level: "medium" },
];
const readiness = {
  totalCount: 12, completedCount: 7, missingCount: 3, dueSoonCount: 2, inReviewCount: 1, ownedCount: 9,
  documentComplianceScore: 82, readinessScore: 74, readinessRisk: "Orta",
  auditChecklist: [{ label: "Zorunlu evrak tamamlama", value: 78, level: "medium" }],
  recommendedActions: ["Eksik evraklar için sorumlu atayın."],
};

const setRole = (role: string) =>
  localStorage.setItem(SESSION_KEY, JSON.stringify({
    token: "T", refreshToken: "R",
    user: { id: "u", name: "X", email: "x@x", role, roleLabel: "X", initials: "XX", employeeId: 5 },
  }));

beforeEach(() => {
  localStorage.clear();
  setRole("hr-admin");
  stubApi({
    "/api/compliance/documents": documents,
    "/api/compliance/documents/1/status": documents[0],
    "/api/compliance/readiness": readiness,
  });
});
afterEach(() => vi.unstubAllGlobals());

const renderShell = () =>
  renderPage(
    <AuthProvider>
      <ToastProvider>
        <CompliancePage />
      </ToastProvider>
    </AuthProvider>,
  );

// "İş Sözleşmesi" ve "4 gün" hem tabloda hem Yaklaşan Son Tarihler panelinde
// geçer → tekil getByText çoklu eşleşmeyle düşer; getAllByText/findAllByText kullanılır.
test("KPI'lar readiness'tan, tablo belgelerden dolar", async () => {
  renderShell();
  expect(await screen.findByText("Uyum, Evrak ve Denetim Risk Merkezi")).toBeInTheDocument();
  expect((await screen.findAllByText("İş Sözleşmesi")).length).toBeGreaterThan(0);
  expect(screen.getByText("82")).toBeInTheDocument();
  expect(screen.getAllByText("Orta").length).toBeGreaterThan(0);
  expect(screen.getByText("Eksik evraklar için sorumlu atayın.")).toBeInTheDocument();
});

test("durum filtresi server-side sorgu atar", async () => {
  renderShell();
  await screen.findAllByText("İş Sözleşmesi");
  await userEvent.selectOptions(screen.getByLabelText("Durum filtresi"), "Eksik");
  await waitFor(() => {
    const hit = vi.mocked(fetch).mock.calls.some(([u]) => String(u) === "/api/compliance/documents?status=Eksik");
    expect(hit).toBe(true);
  });
});

test("yaklaşan son tarihler paneli dueDate'li belgelerden türetilir", async () => {
  renderShell();
  await screen.findAllByText("İş Sözleşmesi");
  expect(screen.getByText("Yaklaşan Son Tarihler")).toBeInTheDocument();
  expect(screen.getAllByText("4 gün").length).toBeGreaterThan(1);
});

test("hr-admin durum select'i PATCH status atar", async () => {
  renderShell();
  await screen.findAllByText("İş Sözleşmesi");
  await userEvent.selectOptions(screen.getAllByLabelText("Belge durumu")[0], "Tamamlandı");
  expect(screen.getByRole("group", { name: "Belge durum değişikliği ön kontrolü" })).toHaveTextContent("Ahmet Yılmaz · İş Sözleşmesi");
  expect(vi.mocked(fetch).mock.calls.some(([u, i]) => String(u) === "/api/compliance/documents/1/status" && i?.method === "PATCH")).toBe(false);
  await userEvent.click(screen.getByRole("button", { name: "Durumu kaydet" }));
  await waitFor(() => {
    const patched = vi.mocked(fetch).mock.calls.find(
      ([u, i]) => String(u) === "/api/compliance/documents/1/status" && i?.method === "PATCH",
    );
    expect(patched).toBeTruthy();
    expect(JSON.parse(String(patched![1]?.body))).toMatchObject({ status: "Tamamlandı" });
  });
});

test("manager salt-okur: Yeni Belge ve durum select'i yok", async () => {
  setRole("manager");
  renderShell();
  await screen.findAllByText("İş Sözleşmesi");
  expect(screen.queryByRole("button", { name: /Yeni Belge/ })).not.toBeInTheDocument();
  expect(screen.queryAllByLabelText("Belge durumu")).toHaveLength(0);
  await userEvent.click(screen.getByRole("button", { name: "İş Sözleşmesi belgesinin rehberini aç" }));
  const guideTitle = screen.getByRole("heading", { name: "Ahmet Yılmaz · İş Sözleşmesi" });
  expect(guideTitle).toBeInTheDocument();
  expect(guideTitle.closest("section")).toHaveFocus();
  expect(screen.getByText(/İK yöneticisine bildirin/)).toBeInTheDocument();
});

test("günlük iş bağlantısı ilgili belgeyi tabloda öne çıkarır", async () => {
  render(<QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
    <MemoryRouter initialEntries={["/app/risk/compliance?documentId=2"]}>
      <AuthProvider><ToastProvider><CompliancePage /></ToastProvider></AuthProvider>
    </MemoryRouter>
  </QueryClientProvider>);
  await screen.findAllByText("SGK Girişi");
  expect(screen.getAllByRole("row")[1]).toHaveAttribute("id", "document-2");
  expect(screen.getAllByRole("row")[1]).toHaveClass("ez-guidance-target");
  expect(screen.getByRole("heading", { name: "Ece Arslan · SGK Girişi" })).toBeInTheDocument();
  expect(screen.getByText(/durum değiştirmek dosya yüklemez/)).toBeInTheDocument();
});

test("durum değişikliğinden vazgeçmek sunucuya PATCH göndermez", async () => {
  renderShell();
  await screen.findAllByText("İş Sözleşmesi");
  await userEvent.selectOptions(screen.getAllByLabelText("Belge durumu")[0], "Tamamlandı");
  await userEvent.click(screen.getByRole("button", { name: "Vazgeç" }));
  expect(screen.queryByRole("group", { name: "Belge durum değişikliği ön kontrolü" })).not.toBeInTheDocument();
  expect(vi.mocked(fetch).mock.calls.some(([u, i]) => String(u) === "/api/compliance/documents/1/status" && i?.method === "PATCH")).toBe(false);
});

test("incelemede ön kontrolü günlük iş kartının kalkacağını açıklar", async () => {
  renderShell();
  await screen.findAllByText("İş Sözleşmesi");
  await userEvent.selectOptions(screen.getAllByLabelText("Belge durumu")[1], "İncelemede");
  expect(screen.getByRole("group", { name: "Belge durum değişikliği ön kontrolü" }))
    .toHaveTextContent("Bu seçim kaydı günlük iş listesinden çıkarır");
});
