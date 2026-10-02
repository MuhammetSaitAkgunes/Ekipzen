import { AuthProvider } from "../../auth/AuthContext";
import { useState } from "react";
import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, expect, test, vi } from "vitest";
import { stubApi } from "../../test/apiStub";
import { renderPage } from "../../test/renderPage";
import { ToastProvider } from "../../layout/ToastProvider";
import { CandidateCorrectionModal } from "./CandidateCorrectionModal";
import type { CandidateDetailDto } from "./queries";

const candidate = { id: 5, name: "Deniz Yanlış", appliedRole: "Eski Rol", positionId: 12,
  positionTitle: "Eski Rol", status: "Mülakat", score: 70, experienceYears: 3, version: 4 } as CandidateDetailDto;
beforeEach(() => stubApi({
  "/api/positions": [
    { id: 12, title: "Eski Rol", isOpen: false, openCount: 2, version: 2 },
    { id: 15, title: "Yeni Rol", isOpen: true, openCount: 1, version: 7 },
    { id: 16, title: "Dolu Rol", isOpen: true, openCount: 0, version: 1 },
  ],
  "/api/candidates/5": { ...candidate, version: 5 },
  "/api/candidates/5/corrections": [],
}));
afterEach(() => vi.unstubAllGlobals());
const renderModal = (record = candidate, onClose = vi.fn()) => renderPage(
  <AuthProvider><ToastProvider><CandidateCorrectionModal candidate={record} onClose={onClose} /></ToastProvider></AuthProvider>);
const putCalls = () => vi.mocked(fetch).mock.calls.filter(([, init]) => init?.method === "PUT");

test("rol değişimi uygunluk teyidi ve önizleme sonrası incelenen sürümlerle gönderilir", async () => {
  const onClose = vi.fn();
  renderModal(candidate, onClose);
  await screen.findByRole("option", { name: "Yeni Rol · 1 kalan kadro" });
  expect(screen.getByRole("option", { name: /Eski Rol.*kapalı/ })).toBeInTheDocument();
  expect(screen.queryByRole("option", { name: /Dolu Rol/ })).not.toBeInTheDocument();
  await userEvent.selectOptions(screen.getByLabelText("Bağlı pozisyon"), "15");
  expect(screen.getByLabelText("Başvurulan rol")).toHaveValue("Yeni Rol");
  await userEvent.clear(screen.getByLabelText("Ad Soyad"));
  await userEvent.type(screen.getByLabelText("Ad Soyad"), "Deniz Doğru");
  await userEvent.type(screen.getByLabelText("Düzeltme gerekçesi ve dayanak"), "Başvuru belgesi doğrulandı");
  await userEvent.click(screen.getByRole("button", { name: "Bilgileri kontrol et" }));
  expect(screen.getByRole("alert")).toHaveTextContent("yeni role uygunluğunu kontrol");
  expect(putCalls()).toHaveLength(0);
  await userEvent.click(screen.getByRole("checkbox", { name: /Mevcut not ve değerlendirmelerin/ }));
  await userEvent.click(screen.getByRole("button", { name: "Bilgileri kontrol et" }));
  const preview = screen.getByRole("region", { name: "Aday düzeltmesi ön kontrolü" });
  expect(preview).toHaveTextContent("Deniz Yanlış");
  expect(preview).toHaveTextContent("Deniz Doğru");
  expect(preview).toHaveTextContent("Mülakat olarak kalır");
  expect(putCalls()).toHaveLength(0);
  await userEvent.click(screen.getByRole("button", { name: "Düzeltmeyi kaydet" }));
  await waitFor(() => expect(onClose).toHaveBeenCalledOnce());
  expect(JSON.parse(String(putCalls()[0][1]?.body))).toEqual({
    expectedVersion: 4, name: "Deniz Doğru", appliedRole: "Yeni Rol", positionId: 15,
    expectedPositionVersion: 7, score: 70, location: null, experienceYears: 3, summary: null,
    reason: "Başvuru belgesi doğrulandı", confirmExistingEvidenceReview: true,
  });
});

test("önizleme sonrası alan değişirse kayıt onayı ve rol teyidi yeniden istenir", async () => {
  renderModal();
  await screen.findByRole("option", { name: /Yeni Rol/ });
  await userEvent.type(screen.getByLabelText("Başvurulan rol"), " Uzman");
  await userEvent.type(screen.getByLabelText("Düzeltme gerekçesi ve dayanak"), "Belgeden kontrol");
  await userEvent.click(screen.getByRole("checkbox"));
  await userEvent.click(screen.getByRole("button", { name: "Bilgileri kontrol et" }));
  await userEvent.type(screen.getByLabelText("Lokasyon"), "Ankara");
  expect(screen.queryByRole("button", { name: "Düzeltmeyi kaydet" })).not.toBeInTheDocument();
  expect(screen.getByRole("checkbox")).not.toBeChecked();
  expect(putCalls()).toHaveLength(0);
});

