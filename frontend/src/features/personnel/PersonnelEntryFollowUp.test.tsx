import { screen } from "@testing-library/react";
import { afterEach, expect, test, vi } from "vitest";
import { stubApi } from "../../test/apiStub";
import { renderPage } from "../../test/renderPage";
import { PersonnelEntryFollowUp } from "./PersonnelEntryFollowUp";

afterEach(() => vi.unstubAllGlobals());

const renderFollowUp = () => renderPage(
  <PersonnelEntryFollowUp employeeId={42} verifiedCreation={false}
    onOpenDocuments={() => {}} onOpenCompensation={() => {}} />,
);

test("yüklenen dosya ve geçmiş dönemin ücretini işe girişin tamamı saymaz", async () => {
  stubApi({
    "/api/employees/42/documents": [{ id: 3, documentType: "Sözleşme", fileName: "sozlesme.pdf" }],
    "/api/employees/42/employment-foundation": {
      employeeId: 42,
      employmentPeriods: [
        { id: 1, startDate: "2024-01-01", endDate: "2025-12-31" },
        { id: 2, startDate: "2026-01-01", endDate: null },
      ],
      compensationPackages: [{ id: 9, employmentPeriodId: 1, effectiveFrom: "2024-01-01" }],
    },
  });
  renderFollowUp();
  expect(await screen.findByText(/1 dosya yüklenmiş/)).toBeInTheDocument();
  expect(await screen.findByText("Etkin çalışma dönemi için ücret paketi kaydı bulunmuyor.")).toBeInTheDocument();
  expect(screen.getByText(/tüm işe giriş yükümlülüklerinin tamamlandığını kanıtlamaz/)).toBeInTheDocument();
});

test("etkin dönemin ücret paketi kaydını gösterir ama içeriğini doğrulanmış saymaz", async () => {
  stubApi({
    "/api/employees/42/documents": [],
    "/api/employees/42/employment-foundation": {
      employeeId: 42,
      employmentPeriods: [{ id: 2, startDate: "2026-01-01", endDate: null }],
      compensationPackages: [{ id: 10, employmentPeriodId: 2, effectiveFrom: "2026-01-01" }],
    },
  });
  renderFollowUp();
  expect(await screen.findByText(/1 ücret paketi kaydı var/)).toBeInTheDocument();
  expect(screen.getByText(/Yürürlük tarihini, tutarı ve kaynak referansını kontrol edin/)).toBeInTheDocument();
});
