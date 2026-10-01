import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, expect, test, vi } from "vitest";
import { AuthProvider } from "../../auth/AuthContext";
import { SESSION_KEY } from "../../api/session";
import { renderPage } from "../../test/renderPage";
import { stubApi } from "../../test/apiStub";
import { PublishedGuidancePractice } from "./PublishedGuidancePractice";
import { GuidancePracticeJourney } from "./GuidancePracticeJourney";
import type { PublishedPractice } from "./publishedPracticeQueries";

const practice: PublishedPractice = { contentRevisionId: 21, contentKey: "personnel-entry-tab-kimlik", processKey: "personnel-entry",
  stepKey: "tab-kimlik", contentVersion: 1, title: "Kimlik kontrolü", isCurrent: true, question: "İlk sürüm sorusu",
  choices: ["Tahmin", "Doğrulama"], attemptCount: 0, lastSelectedChoice: null, lastCorrect: null, explanation: null };
const stepPath = "/api/guidance/catalog/steps/personnel-entry/tab-kimlik";
beforeEach(() => {
  localStorage.clear(); localStorage.setItem(SESSION_KEY, JSON.stringify({ token: "T", refreshToken: "R",
    user: { id: "u", name: "Görevli", email: "u@example.test", role: "hr-admin", roleLabel: "İK", initials: "G", employeeId: null } }));
});
afterEach(() => vi.unstubAllGlobals());
const renderPractice = () => renderPage(<AuthProvider><PublishedGuidancePractice process="personnel-entry" step="tab-kimlik" /></AuthProvider>);

test("yeni yayın açık alıştırmanın sorusunu ve seçilen yanıtını değiştirmez; eski sürüm çakışmada korunur", async () => {
  let latest = practice;
  stubApi({ [stepPath]: practice, "/api/guidance/catalog/versions/21/practice": practice });
  const mock = vi.mocked(fetch), original = mock.getMockImplementation()!;
  mock.mockImplementation(async (input, init) => {
    if (String(input) === stepPath) return new Response(JSON.stringify(latest));
    if (String(input).endsWith("/attempts")) return new Response(JSON.stringify({ title: "Alıştırma sürümü güncel değil" }), { status: 409 });
    return original(input, init);
  });
  renderPractice(); expect(mock).not.toHaveBeenCalled();
  await userEvent.click(screen.getByText("Kaynaklı adım ve sürümlü alıştırma"));
  await userEvent.click(await screen.findByRole("button", { name: "Bu sürümle alıştırmayı aç" }));
  await userEvent.click(await screen.findByRole("radio", { name: "Doğrulama" }));
  latest = { ...practice, contentRevisionId: 22, contentVersion: 2, question: "Yeni sürüm sorusu" };
  await userEvent.click(screen.getByRole("button", { name: "Güncel adımı yeniden yükle" }));
  await screen.findByText(/Güncel içerik:.*Sürüm 2/);
  expect(screen.getByText("İlk sürüm sorusu")).toBeInTheDocument();
  expect(screen.getByRole("radio", { name: "Doğrulama" })).toBeChecked();
  await userEvent.click(screen.getByRole("button", { name: "Sürümlü yanıtı kaydet" }));
  expect(await screen.findByRole("alert")).toHaveTextContent("sürümü güncel değil");
  expect(screen.getByRole("radio", { name: "Doğrulama" })).toBeChecked();
  const post = mock.mock.calls.find(([, init]) => init?.method === "POST")!;
  expect(post[0]).toBe("/api/guidance/catalog/versions/21/practice/attempts");
  expect(JSON.parse(String(post[1]?.body))).toMatchObject({ selectedChoice: 1, expectedAttemptCount: 0 });
  expect(screen.getByRole("link", { name: /açıklama ve dayanağını/ })).toHaveAttribute("href", "/app/guidance/catalog?version=21");
});

test("ağ hatasında aynı deneme kimliğiyle tekrar gönderilir; açıklama kayıt sonucundan gelir", async () => {
  stubApi({ [stepPath]: practice, "/api/guidance/catalog/versions/21/practice": practice });
  const mock = vi.mocked(fetch), original = mock.getMockImplementation()!;
  let attempts = 0;
  mock.mockImplementation(async (input, init) => {
    if (String(input).endsWith("/attempts")) {
      if (++attempts === 1) throw new TypeError("Bağlantı koptu");
      return new Response(JSON.stringify({ ...practice, attemptCount: 1, lastSelectedChoice: 1, lastCorrect: true, explanation: "Kaynağı doğrulayın." }));
    }
    return original(input, init);
  });
  renderPractice(); await userEvent.click(screen.getByText("Kaynaklı adım ve sürümlü alıştırma"));
  await userEvent.click(await screen.findByRole("button", { name: "Bu sürümle alıştırmayı aç" }));
  await userEvent.click(await screen.findByRole("radio", { name: "Doğrulama" }));
  expect(screen.queryByText(/Kaynağı doğrulayın/)).not.toBeInTheDocument();
  await userEvent.click(screen.getByRole("button", { name: "Sürümlü yanıtı kaydet" }));
  await screen.findByRole("alert");
  await userEvent.click(screen.getByRole("button", { name: "Sürümlü yanıtı kaydet" }));
  await screen.findByText(/Doğru karar. Kaynağı doğrulayın/);
  const posts = mock.mock.calls.filter(([, init]) => init?.method === "POST");
  expect(posts).toHaveLength(2); expect(posts[0][1]?.body).toBe(posts[1][1]?.body);
  expect(JSON.parse(String(posts[0][1]?.body)).attemptId).toMatch(/^[0-9a-f-]{36}$/);
});

test("yayınlanmamış adım gerçek işin tamamlandığına çevrilmez; geçmiş sürüm ayrı görünür", async () => {
  stubApi({ [stepPath]: null, "/api/guidance/catalog/practice-progress": [{ ...practice, attemptCount: 1, isCurrent: false,
    lastSelectedChoice: 0, lastCorrect: false, lastAnsweredAtUtc: "2026-10-01T08:00:00Z" }] });
  renderPage(<AuthProvider><PublishedGuidancePractice process="personnel-entry" step="tab-kimlik" /><GuidancePracticeJourney /></AuthProvider>);
  await userEvent.click(screen.getByText("Kaynaklı adım ve sürümlü alıştırma"));
  expect(await screen.findByText(/henüz yayımlanmadı/)).toBeInTheDocument();
  await userEvent.click(screen.getByText("Kaynaklı sürümlere ait alıştırma geçmişim"));
  await waitFor(() => expect(screen.getByText(/Geçmiş sürüm.*Yeniden çalışılmalı/)).toBeInTheDocument());
  expect(screen.getByText(/önceki yanıtlar güncel sürümü tamamlamış sayılmaz/)).toBeInTheDocument();
  expect(vi.mocked(fetch).mock.calls.some(([, init]) => init?.method === "POST")).toBe(false);
});
