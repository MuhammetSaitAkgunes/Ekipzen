import { AuthProvider } from "../../auth/AuthContext";
import { useState } from "react";
import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, expect, test, vi } from "vitest";
import { stubApi } from "../../test/apiStub";
import { renderPage } from "../../test/renderPage";
import { ToastProvider } from "../../layout/ToastProvider";
import { CandidateEvidenceModal, type EvidenceSelection } from "./CandidateEvidenceModal";
import type { CandidateDetailDto } from "./queries";

const candidate = { id: 5, name: "Deniz", status: "Mülakat", version: 4 } as CandidateDetailDto;
const note: EvidenceSelection = { kind: "notes", record: { id: 7, noteType: "İK Görüşmesi",
  text: "Güncel gözlem", authorName: "İlk Yazar", version: 2 } };
const evaluation: EvidenceSelection = { kind: "evaluations", record: { id: 8,
  criterion: "SQL", score: 4, maxScore: 5, version: 1 } };
const original = { noteType: "İK Görüşmesi", text: "İlk not", criterion: null, score: null, maxScore: null };
beforeEach(() => stubApi({
  "/api/candidates/5/notes/7/revisions": { original, revisions: [{ id: 9, version: 2,
    before: original, after: { ...original, text: "Güncel gözlem" }, reason: "İlk kontrol",
    changedByName: "İK", changedAtUtc: "2026-09-30T10:00:00Z" }] },
  "/api/candidates/5/evaluations/8/revisions": { original: { noteType: null, text: null,
    criterion: "SQL", score: 4, maxScore: 5 }, revisions: [] },
}));
afterEach(() => vi.unstubAllGlobals());
const renderModal = (selection: EvidenceSelection = note, record: CandidateDetailDto = candidate, onClose = vi.fn()) => renderPage(
  <AuthProvider><ToastProvider><CandidateEvidenceModal candidate={record} selection={selection} onClose={onClose} /></ToastProvider></AuthProvider>);
const posts = () => vi.mocked(fetch).mock.calls.filter(([, init]) => init?.method === "POST");

test("not ilk kaydı korunarak önizlemeden sonra iki incelenen sürümle düzeltilir", async () => {
  const onClose = vi.fn(); renderModal(note, candidate, onClose);
  await userEvent.click(screen.getByText("İlk kayıt ve düzeltme geçmişi"));
  expect((await screen.findAllByText("İlk not"))[0]).toBeInTheDocument();
  await userEvent.click(screen.getByText(/Sürüm 2 · İlk kontrol/));
  expect(screen.getAllByText("Güncel gözlem")[0]).toBeInTheDocument();
  await userEvent.selectOptions(screen.getByLabelText("Görüşme türü"), "Teknik Mülakat");
  await userEvent.clear(screen.getByLabelText("Güncel not metni"));
  await userEvent.type(screen.getByLabelText("Güncel not metni"), "Belgeyle doğrulanan gözlem");
  await userEvent.type(screen.getByLabelText("Düzeltme gerekçesi ve dayanak"), "Görüşme belgesinden kontrol");
  await userEvent.click(screen.getByRole("button", { name: "Bilgileri kontrol et" }));
  expect(screen.getByRole("region", { name: "Kanıt düzeltmesi ön kontrolü" }))
    .toHaveTextContent("Belgeyle doğrulanan gözlem");
  expect(posts()).toHaveLength(0);
  await userEvent.click(screen.getByRole("button", { name: "Yeni sürümü kaydet" }));
  await waitFor(() => expect(onClose).toHaveBeenCalledOnce());
  expect(String(posts()[0][0])).toBe("/api/candidates/5/notes/7/revisions");
  expect(JSON.parse(String(posts()[0][1]?.body))).toEqual({
    expectedCandidateVersion: 4, expectedRecordVersion: 2, noteType: "Teknik Mülakat",
    text: "Belgeyle doğrulanan gözlem", reason: "Görüşme belgesinden kontrol",
  });
});

