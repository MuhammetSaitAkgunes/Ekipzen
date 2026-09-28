import { screen } from "@testing-library/react";
import { expect, test } from "vitest";
import { renderPage } from "../../test/renderPage";
import { DailyReviewRoute } from "./DailyReviewRoute";

test("İK yöneticisinin günlük kontrol rotası bütün ilgili süreçleri açar", () => {
  renderPage(<DailyReviewRoute isHrAdmin />);
  expect(screen.getByRole("heading", { name: "Günlük kontrol rotası" })).toBeInTheDocument();
  expect(screen.getByText(/Otomatik görev, son tarih veya tamamlanma kanıtı değildir/)).toBeInTheDocument();
  expect(screen.getByRole("link", { name: /Adaylar ve pozisyonlar ekranını aç/ }))
    .toHaveAttribute("href", "/app/recruitment");
  expect(screen.getByRole("link", { name: /Bordro dönemi ekranını aç/ }))
    .toHaveAttribute("href", "/app/payroll");
  expect(screen.getByRole("link", { name: /İzin kararları ekranını aç/ }))
    .toHaveAttribute("href", "/app/inbox?tab=approvals");
  expect(screen.getAllByRole("listitem")).toHaveLength(7);
});

test("yöneticiye yalnız erişebildiği günlük kontrol ekranları gösterilir", () => {
  renderPage(<DailyReviewRoute isHrAdmin={false} />);
  expect(screen.getByRole("link", { name: /Çalışan kayıtları ekranını aç/ }))
    .toHaveAttribute("href", "/app/personnel");
  expect(screen.queryByRole("link", { name: /Adaylar ve pozisyonlar ekranını aç/ })).not.toBeInTheDocument();
  expect(screen.queryByRole("link", { name: /Bordro dönemi ekranını aç/ })).not.toBeInTheDocument();
  expect(screen.getAllByRole("listitem")).toHaveLength(5);
});
