import { screen } from "@testing-library/react";
import { afterEach, beforeEach, expect, test, vi } from "vitest";
import { stubApi } from "../../test/apiStub";
import { renderPage } from "../../test/renderPage";
import { AttritionDetailPage } from "./AttritionDetailPage";
import { BurnoutDetailPage } from "./BurnoutDetailPage";

const employee = {
  employeeId: 1, name: "Ahmet Yılmaz", title: "Senior Developer", dept: "Yazılım", manager: "Ece Arslan",
  absence: 18, lateness: 22, overtime: 74, unusedLeave: 82, pulse: 52, performance: 68,
  roleCriticality: 92, riskScore: 71, attritionRisk: "high", burnoutRisk: "high",
  trend: "Fazla mesai artıyor", action: "1:1 görüşme planla",
};
const detail = {
  highCount: 2, mediumCount: 1, criticalRoleCount: 1,
  averagePulse: 61, averageOvertime: 60, averageUnusedLeave: 64,
  employees: [employee],
};

beforeEach(() => stubApi({ "/api/dashboard/attrition": detail, "/api/dashboard/burnout": detail }));
afterEach(() => vi.unstubAllGlobals());

test("ayrılma detayı KPI ve tablo satırını gösterir", async () => {
  renderPage(<AttritionDetailPage />);
  expect(await screen.findByText("Ayrılma Riski Detayı")).toBeInTheDocument();
  expect(screen.getByRole("link", { name: "Ahmet Yılmaz" })).toHaveAttribute("href", "/app/personnel/1");
  expect(screen.getByText("Kritik Rol Riski").closest(".stat-box")).toHaveTextContent("1");
});

test("tükenmişlik detayı ortalamaları ve seviye rozetini gösterir", async () => {
  renderPage(<BurnoutDetailPage />);
  expect(await screen.findByText("Tükenmişlik Sinyali")).toBeInTheDocument();
  expect(screen.getByText("Fazla Mesai Ort.").closest(".stat-box")).toHaveTextContent("60");
  expect(screen.getByText("74%")).toBeInTheDocument();
});
