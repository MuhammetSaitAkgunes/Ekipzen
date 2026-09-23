import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { expect, test } from "vitest";
import { plans } from "../content";
import { ContactPage } from "./ContactPage";

const renderAt = (path: string) =>
  render(
    <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
      <MemoryRouter initialEntries={[path]}>
        <ContactPage />
      </MemoryRouter>
    </QueryClientProvider>,
  );

test("İletişim: paket parametresi mesaj alanını önceden doldurur", () => {
  renderAt("/iletisim?paket=buyuyen");
  const plan = plans.find((p) => p.id === "buyuyen")!;
  expect(screen.getByLabelText(/Mesajınız/)).toHaveValue(`${plan.name} paketi için teklif almak istiyorum.`);
});

test("İletişim: paket parametresi olmadan mesaj alanı boş gelir", () => {
  renderAt("/iletisim");
  expect(screen.getByLabelText(/Mesajınız/)).toHaveValue("");
});

test("demo formu yardımcı içerikten önce ve yalnız bir kez sunulur", () => {
  const { container } = renderAt("/iletisim");
  const form = container.querySelector("form")!;
  const followup = screen.getByRole("heading", { name: "Demo görüşmesinin kapsamı" });
  expect(container.querySelectorAll("form")).toHaveLength(1);
  expect(form.compareDocumentPosition(followup) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
});