test("arka planda sürüm değişse de eski formun beklenen sürümü değiştirilmez; çakışma taslağı korur", async () => {
  const baseFetch = globalThis.fetch;
  vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    if (init?.method === "PUT") return new Response(JSON.stringify({ title: "Aday kaydı bu sırada değişti" }),
      { status: 409, headers: { "Content-Type": "application/problem+json" } });
    return baseFetch(input, init);
  }));
  // Aynı pencere açıkken gelen yeni prop eski taslağın sürümünü yükseltmemeli.
  function Harness() {
    const [latest, setLatest] = useState(candidate);
    return <AuthProvider><ToastProvider><button onClick={() => setLatest({ ...candidate, version: 8 })}>Arka plan yenile</button>
      <CandidateCorrectionModal candidate={latest} onClose={() => {}} /></ToastProvider></AuthProvider>;
  }
  renderPage(<Harness />);
  await userEvent.type(screen.getByLabelText("Ad Soyad"), " Düzeltildi");
  await userEvent.type(screen.getByLabelText("Düzeltme gerekçesi ve dayanak"), "İsim belgeden doğrulandı");
  await userEvent.click(screen.getByRole("button", { name: "Arka plan yenile" }));
  await userEvent.click(screen.getByRole("button", { name: "Bilgileri kontrol et" }));
  await userEvent.click(screen.getByRole("button", { name: "Düzeltmeyi kaydet" }));
  expect(await screen.findByRole("alert")).toHaveTextContent("Aday kaydı bu sırada değişti");
  expect(JSON.parse(String(putCalls()[0][1]?.body)).expectedVersion).toBe(4);
  expect(screen.getByLabelText("Ad Soyad")).toHaveValue("Deniz Yanlış Düzeltildi");
  expect(screen.queryByRole("button", { name: "Düzeltmeyi kaydet" })).not.toBeInTheDocument();
});

test.each([{ score: null, value: "" }, { score: 0, value: "0" }])("başka alan düzeltmesi genel puanı $score olarak korur", async ({ score, value }) => {
  renderModal({ ...candidate, score });
  expect(screen.getByLabelText("Elle verilen aday puanı (0-100)")).toHaveValue(value === "" ? null : 0);
  await userEvent.type(screen.getByLabelText("Lokasyon"), "Ankara");
  await userEvent.type(screen.getByLabelText("Düzeltme gerekçesi ve dayanak"), "Adres doğrulandı");
  await userEvent.click(screen.getByRole("button", { name: "Bilgileri kontrol et" }));
  await userEvent.click(screen.getByRole("button", { name: "Düzeltmeyi kaydet" }));
  await waitFor(() => expect(putCalls()).toHaveLength(1));
  expect(JSON.parse(String(putCalls()[0][1]?.body)).score).toBe(score);
});

test("yanlış genel puan boşaltılırken önce ve sonra açıkça gösterilir", async () => {
  renderModal();
  await userEvent.clear(screen.getByLabelText("Elle verilen aday puanı (0-100)"));
  await userEvent.type(screen.getByLabelText("Düzeltme gerekçesi ve dayanak"), "Dayanağı olmayan puan kaldırıldı");
  await userEvent.click(screen.getByRole("button", { name: "Bilgileri kontrol et" }));
  const preview = screen.getByRole("region", { name: "Aday düzeltmesi ön kontrolü" });
  expect(preview).toHaveTextContent("70/100 puan");
  expect(preview).toHaveTextContent("Henüz puan verilmedi");
  await userEvent.click(screen.getByRole("button", { name: "Düzeltmeyi kaydet" }));
  await waitFor(() => expect(putCalls()).toHaveLength(1));
  expect(JSON.parse(String(putCalls()[0][1]?.body))).toMatchObject({ score: null, expectedVersion: 4 });
});

test("işe alınmış başvuru salt okunur gerekçeli geçmiş sunar", async () => {
  const before = { name: "Deniz Yanlış", appliedRole: "Eski Rol", positionId: 12, positionTitle: "Eski Rol",
    score: 70, location: null, experienceYears: 3, summary: null, status: "Mülakat" };
  stubApi({ "/api/positions": [], "/api/candidates/5/corrections": [{ id: 8,
    fromVersion: 4, toVersion: 5, before, after: { ...before, name: "Deniz Doğru" },
    reason: "Belgedeki ad düzeltildi", changedByName: "İK Yöneticisi", changedAtUtc: "2026-09-30T10:00:00Z" }] });
  renderModal({ ...candidate, status: "İşe Alındı" });
  expect(screen.getByText(/başvuru kaydı kilitlidir/)).toBeInTheDocument();
  expect(screen.queryByLabelText("Ad Soyad")).not.toBeInTheDocument();
  expect(screen.queryByRole("button", { name: "Bilgileri kontrol et" })).not.toBeInTheDocument();
  await userEvent.click(screen.getByText("Gerekçeli düzeltme geçmişi"));
  await userEvent.click(await screen.findByText(/Sürüm 4 → 5/));
  expect(within(screen.getByRole("dialog")).getByText("Deniz Doğru")).toBeInTheDocument();
  expect(putCalls()).toHaveLength(0);
});
