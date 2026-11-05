import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, expect, test, vi } from "vitest";
import { AuthProvider } from "../../auth/AuthContext";
import { SESSION_KEY } from "../../api/session";
import { ToastProvider } from "../../layout/ToastProvider";
import { renderPage } from "../../test/renderPage";
import { stubApi } from "../../test/apiStub";
import { guidanceStepDrafts } from "../guidance/stepDrafts";
import { LeaveRequestModal } from "./LeaveRequestModal";
import { LeavesPage } from "./LeavesPage";

const source = { ...guidanceStepDrafts.find(x => x.contentKey === "leave-request-lifecycle")!,
  id: 71, contentVersion: 1, state: "published", isCurrent: true, reviewedOn: "2026-10-02", reviewDueOn: "2026-11-02" };
const type = { id: 1, name: "Yıllık İzin", code: "Yıllık", deductsFromAnnualBalance: true, requiresApproval: true };
const balance = { entitledDays: 24, carriedOverDays: 0, usedDays: 4, pendingDays: 3, remainingDays: 20 };
const leave = { id: 9, employeeId: 5, leaveTypeId: 1, leaveTypeName: "Yıllık İzin", startDate: "2026-10-05", endDate: "2026-10-07", days: 3, status: "pending" };
const preview = { workingDays: 3, deductsFromAnnualBalance: true, requiresApproval: true, canSubmit: true, recordToken: "create-token" };
const cancellation = { ...leave, canCancel: true, releasedReservationDays: 3, refundedDays: 0, recordToken: "cancel-token" };
const history = [{ isCurrent: true, evidence: { operationKey: "leave-request-create", resourceId: 9, outcome: "created", evidenceKey: "leave-request-create:1",
  occurredAtUtc: "2026-10-02T08:00:00Z", content: { contentRevisionId: 71, contentKey: "leave-request-lifecycle", processKey: "leave", stepKey: "request-lifecycle", contentVersion: 1, title: source.body.title },
  preparationChanges: { resourceKind: "leave-request", resourceId: 9, changedFields: ["Onay bekleyen talep", "Tür ve tarihler"] } } }];
beforeEach(() => {
  localStorage.clear(); localStorage.setItem(SESSION_KEY, JSON.stringify({ token: "T", refreshToken: "R", user: {
    id: "u", name: "Yeni personel", role: "employee", roleLabel: "Çalışan", initials: "YP", employeeId: 5,
  } }));
  stubApi({ "/api/leaves/types": [type], "/api/leaves/balance": balance, "/api/leaves/ledger": [], "/api/leaves/my": [leave],
    "/api/leaves/preview": preview, "/api/leaves": leave, "/api/leaves/9/cancellation-preview": cancellation, "/api/leaves/9/cancel": null,
    "/api/leaves/learning-operations": history, "/api/guidance/catalog": [source], "/api/guidance/catalog/versions/71": source,
  });
});
afterEach(() => { vi.unstubAllGlobals(); vi.restoreAllMocks(); localStorage.clear(); });
const render = (view: React.ReactNode) => renderPage(<AuthProvider><ToastProvider>{view}</ToastProvider></AuthProvider>);
async function chooseSource(label = "Gerçek işlemde kullandığım rehber sürümü") {
  const region = screen.queryByRole("region", { name: "Talebi iptal etmeden önce" });
  const scope = region ? within(region) : screen;
  await userEvent.click(scope.getByRole("button", { name: /^Rehber/ }));
  await userEvent.click(screen.getByText(label, { exact: true }));
  const details = screen.getByText(label, { exact: true }).closest("aside")!;
  await userEvent.click(await within(details).findByRole("button", { name: "Bu rehber sürümünü işlemime bağla" }));
  await userEvent.click(screen.getByRole("button", { name: "Kapat" }));
}
async function prepareRequest() {
  await userEvent.click(await screen.findByRole("radio", { name: /Yıllık İzin/ }));
  await userEvent.type(screen.getByLabelText("Başlangıç Tarihi"), "2026-10-05");
  await userEvent.type(screen.getByLabelText("Bitiş Tarihi"), "2026-10-07");
  await userEvent.click(screen.getByRole("button", { name: "Bilgileri kontrol et" }));
  await screen.findByRole("region", { name: "Göndermeden önce kontrol edin" });
}
const writes = (path: string) => vi.mocked(fetch).mock.calls.filter(([url, init]) => String(url).split("?")[0] === path && init?.method === "POST");

test("çalışan kendi dersini ve tokeni talebe bağlar; kaynak seçimi yapılmış ön kontrolü yeniletir", async () => {
  render(<LeaveRequestModal onClose={() => {}} />); await prepareRequest();
  await chooseSource();
  expect(screen.queryByRole("button", { name: "Talebi onayla ve gönder" })).not.toBeInTheDocument();
  expect(screen.getByRole("button", { name: "Rehber · seçildi" })).toBeInTheDocument();
  expect(vi.mocked(fetch).mock.calls.some(([url]) => String(url) === "/api/guidance/help-preferences" || String(url) === "/api/guidance/operations")).toBe(false);
  await userEvent.click(screen.getByRole("button", { name: "Bilgileri kontrol et" }));
  await screen.findByRole("region", { name: "Göndermeden önce kontrol edin" });
  await userEvent.click(screen.getByRole("button", { name: "Talebi onayla ve gönder" }));
  await waitFor(() => expect(writes("/api/leaves")).toHaveLength(1));
  expect(JSON.parse(String(writes("/api/leaves")[0][1]?.body))).toMatchObject({ expectedRecordToken: "create-token", guidanceContentRevisionId: 71 });
});

