import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, expect, test, vi } from "vitest";
import { stubApi } from "../../test/apiStub";
import { renderPage } from "../../test/renderPage";
import { ToastProvider } from "../../layout/ToastProvider";
import { RecruitmentPage } from "./RecruitmentPage";

const candidates = [
  { id: 5, name: "Burak Yılmaz", appliedRole: "Senior Frontend Developer", status: "Mülakat", score: 92, initials: "BY", appliedAtUtc: "2026-07-16T10:00:00Z" },
  { id: 6, name: "Selin Koç", appliedRole: "UI/UX Designer", status: "Yeni", score: 85, initials: "SK", appliedAtUtc: "2026-07-15T09:00:00Z" },
];

beforeEach(() =>
  stubApi({
    "/api/candidates": candidates,
    "/api/positions": [{ id: 12, title: "QA Engineer", department: "Ürün", isOpen: true, openCount: 2, candidateCount: 1 }],
    "/api/candidates/5": { ...candidates[0], skills: [], experiences: [], notes: [], evaluations: [], history: [] },
    "/api/candidates/6": { ...candidates[1], skills: [], experiences: [], notes: [], evaluations: [], history: [] },
  }),
);
afterEach(() => vi.unstubAllGlobals());

const renderShell = () =>
  renderPage(
    <ToastProvider>
      <RecruitmentPage />
    </ToastProvider>,
  );

test("aday listesi skor ve durum etiketleriyle dolar", async () => {
  renderShell();
  expect(await screen.findByText("Burak Yılmaz")).toBeInTheDocument();
  expect(screen.getByText("92/100 puan")).toBeInTheDocument();
  expect(screen.getByText("Selin Koç")).toBeInTheDocument();
  expect(screen.getByText("Aday Havuzu")).toBeInTheDocument();
});

test("gerçek pozisyon listesi ve oluşturma girişi görünür", async () => {
  renderShell();
  expect(await screen.findByRole("heading", { name: "Açık pozisyonlar" })).toBeInTheDocument();
  expect(await screen.findByText("QA Engineer")).toBeInTheDocument();
  expect(screen.getByText(/2 açık kadro · 1 bağlı aday/)).toBeInTheDocument();
  expect(screen.getByRole("button", { name: "Yeni pozisyon" })).toBeInTheDocument();
});

test("durum filtre sekmesi server-side sorgu atar", async () => {
  renderShell();
  await screen.findByText("Burak Yılmaz");
  await userEvent.click(screen.getByRole("button", { name: "Yeni" }));
  await waitFor(() => {
    const hit = vi.mocked(fetch).mock.calls.some(([u]) => String(u) === "/api/candidates?status=Yeni");
    expect(hit).toBe(true);
  });
});

test("arama 300ms debounce ile server-side gider", async () => {
  renderShell();
  await screen.findByText("Burak Yılmaz");
  await userEvent.type(screen.getByLabelText("Aday ara"), "burak");
  await waitFor(() => {
    const hit = vi.mocked(fetch).mock.calls.some(([u]) => String(u) === "/api/candidates?search=burak");
    expect(hit).toBe(true);
  });
});

test("aday yoksa boş durum ve Yeni Aday butonu görünür", async () => {
  stubApi({ "/api/candidates": [] });
  renderShell();
  expect(await screen.findByText(/Henüz aday yok/)).toBeInTheDocument();
  expect(screen.getByRole("button", { name: /Yeni Aday/ })).toBeInTheDocument();
});

test("aday kartı klavyeyle seçilir ve ayrıntı açılır", async () => {
  renderShell();
  const candidate = await screen.findByRole("button", { name: /Selin Koç/ });
  candidate.focus();
  await userEvent.keyboard("{Enter}");
  expect(candidate).toHaveAttribute("aria-pressed", "true");
  expect(await screen.findByRole("heading", { name: "Selin Koç", level: 2 })).toBeInTheDocument();
});
