import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, expect, test, vi } from "vitest";
import { stubApi } from "../../test/apiStub";
import { renderPage } from "../../test/renderPage";
import { AuthProvider } from "../../auth/AuthContext";
import { ToastProvider } from "../../layout/ToastProvider";
import { SESSION_KEY } from "../../api/session";
import { LeaveRequestModal } from "./LeaveRequestModal";

const types = [
  { id: 1, name: "Yıllık İzin", code: "YIL", deductsFromAnnualBalance: true, requiresApproval: true },
  { id: 2, name: "Raporlu", code: "RAP", deductsFromAnnualBalance: false, requiresApproval: false },
];
const balance = { year: 2026, entitledDays: 20, carriedOverDays: 4, usedDays: 10, remainingDays: 14 };
const preview = { workingDays: 1, deductsFromAnnualBalance: true, requiresApproval: true, canSubmit: true, blockReason: null, previewedAtUtc: "2026-07-01T08:00:00Z" };
const created = { id: 9, leaveTypeId: 1, leaveTypeName: "Yıllık İzin", startDate: "2026-08-01", endDate: "2026-08-03", days: 2, status: "pending" };

const setRole = (role: string) =>
  localStorage.setItem(SESSION_KEY, JSON.stringify({
    token: "T", refreshToken: "R",
    user: { id: "u", name: "X", email: "x@x", role, roleLabel: "X", initials: "XX", employeeId: 5 },
  }));

beforeEach(() => {
  localStorage.clear();
  stubApi({
    "/api/leaves/types": types,
    "/api/leaves/balance": balance,
    "/api/leaves": created,
    "/api/leaves/preview": preview,
    "/api/employees": { items: [{ id: 7, name: "Selin Koç", title: "UI", departmentId: 2, department: "Tasarım", status: "active", initials: "SK", hireDate: "2022-01-01", nationalIdMasked: "1" }], total: 1, page: 1, pageSize: 50, totalPages: 1 },
  });
});
afterEach(() => vi.unstubAllGlobals());

const renderModal = () =>
  renderPage(
    <AuthProvider>
      <ToastProvider>
        <LeaveRequestModal onClose={() => {}} />
      </ToastProvider>
    </AuthProvider>,
  );

test("izin türleri sunucu bayraklarıyla açıklanır ve başlangıçta hiçbiri seçili değildir", async () => {
  setRole("employee");
  renderModal();
  expect(await screen.findByText("Yıllık İzin")).toBeInTheDocument();
  expect(screen.getByText("Yıllık bakiye: 14 gün")).toBeInTheDocument();
  expect(screen.getByText("Yıllık bakiyeden düşmez")).toBeInTheDocument();
  expect(screen.getByText("Onay beklemeden oluşur")).toBeInTheDocument();
  expect(screen.getAllByRole("radio").every((radio) => !(radio as HTMLInputElement).checked)).toBe(true);
});

test("tarih seçilince takvim günü ön izlemesi yapılır; işe dönüş tahmin edilmez", async () => {
  setRole("employee");
  renderModal();
  await screen.findByText("Yıllık İzin");
  await userEvent.type(screen.getByLabelText("Başlangıç Tarihi"), "2026-08-01");
  await userEvent.type(screen.getByLabelText("Bitiş Tarihi"), "2026-08-03");
  expect(screen.getByText("3 gün")).toBeInTheDocument();
  expect(screen.queryByText("Tahmini işe dönüş")).not.toBeInTheDocument();
  expect(screen.getByText(/Takvim günü, izin bakiyesinden düşecek iş günü değildir/)).toBeInTheDocument();
});

test("talep gönderilince POST /leaves doğru gövdeyle gider", async () => {
  setRole("manager");
  renderModal();
  await screen.findByText("Yıllık İzin");
  await userEvent.click(screen.getByRole("radio", { name: /Yıllık İzin/ }));
  await userEvent.type(screen.getByLabelText("Başlangıç Tarihi"), "2026-08-01");
  await userEvent.type(screen.getByLabelText("Bitiş Tarihi"), "2026-08-03");
  await userEvent.selectOptions(await screen.findByLabelText("Yerine Bakacak Kişi"), "7");
  await userEvent.click(screen.getByRole("button", { name: "Bilgileri kontrol et" }));
  const review = await screen.findByRole("region", { name: "Göndermeden önce kontrol edin" });
  expect(review).toHaveTextContent("Selin Koç");
  expect(review).toHaveTextContent("İş günü: 1 gün");
  const previewPost = vi.mocked(fetch).mock.calls.find(([u, i]) => String(u) === "/api/leaves/preview" && i?.method === "POST");
  expect(JSON.parse(String(previewPost?.[1]?.body))).toMatchObject({ leaveTypeId: 1, substituteEmployeeId: 7 });
  expect(vi.mocked(fetch).mock.calls.some(([u, i]) => String(u) === "/api/leaves" && i?.method === "POST")).toBe(false);
  await userEvent.click(screen.getByRole("button", { name: "Talebi onayla ve gönder" }));
  await waitFor(() => {
    const posted = vi.mocked(fetch).mock.calls.find(
      ([u, i]) => String(u) === "/api/leaves" && i?.method === "POST",
    );
    expect(posted).toBeTruthy();
    const body = JSON.parse(String(posted![1]?.body));
    expect(body).toMatchObject({ leaveTypeId: 1, startDate: "2026-08-01", endDate: "2026-08-03", substituteEmployeeId: 7 });
  });
});