test("kaynak veya kayıt çakışmasında talep taslağı ve ders korunur; yeni ön kontrol olmadan tekrar gönderilmez", async () => {
  const base = globalThis.fetch;
  vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => String(input) === "/api/leaves" && init?.method === "POST"
    ? new Response(JSON.stringify({ title: "Seçilen rehber sürümü güncel değil." }), { status: 409 }) : base(input, init)));
  render(<LeaveRequestModal onClose={() => {}} />); await chooseSource(); await prepareRequest();
  await userEvent.click(screen.getByRole("button", { name: "Talebi onayla ve gönder" }));
  expect(await screen.findByRole("alert")).toHaveTextContent("güncel değil");
  expect(screen.getByLabelText("Başlangıç Tarihi")).toHaveValue("2026-10-05");
  expect(screen.getByRole("button", { name: "Rehber · seçildi" })).toBeInTheDocument();
  expect(screen.queryByRole("button", { name: "Talebi onayla ve gönder" })).not.toBeInTheDocument();
  expect(writes("/api/leaves")).toHaveLength(1);
});

test("iptal sunucu rezervasyonu ve iadeyi gösterir; ders seçince yeniden önizleme ve son onay ister", async () => {
  render(<LeavesPage />);
  await userEvent.click(await screen.findByRole("button", { name: "Yıllık İzin talebini iptal et" }));
  const region = screen.getByRole("region", { name: "Talebi iptal etmeden önce" });
  await waitFor(() => expect(within(region).getByRole("button", { name: "Talebi iptal etmeyi onayla" })).toBeEnabled());
  expect(region).toHaveTextContent("Kalkacak yıllık rezervasyon: 3 gün · Deftere iade: 0 gün");
  await chooseSource("İptalde kullandığım rehber sürümü");
  expect(within(region).getByRole("button", { name: "Talebi iptal etmeyi onayla" })).toBeDisabled();
  expect(writes("/api/leaves/9/cancel")).toHaveLength(0);
  await userEvent.click(within(region).getByRole("button", { name: "İptal etkisini yeniden kontrol et" }));
  await waitFor(() => expect(within(region).getByRole("button", { name: "Talebi iptal etmeyi onayla" })).toBeEnabled());
  await userEvent.click(within(region).getByRole("button", { name: "Talebi iptal etmeyi onayla" }));
  await waitFor(() => expect(writes("/api/leaves/9/cancel")).toHaveLength(1));
  expect(String(writes("/api/leaves/9/cancel")[0][0])).toContain("expectedRecordToken=cancel-token&guidanceContentRevisionId=71");
});

test("iptal çakışması seçilmiş dersi korur ve eski önizlemeyle yeniden yazmayı engeller", async () => {
  const base = globalThis.fetch;
  vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => String(input).startsWith("/api/leaves/9/cancel?") && init?.method === "POST"
    ? new Response(JSON.stringify({ title: "Kayıt siz incelerken değişti." }), { status: 409 }) : base(input, init)));
  render(<LeavesPage />); await userEvent.click(await screen.findByRole("button", { name: "Yıllık İzin talebini iptal et" }));
  await chooseSource("İptalde kullandığım rehber sürümü");
  await userEvent.click(screen.getByRole("button", { name: "İptal etkisini yeniden kontrol et" }));
  await waitFor(() => expect(screen.getByRole("button", { name: "Talebi iptal etmeyi onayla" })).toBeEnabled());
  await userEvent.click(screen.getByRole("button", { name: "Talebi iptal etmeyi onayla" }));
  expect(await screen.findByRole("alert")).toHaveTextContent("değişti");
  expect(screen.getByRole("button", { name: "Talebi iptal etmeyi onayla" })).toBeDisabled();
  expect(screen.getByRole("button", { name: "Rehber · seçildi" })).toBeInTheDocument();
  expect(writes("/api/leaves/9/cancel")).toHaveLength(1);
});

test("kişisel geçmiş gerçek kaynak sürümünü gösterir; okumak yönetim API'sine erişmez veya işlem üretmez", async () => {
  render(<LeavesPage />);
  await userEvent.click(await screen.findByRole("button", { name: "Rehber" }));
  await userEvent.click(await screen.findByText("Kendi talep ve iptal işlem kanıtlarım"));
  expect(await screen.findByText("Talep oluşturuldu")).toBeInTheDocument();
  expect(screen.getByRole("link", { name: /Kendi izin türünü, talebini ve iptalini izleme · Sürüm 1/ })).toHaveAttribute("href", "/app/guidance/catalog?version=71");
  expect(vi.mocked(fetch).mock.calls.some(([, init]) => init?.method === "POST")).toBe(false);
  expect(vi.mocked(fetch).mock.calls.some(([url]) => String(url) === "/api/guidance/operations")).toBe(false);
});
