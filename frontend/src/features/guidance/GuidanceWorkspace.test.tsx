import { useState } from "react";
import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, expect, test, vi } from "vitest";
import { AuthProvider } from "../../auth/AuthContext";
import { SESSION_KEY } from "../../api/session";
import { renderPage } from "../../test/renderPage";
import { stubApi } from "../../test/apiStub";
import { GuidanceOperationSource } from "./GuidanceOperationSource";
import { GuidanceWorkspaceProvider, useGuidanceWorkspace } from "./GuidanceWorkspace";

beforeEach(() => {
  localStorage.clear();
  localStorage.setItem(SESSION_KEY, JSON.stringify({ token: "T", refreshToken: "R", user: { id: "u", role: "hr-admin" } }));
  stubApi({});
});
afterEach(() => vi.unstubAllGlobals());
function Block({ name, hidden = false }: { name: string; hidden?: boolean }) {
  return <div className="ez-workbench" hidden={hidden}><div className="ez-workbench-layout">
    <div className="ez-workbench-guide"><GuidanceOperationSource process="daily" step="task-action" value={null} onChange={() => {}} label={name} /></div>
    <div className="ez-workbench-main"><label>{name} alanı<input defaultValue="" /></label></div>
  </div></div>;
}
function Frame() {
  const workspace = useGuidanceWorkspace(); const [modal, setModal] = useState(false);
  return <div onFocusCapture={workspace?.focus}><Block name="Birinci" /><Block name="İkinci" />
    <button onClick={() => setModal(true)}>Alt işlemi aç</button>
    {modal && <div role="dialog" aria-label="Alt işlem"><Block name="Alt işlem" /><button onClick={() => setModal(false)}>Alt işlemi kapat</button></div>}
  </div>;
}
const mount = () => renderPage(<AuthProvider><GuidanceWorkspaceProvider><Frame /></GuidanceWorkspaceProvider></AuthProvider>);

test("alan odağı yalnız ilgili rehberi açar; küçültme ve yeniden açma taslağı korur", async () => {
  mount();
  const first = screen.getByRole("complementary", { name: "Birinci" });
  const second = screen.getByRole("complementary", { name: "İkinci" });
  await waitFor(() => expect(within(first).getByRole("button", { name: "Küçült" })).toHaveAttribute("aria-expanded", "true"));
  await userEvent.type(screen.getByLabelText("Birinci alanı"), "Taslak");
  await userEvent.click(screen.getByLabelText("İkinci alanı"));
  expect(within(first).getByRole("button", { name: "Rehberi aç" })).toHaveAttribute("aria-expanded", "false");
  await userEvent.click(within(first).getByRole("button", { name: "Rehberi aç" }));
  expect(within(first).getByRole("button", { name: "Küçült" })).toHaveAttribute("aria-expanded", "true");
  expect(within(second).getByRole("button", { name: "Rehberi aç" })).toHaveAttribute("aria-expanded", "false");
  await userEvent.click(within(first).getByRole("button", { name: "Küçült" }));
  await userEvent.click(screen.getByLabelText("İkinci alanı"));
  expect(screen.queryByRole("button", { name: "Küçült" })).not.toBeInTheDocument();
  expect(screen.getByLabelText("Birinci alanı")).toHaveValue("Taslak");
  expect(vi.mocked(fetch).mock.calls.every(([, init]) => !init?.method || init.method === "GET")).toBe(true);
});

test("alt işlem açılınca onun rehberi öne gelir; kapanınca ana forma dönülür", async () => {
  mount(); await userEvent.type(screen.getByLabelText("Birinci alanı"), "Korunan değer");
  await userEvent.click(screen.getByRole("button", { name: "Alt işlemi aç" }));
  const dialog = screen.getByRole("dialog", { name: "Alt işlem" });
  expect(within(dialog).getByRole("button", { name: "Küçült" })).toHaveAttribute("aria-expanded", "true");
  await userEvent.click(screen.getByRole("button", { name: "Alt işlemi kapat" }));
  expect(within(screen.getByRole("complementary", { name: "Birinci" })).getByRole("button", { name: "Küçült" })).toHaveAttribute("aria-expanded", "true");
  expect(screen.getByLabelText("Birinci alanı")).toHaveValue("Korunan değer");
});

test("gizli bölümdeki rehber görünür bölümün yerini almaz", async () => {
  renderPage(<AuthProvider><GuidanceWorkspaceProvider><Block name="Gizli" hidden /><Block name="Görünür" /></GuidanceWorkspaceProvider></AuthProvider>);
  await waitFor(() => expect(within(screen.getByRole("complementary", { name: "Görünür" })).getByRole("button", { name: "Küçült" })).toHaveAttribute("aria-expanded", "true"));
});
