import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { afterEach, expect, test, vi } from "vitest";
import { ToastProvider } from "../../layout/ToastProvider";
import { stubApi } from "../../test/apiStub";
import { ApprovalsList } from "./ApprovalsList";

afterEach(() => vi.unstubAllGlobals());

test("günlük iş bağlantısı ilgili izin talebini listenin başında açık gösterir", async () => {
  stubApi({
    "/api/leaves/pending": [
      { id: 1, employeeName: "Ece Arslan", leaveTypeName: "Yıllık İzin", days: 2, startDate: "2026-10-01", endDate: "2026-10-02", description: "Birinci talep" },
      { id: 8, employeeName: "Ahmet Yılmaz", leaveTypeName: "Mazeret", days: 1, startDate: "2026-09-30", endDate: "2026-09-30", description: "İlgili talep açıklaması" },
    ],
  });
  render(<QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
    <MemoryRouter initialEntries={["/app/inbox?tab=approvals&requestId=8"]}>
      <ToastProvider><ApprovalsList /></ToastProvider>
    </MemoryRouter>
  </QueryClientProvider>);

  const cards = await screen.findAllByRole("article");
  expect(cards[0]).toHaveAttribute("id", "request-8");
  expect(within(cards[0]).getByText("İlgili talep açıklaması")).toBeInTheDocument();
  expect(within(cards[0]).getByRole("button", { name: "Kapat" })).toBeInTheDocument();
});


test("onay için karar özeti görülmeden işlem gönderilmez; not karar kaydına aktarılır", async () => {
  stubApi({
    "/api/leaves/pending": [
      { id: 8, employeeName: "Ahmet Yılmaz", leaveTypeName: "Yıllık İzin", days: 2,
        startDate: "2026-09-30", endDate: "2026-10-01", description: "Aile ziyareti" },
    ],
    "/api/leaves/8/approve": { id: 8, status: "approved" },
  });
  render(<QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
    <MemoryRouter initialEntries={["/app/inbox?requestId=8"]}>
      <ToastProvider><ApprovalsList /></ToastProvider>
    </MemoryRouter>
  </QueryClientProvider>);

  await userEvent.click(await screen.findByRole("button", { name: "Onayı incele" }));
  expect(screen.getByText(/sistem onay anında bakiyeyi yeniden kontrol eder/)).toBeInTheDocument();
  expect(vi.mocked(fetch).mock.calls.some(([url, init]) =>
    String(url) === "/api/leaves/8/approve" && init?.method === "POST")).toBe(false);

  await userEvent.click(screen.getByRole("button", { name: "Vazgeç" }));
  expect(screen.queryByRole("button", { name: "Onayı kaydet" })).not.toBeInTheDocument();
  await userEvent.click(screen.getByRole("button", { name: "Onayı incele" }));
  await userEvent.type(screen.getByLabelText("Karar notu (isteğe bağlı)"), "Tarihler ve ekip planı kontrol edildi.");
  await userEvent.click(screen.getByRole("button", { name: "Onayı kaydet" }));
  await waitFor(() => expect(vi.mocked(fetch).mock.calls.some(([url, init]) =>
    String(url) === "/api/leaves/8/approve" && init?.method === "POST"
      && JSON.parse(String(init.body)).note === "Tarihler ve ekip planı kontrol edildi.")).toBe(true));
});

test("ret kararı da ayrı inceleme ve açık kaydetme gerektirir", async () => {
  stubApi({
    "/api/leaves/pending": [
      { id: 8, employeeName: "Ahmet Yılmaz", leaveTypeName: "Mazeret", days: 1,
        startDate: "2026-09-30", endDate: "2026-09-30", description: null },
    ],
    "/api/leaves/8/reject": { id: 8, status: "rejected" },
  });
  render(<QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
    <MemoryRouter initialEntries={["/app/inbox?requestId=8"]}>
      <ToastProvider><ApprovalsList /></ToastProvider>
    </MemoryRouter>
  </QueryClientProvider>);

  await userEvent.click(await screen.findByRole("button", { name: "Reddetmeyi incele" }));
  expect(screen.getByText(/Bu ekranda kararı geri alma işlemi yoktur/)).toBeInTheDocument();
  expect(vi.mocked(fetch).mock.calls.some(([url, init]) =>
    String(url) === "/api/leaves/8/reject" && init?.method === "POST")).toBe(false);
  await userEvent.click(screen.getByRole("button", { name: "Ret kararını kaydet" }));
  await waitFor(() => expect(vi.mocked(fetch).mock.calls.some(([url, init]) =>
    String(url) === "/api/leaves/8/reject" && init?.method === "POST"
      && JSON.parse(String(init.body)).note === null)).toBe(true));
});


test("karar çakışırsa panel ve not korunur; tamamlanmış gibi gösterilmez", async () => {
  vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL) => {
    const path = String(input);
    if (path === "/api/leaves/pending") return new Response(JSON.stringify([
      { id: 8, employeeName: "Ahmet Yılmaz", leaveTypeName: "Yıllık İzin", days: 2,
        startDate: "2026-09-30", endDate: "2026-10-01", description: "Aile ziyareti" },
    ]), { status: 200, headers: { "Content-Type": "application/json" } });
    if (path === "/api/leaves/8/approve") return new Response(
      JSON.stringify({ title: "Talep artık beklemiyor." }),
      { status: 409, headers: { "Content-Type": "application/json" } },
    );
    throw new Error(`Beklenmeyen API: ${path}`);
  }));
  render(<QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
    <MemoryRouter initialEntries={["/app/inbox?requestId=8"]}>
      <ToastProvider><ApprovalsList /></ToastProvider>
    </MemoryRouter>
  </QueryClientProvider>);

  await userEvent.click(await screen.findByRole("button", { name: "Onayı incele" }));
  await userEvent.type(screen.getByLabelText("Karar notu (isteğe bağlı)"), "Ekip planı kontrol edildi.");
  await userEvent.click(screen.getByRole("button", { name: "Onayı kaydet" }));
  expect(await screen.findByRole("alert")).toHaveTextContent("Talep artık beklemiyor.");
  expect(screen.getByLabelText("Karar notu (isteğe bağlı)")).toHaveValue("Ekip planı kontrol edildi.");
  expect(screen.getByRole("button", { name: "Onayı kaydet" })).toBeEnabled();
});
