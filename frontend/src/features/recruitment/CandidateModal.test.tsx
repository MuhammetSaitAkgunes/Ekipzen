import { AuthProvider } from "../../auth/AuthContext";
import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, expect, test, vi } from "vitest";
import { stubApi } from "../../test/apiStub";
import { renderPage } from "../../test/renderPage";
import { ToastProvider } from "../../layout/ToastProvider";
import { CandidateModal } from "./CandidateModal";

beforeEach(() =>
  stubApi({
    "/api/candidates": { id: 7, name: "Zeynep Aksoy", appliedRole: "QA Engineer", status: "Yeni", score: 70, initials: "ZA" },
    "/api/positions": [],
    "/api/guidance/recruitment/checks/candidate-create": {
      stepKey: "candidate-create", contentVersion: 1,
      question: "Başvuruyu kaydederken aday puanının dayanağı henüz yok. Ne yaparsınız?",
      choices: ["Tahmini bir puan girerim", "Puanı başka adaydan kopyalarım", "Değerlendirme dayanağını netleştirmeden puan uydurmam"],
      lastSelectedChoice: null, lastCorrect: null, attemptCount: 0, explanation: null,
    },
  }),
);
afterEach(() => vi.unstubAllGlobals());

const renderModal = (onCreated = vi.fn()) => {
  renderPage(
    <AuthProvider><ToastProvider>
      <CandidateModal onClose={() => {}} onCreated={onCreated} />
    </ToastProvider></AuthProvider>,
  );
  return onCreated;
};

test("aday puanı karar alıştırması kaydedilir ama aday oluşturulmuş sayılmaz", async () => {
  const baseFetch = globalThis.fetch;
  let attemptCount = 0;
  vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    if (String(input) === "/api/guidance/recruitment/checks/candidate-create/attempts" &&
        init?.method === "POST") {
      const { selectedChoice } = JSON.parse(String(init.body));
      attemptCount++;
      return new Response(JSON.stringify({
        stepKey: "candidate-create", contentVersion: 1,
        question: "Başvuruyu kaydederken aday puanının dayanağı henüz yok. Ne yaparsınız?",
        choices: ["Tahmini bir puan girerim", "Puanı başka adaydan kopyalarım", "Değerlendirme dayanağını netleştirmeden puan uydurmam"],
        lastSelectedChoice: selectedChoice, lastCorrect: selectedChoice === 2, attemptCount,
        explanation: "Dayanak yoksa rastgele sayı girmeyin.",
      }), { status: 200, headers: { "Content-Type": "application/json" } });
    }
    return baseFetch(input, init);
  }));
  renderModal();
  await userEvent.click(screen.getByRole("button", { name: "Rehber" }));
  await userEvent.click(screen.getByText("Kısa karar alıştırması"));
  expect(await screen.findByText(/aday puanının dayanağı henüz yok/)).toBeInTheDocument();
  await userEvent.click(screen.getByLabelText("Tahmini bir puan girerim"));
  expect(screen.queryByRole("status")).not.toBeInTheDocument();
  await userEvent.click(screen.getByRole("button", { name: "Yanıtı kontrol et" }));
  expect(await screen.findByRole("status")).toHaveTextContent("Bir daha düşünün.");
  await userEvent.click(screen.getByLabelText("Değerlendirme dayanağını netleştirmeden puan uydurmam"));
  await userEvent.click(screen.getByRole("button", { name: "Yanıtı kontrol et" }));
  await waitFor(() => expect(screen.getByRole("status")).toHaveTextContent("Doğru karar."));
  expect(screen.getByText(/2 kayıtlı deneme/)).toBeInTheDocument();
  expect(screen.getByText(/gerçek aday işlemi kanıtı veya yeterlilik onayı değildir/)).toBeInTheDocument();
  expect(vi.mocked(fetch).mock.calls.some(([url]) => String(url) === "/api/candidates")).toBe(false);
});

test("ön kontrolden sonra aday tam gövdeyle kaydedilir", async () => {
  const onCreated = renderModal();
  await userEvent.type(screen.getByLabelText("Ad Soyad"), "Zeynep Aksoy");
  await userEvent.type(screen.getByLabelText("Başvurulan rol"), "QA Engineer");
  await userEvent.type(screen.getByLabelText("Elle verilen aday puanı (0-100)"), "70");
  await userEvent.type(screen.getByLabelText("Yetenekler (virgülle)"), "Playwright, API testi");
  await userEvent.click(screen.getByRole("button", { name: /Bilgileri kontrol et/ }));
  await userEvent.click(screen.getByRole("button", { name: "Rehber" }));
  expect(screen.getByRole("region", { name: "Aday kaydı rehberi" })).toHaveTextContent("sistem AI ile hesaplamaz");
  await userEvent.click(screen.getByRole("button", { name: "Kapat" }));
  expect(screen.getByText("Kaydetmeden önce son kontrol")).toBeInTheDocument();
  expect(vi.mocked(fetch).mock.calls.some(([, i]) => i?.method === "POST")).toBe(false);
  await userEvent.click(screen.getByRole("button", { name: /Adayı oluştur/ }));
  await waitFor(() => {
    const posted = vi.mocked(fetch).mock.calls.find(
      ([u, i]) => String(u) === "/api/candidates" && i?.method === "POST",
    );
    expect(posted).toBeTruthy();
    expect(JSON.parse(String(posted![1]?.body))).toMatchObject({
      name: "Zeynep Aksoy", appliedRole: "QA Engineer", score: 70,
      skills: ["Playwright", "API testi"],
    });
    expect(onCreated).toHaveBeenCalledWith(7);
  });
});

