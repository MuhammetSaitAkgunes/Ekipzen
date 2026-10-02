import { act, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, expect, test, vi } from "vitest";
import { AuthProvider } from "../auth/AuthContext";
import { setSession, clearSession } from "../api/session";
import { pendingWrites, rememberUnknownWrite, sessionIdentity } from "../api/writeRecovery";
import { renderPage } from "../test/renderPage";
import { WriteRecoveryNotice } from "./WriteRecoveryNotice";

beforeEach(() => { localStorage.clear(); sessionStorage.clear(); setSession({ token: "T", refreshToken: "R", user: { id: "notice-owner", tenantId: 1, role: "hr-admin" } }); });
afterEach(() => { clearSession(); sessionStorage.clear(); vi.unstubAllGlobals(); });
test("uyarı kaydı kontrol etmeden kalkmaz; kaldırma gerçek yazım veya öğrenme kanıtı göndermez", async () => {
  rememberUnknownWrite(sessionIdentity(), "/actions/7", "PUT", null);
  vi.stubGlobal("fetch", vi.fn());
  renderPage(<AuthProvider><WriteRecoveryNotice /></AuthProvider>);
  expect(screen.getByRole("alert")).toHaveTextContent("İşlem kaydedilmiş olabilir");
  expect(screen.getByRole("link")).toHaveAttribute("href", "/app/actions?actionId=7");
  expect(screen.getByRole("button", { name: "Kontrol uyarısını kaldır" })).toBeDisabled();
  await userEvent.click(screen.getByRole("checkbox"));
  await userEvent.click(screen.getByRole("button", { name: "Kontrol uyarısını kaldır" }));
  expect(pendingWrites()).toHaveLength(0); expect(screen.queryByRole("alert")).not.toBeInTheDocument();
  expect(fetch).not.toHaveBeenCalled();
});
test("başka tenant aynı kullanıcıyla açıldığında önceki uyarı ve teyit görünmez", () => {
  rememberUnknownWrite(sessionIdentity(), "/actions/7", "PUT", null);
  renderPage(<AuthProvider><WriteRecoveryNotice /></AuthProvider>);
  expect(screen.getByRole("alert")).toBeInTheDocument();
  act(() => setSession({ token: "T2", refreshToken: "R2", user: { id: "notice-owner", tenantId: 2, role: "hr-admin" } }));
  expect(screen.queryByRole("alert")).not.toBeInTheDocument();
});
