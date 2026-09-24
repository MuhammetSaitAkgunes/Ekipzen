import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, within } from "@testing-library/react";
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
