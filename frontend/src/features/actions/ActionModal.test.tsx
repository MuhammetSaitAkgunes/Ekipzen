import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, expect, test, vi } from "vitest";
import { stubApi } from "../../test/apiStub";
import { renderPage } from "../../test/renderPage";
import { ToastProvider } from "../../layout/ToastProvider";
import { ActionModal } from "./ActionModal";
import { AuthProvider } from "../../auth/AuthContext";

beforeEach(() =>
  stubApi({
    "/api/actions": { id: 7, title: "Yeni görev", source: "Bordro", owner: "İK", due: "Bugün", priority: "high", status: "open", action: "Denetle." },
  }),
);
afterEach(() => vi.unstubAllGlobals());

test("form doldurulup kaydedilince POST actions tam gövdeyle gider", async () => {
  renderPage(
    <AuthProvider><ToastProvider>
      <ActionModal onClose={() => {}} />
    </ToastProvider></AuthProvider>,
  );
  await userEvent.type(screen.getByLabelText("Başlık"), "Yeni görev");
  await userEvent.type(screen.getByLabelText("Kaynak"), "Bordro");
  await userEvent.type(screen.getByLabelText("Sahip"), "İK");
  await userEvent.clear(screen.getByLabelText("Vade etiketi"));
  await userEvent.type(screen.getByLabelText("Vade etiketi"), "Bugün");
  await userEvent.selectOptions(screen.getByLabelText("Öncelik"), "high");
  await userEvent.type(screen.getByLabelText("Önerilen aksiyon"), "Denetle.");
  await userEvent.click(screen.getByRole("button", { name: /Kaydet/ }));
  await waitFor(() => {
    const posted = vi.mocked(fetch).mock.calls.find(
      ([u, i]) => String(u) === "/api/actions" && i?.method === "POST",
    );
    expect(posted).toBeTruthy();
    expect(JSON.parse(String(posted![1]?.body))).toMatchObject({
      title: "Yeni görev", source: "Bordro", owner: "İK", due: "Bugün",
      priority: "high", recommendedAction: "Denetle.",
    });
  });
});

test("başlık boşsa form hatası gösterilir, istek atılmaz", async () => {
  renderPage(
    <AuthProvider><ToastProvider>
      <ActionModal onClose={() => {}} />
    </ToastProvider></AuthProvider>,
  );
  await userEvent.click(screen.getByRole("button", { name: /Kaydet/ }));
  expect(await screen.findByRole("alert")).toHaveTextContent("Başlık, kaynak ve sahip zorunludur.");
  expect(vi.mocked(fetch).mock.calls.some(([, i]) => i?.method === "POST")).toBe(false);
});