test("tarihler boşsa ön kontrol durur ve istek gitmez", async () => {
  setRole("employee");
  renderModal();
  await screen.findByText("Yıllık İzin");
  await userEvent.click(screen.getByRole("radio", { name: /Yıllık İzin/ }));
  await userEvent.click(screen.getByRole("button", { name: "Bilgileri kontrol et" }));
  expect(screen.getByRole("alert")).toHaveTextContent("Başlangıç ve bitiş tarihlerini doğrulayın");
  expect(vi.mocked(fetch).mock.calls.some(([u, i]) => String(u) === "/api/leaves" && i?.method === "POST")).toBe(false);
});

test("onay gerekmeyen türde sunucunun onaylı sonucunu doğru bildirir", async () => {
  stubApi({
    "/api/leaves/types": types,
    "/api/leaves/balance": balance,
    "/api/leaves": { ...created, leaveTypeId: 2, leaveTypeName: "Raporlu", status: "approved" },
    "/api/leaves/preview": { ...preview, deductsFromAnnualBalance: false, requiresApproval: false },
  });
  setRole("employee");
  renderModal();
  await screen.findByText("Raporlu");
  await userEvent.click(screen.getByRole("radio", { name: /Raporlu/ }));
  await userEvent.type(screen.getByLabelText("Başlangıç Tarihi"), "2026-08-01");
  await userEvent.type(screen.getByLabelText("Bitiş Tarihi"), "2026-08-03");
  await userEvent.click(screen.getByRole("button", { name: "Bilgileri kontrol et" }));
  expect(await screen.findByRole("region", { name: "Göndermeden önce kontrol edin" })).toHaveTextContent("doğrudan onaylanabilir");
  await userEvent.click(screen.getByRole("button", { name: "Talebi onayla ve gönder" }));
  expect(await screen.findByText("İzin talebi oluşturuldu ve onaylandı.")).toBeInTheDocument();
});


test("sunucu engellerse talep gönderilmez; tarih düzeltilip yeniden ön kontrol yapılabilir", async () => {
  stubApi({
    "/api/leaves/types": types,
    "/api/leaves/balance": balance,
    "/api/leaves": created,
    "/api/leaves/preview": { ...preview, canSubmit: false, blockReason: "Bu tarih aralığıyla çakışan bekleyen/onaylı bir talebiniz var." },
  });
  setRole("employee");
  renderModal();
  await screen.findByText("Yıllık İzin");
  await userEvent.click(screen.getByRole("radio", { name: /Yıllık İzin/ }));
  await userEvent.type(screen.getByLabelText("Başlangıç Tarihi"), "2026-08-01");
  await userEvent.type(screen.getByLabelText("Bitiş Tarihi"), "2026-08-03");
  await userEvent.click(screen.getByRole("button", { name: "Bilgileri kontrol et" }));
  expect(await screen.findByRole("alert")).toHaveTextContent("çakışan bekleyen/onaylı");
  expect(screen.queryByRole("button", { name: "Talebi onayla ve gönder" })).not.toBeInTheDocument();
  expect(vi.mocked(fetch).mock.calls.some(([u, i]) => String(u) === "/api/leaves" && i?.method === "POST")).toBe(false);
  stubApi({
    "/api/leaves/types": types,
    "/api/leaves/balance": balance,
    "/api/leaves": created,
    "/api/leaves/preview": preview,
  });
  await userEvent.clear(screen.getByLabelText("Bitiş Tarihi"));
  await userEvent.type(screen.getByLabelText("Bitiş Tarihi"), "2026-08-04");
  await userEvent.click(screen.getByRole("button", { name: "Bilgileri kontrol et" }));
  expect(await screen.findByRole("region", { name: "Göndermeden önce kontrol edin" })).toHaveTextContent("İş günü: 1 gün");
});
