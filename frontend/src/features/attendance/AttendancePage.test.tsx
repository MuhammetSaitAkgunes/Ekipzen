import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, expect, test, vi } from "vitest";
import { stubApi } from "../../test/apiStub";
import { renderPage } from "../../test/renderPage";
import { ToastProvider } from "../../layout/ToastProvider";
import { AttendancePage } from "./AttendancePage";

const live = [
  { employeeId: 1, name: "Ahmet Yılmaz", initials: "AY", department: "Yazılım", checkIn: "08:45:00", status: "ontime" },
  { employeeId: 2, name: "Burak Demir", initials: "BD", department: "Satış", checkIn: null, status: "absent" },
];
const summary = [
  { employeeId: 1, employeeName: "Ahmet Yılmaz", department: "Yazılım", totalDays: 20, presentDays: 19, absentDays: 1, lateDays: 2, totalWorkedMinutes: 9600, totalOvertimeMinutes: 120 },
  { employeeId: 2, employeeName: "Burak Demir", department: "Satış", totalDays: 20, presentDays: 18, absentDays: 2, lateDays: 0, totalWorkedMinutes: 8400, totalOvertimeMinutes: 0 },
];
const employees = { items: [
  { id: 1, name: "Ahmet Yılmaz", title: "Dev", departmentId: 1, department: "Yazılım", status: "active", initials: "AY", hireDate: "2021-01-01", nationalIdMasked: "1" },
  { id: 2, name: "Burak Demir", title: "Satış", departmentId: 2, department: "Satış", status: "active", initials: "BD", hireDate: "2021-01-01", nationalIdMasked: "2" },
], total: 2, page: 1, pageSize: 50, totalPages: 1 };
const timesheet = {
  employeeId: 1, employeeName: "Ahmet Yılmaz", year: 2026, month: 7,
  rows: [
    { id: 11, workDate: "2026-07-01", type: "Tam", checkIn: "09:00:00", checkOut: "18:00:00", breakMinutes: 60, workedMinutes: 480, overtimeMinutes: 0, status: "ok", note: null },
    { id: 12, workDate: "2026-07-02", type: "Mesai", checkIn: "09:00:00", checkOut: "20:00:00", breakMinutes: 60, workedMinutes: 600, overtimeMinutes: 120, status: "overtime", note: null },
  ],
  totalWorkedMinutes: 1080, totalOvertimeMinutes: 120,
};

beforeEach(() =>
  stubApi({
    "/api/attendance/live": live,
    "/api/attendance/summary": summary,
    "/api/attendance": timesheet,
    "/api/employees": employees,
  }),
);
afterEach(() => vi.unstubAllGlobals());

const renderAttendance = () =>
  renderPage(
    <ToastProvider>
      <AttendancePage />
    </ToastProvider>,
  );

test("istatistik şeridi özet toplamlarından türetilir", async () => {
  renderAttendance();
  // 9600+8400=18000 dk = 300 saat; mesai 120 dk = 2 saat; geç kalan 1 kişi; devamsızlık 3 gün
  expect(await screen.findByText("300")).toBeInTheDocument();
  expect(screen.getByText("Geç Kalma").closest(".stat-box")).toHaveTextContent("1");
  expect(screen.getByText("Devamsızlık").closest(".stat-box")).toHaveTextContent("3");
});

test("canlı kartlar durum rozetiyle dolar", async () => {
  renderAttendance();
  expect(await screen.findByText("Zamanında")).toBeInTheDocument();
  expect(screen.getByText("Gelmedi", { selector: ".lc-badge" })).toBeInTheDocument();
  expect(screen.getByText("Manuel giriş ekle")).toBeInTheDocument();
});

test("puantaj sekmesi tabloyu ve aylık toplamı gösterir", async () => {
  renderAttendance();
  await screen.findByText("Zamanında");
  await userEvent.click(screen.getByRole("button", { name: /Aylık Puantaj/ }));
  expect(await screen.findByText("Fazla mesai")).toBeInTheDocument();
  expect(screen.getByText("18:00", { selector: ".text-blue" })).toBeInTheDocument(); // 1080 dk aylık toplam
});

test("rehber aylık puantaj sekmesine geçirir ve düzeltme sınırını açıklar", async () => {
  renderAttendance();
  await screen.findByText("Zamanında");
  expect(screen.getByRole("heading", { name: "Bir kaydı nasıl kontrol ederim?" })).toBeInTheDocument();
  expect(screen.getByText(/Aynı çalışan için aynı güne ikinci kayıt açılamaz/)).toBeInTheDocument();
  await userEvent.click(screen.getByRole("button", { name: "Aylık puantajı aç" }));
  expect(await screen.findByText("Fazla mesai")).toBeInTheDocument();
  await userEvent.click(screen.getByRole("button", { name: "Eksik günü kaydet" }));
  expect(screen.getByRole("heading", { name: "Manuel Puantaj Girişi" })).toBeInTheDocument();
});