test("değerlendirme puan sınırını doğrular ve ölçek değişimini önizletir", async () => {
  renderModal(evaluation);
  await userEvent.type(screen.getByLabelText("Düzeltme gerekçesi ve dayanak"), "Puan ölçeği belgeden doğrulandı");
  await userEvent.clear(screen.getByLabelText("Güncel azami puan"));
  await userEvent.type(screen.getByLabelText("Güncel azami puan"), "3");
  await userEvent.click(screen.getByRole("button", { name: "Bilgileri kontrol et" }));
  expect(screen.getByRole("alert")).toHaveTextContent("puan 0-azami puan");
  expect(posts()).toHaveLength(0);
  await userEvent.clear(screen.getByLabelText("Güncel azami puan"));
  await userEvent.type(screen.getByLabelText("Güncel azami puan"), "10");
  await userEvent.click(screen.getByRole("button", { name: "Bilgileri kontrol et" }));
  const preview = screen.getByRole("region", { name: "Kanıt düzeltmesi ön kontrolü" });
  expect(preview).toHaveTextContent("4/5"); expect(preview).toHaveTextContent("4/10");
  await userEvent.type(screen.getByLabelText("Güncel ölçüt"), " Uygulaması");
  expect(screen.queryByRole("button", { name: "Yeni sürümü kaydet" })).not.toBeInTheDocument();
  await userEvent.click(screen.getByRole("button", { name: "Bilgileri kontrol et" }));
  await userEvent.click(screen.getByRole("button", { name: "Yeni sürümü kaydet" }));
  await waitFor(() => expect(posts()).toHaveLength(1));
  expect(JSON.parse(String(posts()[0][1]?.body))).toEqual({ expectedCandidateVersion: 4,
    expectedRecordVersion: 1, criterion: "SQL Uygulaması", score: 4, maxScore: 10,
    reason: "Puan ölçeği belgeden doğrulandı" });
});

test("arka plan yenilemesi eski taslağın iki sürümünü yükseltmez; çakışma formu korur", async () => {
  const baseFetch = globalThis.fetch;
  vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    if (init?.method === "POST") return new Response(JSON.stringify({ title: "Aday kaydı değişti" }),
      { status: 409, headers: { "Content-Type": "application/problem+json" } });
    return baseFetch(input, init);
  }));
  function Harness() {
    const [fresh, setFresh] = useState(false);
    return <AuthProvider><ToastProvider><button onClick={() => setFresh(true)}>Yenile</button>
      <CandidateEvidenceModal candidate={fresh ? { ...candidate, version: 9 } : candidate}
        selection={fresh ? { ...note, record: { ...note.record, version: 4 } } as EvidenceSelection : note} onClose={() => {}} /></ToastProvider></AuthProvider>;
  }
  renderPage(<Harness />);
  await userEvent.type(screen.getByLabelText("Güncel not metni"), " düzeltildi");
  await userEvent.type(screen.getByLabelText("Düzeltme gerekçesi ve dayanak"), "Dayanak kontrol edildi");
  await userEvent.click(screen.getByRole("button", { name: "Yenile" }));
  await userEvent.click(screen.getByRole("button", { name: "Bilgileri kontrol et" }));
  await userEvent.click(screen.getByRole("button", { name: "Yeni sürümü kaydet" }));
  expect(await screen.findByRole("alert")).toHaveTextContent("Aday kaydı değişti");
  expect(screen.getByLabelText("Güncel not metni")).toHaveValue("Güncel gözlem düzeltildi");
  expect(JSON.parse(String(posts()[0][1]?.body))).toMatchObject({ expectedCandidateVersion: 4, expectedRecordVersion: 2 });
  expect(screen.queryByRole("button", { name: "Yeni sürümü kaydet" })).not.toBeInTheDocument();
});

test("işe alınmış aday ilk kaydı ve sürüm geçmişini salt okunur açar", async () => {
  renderModal(note, { ...candidate, status: "İşe Alındı" });
  expect(screen.getByText(/başvuru geçmişi kilitli/)).toBeInTheDocument();
  expect(screen.queryByLabelText("Güncel not metni")).not.toBeInTheDocument();
  expect(screen.queryByRole("button", { name: "Bilgileri kontrol et" })).not.toBeInTheDocument();
  await userEvent.click(screen.getByText("İlk kayıt ve düzeltme geçmişi"));
  expect((await screen.findAllByText("İlk not"))[0]).toBeInTheDocument();
  expect(posts()).toHaveLength(0);
});
