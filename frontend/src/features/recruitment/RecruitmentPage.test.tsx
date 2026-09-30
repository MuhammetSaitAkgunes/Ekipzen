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
    "/api/recruitment/funnel": { total: 2, new: 1, interview: 1, offer: 0, rejected: 0, hired: 0 },
    "/api/candidates?status=Teklif": [{ ...candidates[0], status: "Teklif" }],
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

test("listede puansız aday ile sıfır puanlı aday ayrı gösterilir", async () => {
  stubApi({ "/api/candidates": [{ ...candidates[0], score: null }, { ...candidates[1], score: 0 }],
    "/api/candidates/5": { ...candidates[0], score: null, notes: [], evaluations: [] } });
  renderShell();
  expect(await screen.findByText("Henüz puan verilmedi")).toBeInTheDocument();
  expect(screen.getByText("0/100 puan")).toBeInTheDocument();
  expect(await screen.findByText("Genel puan henüz verilmedi")).toBeInTheDocument();
  expect(screen.queryByText("null/100 puan")).not.toBeInTheDocument();
});

test("günlük görev bağlantısı ilgili adayı açar", async () => {
  renderPage(
    <ToastProvider><RecruitmentPage /></ToastProvider>,
    ["/app/recruitment?candidateId=6"],
  );
  expect(await screen.findByRole("heading", { name: "Selin Koç", level: 2 })).toBeInTheDocument();
  expect(screen.getByRole("button", { name: /Selin Koç/ })).toHaveAttribute("aria-pressed", "true");
});

test("gerçek pozisyon listesi ve oluşturma girişi görünür", async () => {
  renderShell();
  expect(await screen.findByRole("heading", { name: "Pozisyonlar" })).toBeInTheDocument();
  expect(await screen.findByText("QA Engineer")).toBeInTheDocument();
  expect(screen.getByText(/2 kalan kadro · 1 bağlı aday/)).toBeInTheDocument();
  expect(screen.getByRole("button", { name: "QA Engineer kaydını düzelt" })).toBeInTheDocument();
  expect(screen.getByRole("button", { name: "Yeni pozisyon" })).toBeInTheDocument();
});

test("aşama özeti gerçek mevcut sayıları açıklar ve seçilen aşamaya filtreler", async () => {
  renderShell();
  expect(await screen.findByRole("heading", { name: "Aday aşamaları" })).toBeInTheDocument();
  expect(await screen.findByRole("button", { name: /Teklif: 0 aday/ })).toBeInTheDocument();
  expect(screen.getByText(/tarihsel geçiş veya işe alım dönüşüm oranı değildir/)).toBeInTheDocument();
  await userEvent.click(screen.getByRole("button", { name: /Teklif: 0 aday/ }));
  expect(screen.getByRole("status")).toHaveTextContent("teklif belgesinin hazırlanıp gönderildiğini");
  await waitFor(() => {
    expect(vi.mocked(fetch).mock.calls.some(([u]) => String(u) === "/api/candidates?status=Teklif")).toBe(true);
  });
});

test("filtre değişince önceki seçim listede yoksa başka aşamadan aday gösterilmez", async () => {
  vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL) => {
    const path = String(input);
    const body = path === "/api/candidates?status=Teklif"
      ? [{ ...candidates[0], status: "Teklif" }]
      : path === "/api/candidates"
        ? candidates
        : path === "/api/candidates/5"
          ? { ...candidates[0], skills: [], experiences: [], notes: [], evaluations: [], history: [] }
          : path === "/api/candidates/6"
            ? { ...candidates[1], skills: [], experiences: [], notes: [], evaluations: [], history: [] }
            : path === "/api/positions"
              ? []
              : { total: 2, new: 1, interview: 1, offer: 0, rejected: 0, hired: 0 };
    return new Response(JSON.stringify(body), { status: 200, headers: { "Content-Type": "application/json" } });
  }));
  renderShell();
  await screen.findByRole("button", { name: /Selin Koç/ });
  await userEvent.click(screen.getByRole("button", { name: /Selin Koç/ }));
  expect(await screen.findByRole("heading", { name: "Selin Koç", level: 2 })).toBeInTheDocument();
  await userEvent.click(screen.getByRole("button", { name: /Teklif: 0 aday/ }));
  expect(await screen.findByRole("heading", { name: "Burak Yılmaz", level: 2 })).toBeInTheDocument();
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