test("ad boşsa ön kontrol hatası gösterilir, istek atılmaz", async () => {
  renderModal();
  await userEvent.click(screen.getByRole("button", { name: /Bilgileri kontrol et/ }));
  expect(await screen.findByRole("alert")).toHaveTextContent("Ad Soyad ve pozisyon zorunludur.");
  expect(vi.mocked(fetch).mock.calls.some(([, i]) => i?.method === "POST")).toBe(false);
});

test("geçersiz puan reddedilir", async () => {
  renderModal();
  await userEvent.type(screen.getByLabelText("Ad Soyad"), "Zeynep Aksoy");
  await userEvent.type(screen.getByLabelText("Başvurulan rol"), "QA Engineer");
  await userEvent.type(screen.getByLabelText("Elle verilen aday puanı (0-100)"), "101");
  await userEvent.click(screen.getByRole("button", { name: /Bilgileri kontrol et/ }));
  expect(screen.getByRole("alert")).toHaveTextContent("boş bırakın veya 0-100 arasında");
  expect(vi.mocked(fetch).mock.calls.some(([, i]) => i?.method === "POST")).toBe(false);
});

test.each([{ input: "", score: null, label: "Henüz puan verilmedi" }, { input: "0", score: 0, label: "0/100 puan" }])(
  "puan alanı $input olduğunda önizleme ve istek $score değerini korur", async ({ input, score, label }) => {
    renderModal();
    await userEvent.type(screen.getByLabelText("Ad Soyad"), "Zeynep Aksoy");
    await userEvent.type(screen.getByLabelText("Başvurulan rol"), "QA Engineer");
    if (input) await userEvent.type(screen.getByLabelText("Elle verilen aday puanı (0-100)"), input);
    await userEvent.click(screen.getByRole("button", { name: /Bilgileri kontrol et/ }));
    expect(screen.getByText(/Elle verilen puan:/).closest("p")).toHaveTextContent(label);
    expect(vi.mocked(fetch).mock.calls.some(([, i]) => i?.method === "POST")).toBe(false);
    await userEvent.click(screen.getByRole("button", { name: /Adayı oluştur/ }));
    await waitFor(() => {
      const posted = vi.mocked(fetch).mock.calls.find(([url, init]) => String(url) === "/api/candidates" && init?.method === "POST");
      expect(JSON.parse(String(posted?.[1]?.body)).score).toBe(score);
    });
  });

test("ön kontrolden sonra alan değişirse eski onay geçersiz olur", async () => {
  renderModal();
  await userEvent.type(screen.getByLabelText("Ad Soyad"), "Zeynep Aksoy");
  await userEvent.type(screen.getByLabelText("Başvurulan rol"), "QA Engineer");
  await userEvent.type(screen.getByLabelText("Elle verilen aday puanı (0-100)"), "70");
  await userEvent.click(screen.getByRole("button", { name: /Bilgileri kontrol et/ }));
  await userEvent.type(screen.getByLabelText("Ad Soyad"), " Yılmaz");
  expect(screen.queryByRole("button", { name: /Adayı oluştur/ })).not.toBeInTheDocument();
  expect(screen.getByRole("button", { name: /Bilgileri kontrol et/ })).toBeInTheDocument();
  expect(vi.mocked(fetch).mock.calls.some(([, i]) => i?.method === "POST")).toBe(false);
});

test("açık pozisyon seçilince aday gerçek pozisyon kimliğiyle bağlanır", async () => {
  stubApi({
    "/api/positions": [{ id: 12, title: "QA Engineer", department: "Ürün", isOpen: true, openCount: 2, candidateCount: 1 }],
    "/api/candidates": { id: 7, name: "Zeynep Aksoy", appliedRole: "QA Engineer", positionId: 12, status: "Yeni", score: 70 },
  });
  renderModal();
  await userEvent.type(screen.getByLabelText("Ad Soyad"), "Zeynep Aksoy");
  await screen.findByRole("option", { name: /QA Engineer/ });
  await userEvent.selectOptions(screen.getByLabelText("Açık pozisyon kaydı"), "12");
  expect(screen.getByLabelText("Başvurulan rol")).toHaveValue("QA Engineer");
  expect(screen.getByLabelText("Başvurulan rol")).toBeDisabled();
  await userEvent.type(screen.getByLabelText("Elle verilen aday puanı (0-100)"), "70");
  await userEvent.click(screen.getByRole("button", { name: /Bilgileri kontrol et/ }));
  expect(screen.getByText(/Pozisyon bağlantısı:/).closest("p")).toHaveTextContent("QA Engineer");
  await userEvent.click(screen.getByRole("button", { name: /Adayı oluştur/ }));
  await waitFor(() => {
    const posted = vi.mocked(fetch).mock.calls.find(
      ([u, i]) => String(u) === "/api/candidates" && i?.method === "POST",
    );
    expect(JSON.parse(String(posted?.[1]?.body))).toMatchObject({
      name: "Zeynep Aksoy", appliedRole: "QA Engineer", positionId: 12, score: 70,
    });
  });
});
