import { fireEvent, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, expect, test, vi } from "vitest";
import { stubApi } from "../../test/apiStub";
import { renderPage } from "../../test/renderPage";
import { ToastProvider } from "../../layout/ToastProvider";
import { CandidateDetail } from "./CandidateDetail";

const detail = {
  id: 5, name: "Burak Yılmaz", appliedRole: "Senior Frontend Developer", version: 4,
  positionId: null, positionTitle: null, status: "Mülakat", score: 92, initials: "BY",
  appliedAtUtc: "2026-07-16T10:00:00Z", location: "İstanbul", experienceYears: 5,
  summary: "React ve modern JavaScript konusunda güçlü aday.",
  skills: [{ id: 1, name: "React.js" }, { id: 2, name: "TypeScript" }],
  experiences: [{ id: 1, title: "Senior Frontend Developer", company: "TechSolutions A.Ş.", period: "2021 - Günümüz", description: "Arayüz geliştirme." }],
  notes: [{ id: 1, authorName: "Ayşe Demir", noteType: "İK Görüşmesi", text: "İletişim becerileri kuvvetli.", createdAtUtc: "2026-07-15T14:30:00Z", version: 1 }],
  evaluations: [{ id: 1, criterion: "Teknik Yeterlilik", score: 4, maxScore: 5, version: 1 }],
  history: [{ id: 1, event: "Başvuru alındı", occurredAtUtc: "2026-07-14T09:00:00Z" }],
};
const departments = [{ id: 1, name: "Yazılım", employeeCount: 4 }];

beforeEach(() =>
  stubApi({
    "/api/candidates/5": detail,
    "/api/candidates/5/status": { ...detail, status: "Teklif" },
    "/api/candidates/5/notes": detail.notes[0],
    "/api/candidates/5/evaluations": { id: 2, criterion: "TypeScript bilgisi", score: 4, maxScore: 5 },
    "/api/candidates/5/hire": { candidateId: 5, employeeId: 9, employeeName: "Burak Yılmaz" },
    "/api/departments": departments,
    "/api/guidance/recruitment/checks/first-review": {
      stepKey: "first-review", contentVersion: 1,
      question: "Yeni adayın aşamasını değiştirmeden önce hangi kontrol gerekir?",
      choices: ["Yalnız genel aday puanına bakarım", "Başvuru, not ve değerlendirme dayanaklarını karşılaştırırım", "Aşama seçimi adaya otomatik bildirim gönderir"],
      lastSelectedChoice: null, lastCorrect: null, attemptCount: 0, explanation: null,
    },
  }),
);
afterEach(() => vi.unstubAllGlobals());

const renderDetail = () =>
  renderPage(
    <ToastProvider>
      <CandidateDetail id={5} />
    </ToastProvider>,
  );

test("aday detayından mevcut not gerekçeli düzeltme penceresinde açılır", async () => {
  stubApi({ "/api/candidates/5": detail,
    "/api/candidates/5/notes/1/revisions": { original: { noteType: "İK Görüşmesi",
      text: "İletişim becerileri kuvvetli.", criterion: null, score: null, maxScore: null }, revisions: [] } });
  renderDetail();
  await screen.findByRole("heading", { name: "Burak Yılmaz" });
  await userEvent.click(screen.getByRole("button", { name: "Mülakat Notları" }));
  await userEvent.click(screen.getByRole("button", { name: "Notu düzelt ve geçmişini gör" }));
  expect(screen.getByRole("dialog")).toHaveTextContent("incelenen kayıt sürümü 1");
  expect(screen.getByLabelText("Güncel not metni")).toHaveValue("İletişim becerileri kuvvetli.");
  expect(screen.getByRole("button", { name: "Bilgileri kontrol et" })).toBeEnabled();
});

test("işe alınmış adayda not ve değerlendirme ekleme kilitli, sürüm geçmişi erişilebilir", async () => {
  const original = { noteType: "İK Görüşmesi", text: "İletişim becerileri kuvvetli.", criterion: null, score: null, maxScore: null };
  stubApi({ "/api/candidates/5": { ...detail, status: "İşe Alındı" },
    "/api/candidates/5/notes/1/revisions": { original, revisions: [] },
    "/api/candidates/5/evaluations/1/revisions": { original: { noteType: null, text: null,
      criterion: "Teknik Yeterlilik", score: 4, maxScore: 5 }, revisions: [] } });
  renderDetail();
  await screen.findByRole("heading", { name: "Burak Yılmaz" });
  await userEvent.click(screen.getByRole("button", { name: "Mülakat Notları" }));
  expect(screen.getByRole("button", { name: "Notu kontrol et" })).toBeDisabled();
  await userEvent.click(screen.getByRole("button", { name: "Notun sürüm geçmişi" }));
  expect(screen.getByRole("dialog")).toHaveTextContent("başvuru geçmişi kilitli");
  expect(screen.queryByLabelText("Güncel not metni")).not.toBeInTheDocument();
  await userEvent.click(screen.getByRole("button", { name: "Kanıt düzeltme penceresini kapat" }));
  await userEvent.click(screen.getByRole("button", { name: "Değerlendirme" }));
  expect(screen.getByRole("button", { name: "Bilgileri kontrol et" })).toBeDisabled();
  await userEvent.click(screen.getByRole("button", { name: "Değerlendirmenin sürüm geçmişi" }));
  expect(screen.getByRole("dialog")).toHaveTextContent("başvuru geçmişi kilitli");
  expect(screen.queryByLabelText("Güncel puan")).not.toBeInTheDocument();
  expect(vi.mocked(fetch).mock.calls.some(([, init]) => init?.method === "POST")).toBe(false);
});

test("detay profil, etiketler ve özgeçmiş sekmesiyle açılır", async () => {
  renderDetail();
  expect(await screen.findByRole("heading", { name: "Burak Yılmaz" })).toBeInTheDocument();
  expect(screen.getByText("5 yıl deneyim")).toBeInTheDocument();
  expect(screen.getByText("React.js")).toBeInTheDocument();
  expect(screen.getByText("TechSolutions A.Ş. • 2021 - Günümüz")).toBeInTheDocument();
});

test("mülakat notu ekleme POST notes ucuna gider", async () => {
  renderDetail();
  await screen.findByRole("heading", { name: "Burak Yılmaz" });
  await userEvent.click(screen.getByRole("button", { name: "Mülakat Notları" }));
  await userEvent.type(screen.getByLabelText("Mülakat notu"), "Teknik derinlik iyi.");
  await userEvent.click(screen.getByRole("button", { name: "Notu kontrol et" }));
  const review = screen.getByRole("region", { name: "Mülakat notu ön kontrolü" });
  expect(review).toHaveTextContent("Burak Yılmaz");
  expect(review).toHaveTextContent("Teknik Mülakat");
  expect(review).toHaveTextContent("Teknik derinlik iyi.");
  expect(vi.mocked(fetch).mock.calls.some(([, i]) => i?.method === "POST")).toBe(false);
  await userEvent.click(screen.getByRole("button", { name: "Notu kaydet" }));
  await waitFor(() => {
    const posted = vi.mocked(fetch).mock.calls.find(
      ([u, i]) => String(u) === "/api/candidates/5/notes" && i?.method === "POST",
    );
    expect(posted).toBeTruthy();
    expect(JSON.parse(String(posted![1]?.body))).toMatchObject({
      noteType: "Teknik Mülakat", text: "Teknik derinlik iyi.",
    });
  });
});

test("boş veya uzun mülakat notu kaydedilmez", async () => {
  renderDetail();
  await screen.findByRole("heading", { name: "Burak Yılmaz" });
  await userEvent.click(screen.getByRole("button", { name: "Mülakat Notları" }));
  await userEvent.click(screen.getByRole("button", { name: "Notu kontrol et" }));
  expect(screen.getByRole("alert")).toHaveTextContent("Önce not içeriğini yazın.");
  fireEvent.change(screen.getByLabelText("Mülakat notu"), { target: { value: "a".repeat(4001) } });
  await userEvent.click(screen.getByRole("button", { name: "Notu kontrol et" }));
  expect(screen.getByRole("alert")).toHaveTextContent("en fazla 4000 karakter");
  expect(vi.mocked(fetch).mock.calls.some(([, i]) => i?.method === "POST")).toBe(false);
});

test("not değişince önceki ön kontrol geçersiz olur", async () => {
  renderDetail();
  await screen.findByRole("heading", { name: "Burak Yılmaz" });
  await userEvent.click(screen.getByRole("button", { name: "Mülakat Notları" }));
  await userEvent.type(screen.getByLabelText("Mülakat notu"), "İlk gözlem.");
  await userEvent.click(screen.getByRole("button", { name: "Notu kontrol et" }));
  await userEvent.selectOptions(screen.getByLabelText("Not türü"), "İK Görüşmesi");
  expect(screen.queryByRole("button", { name: "Notu kaydet" })).not.toBeInTheDocument();
  expect(screen.getByRole("button", { name: "Notu kontrol et" })).toBeInTheDocument();
  expect(vi.mocked(fetch).mock.calls.some(([, i]) => i?.method === "POST")).toBe(false);
});

test("aday değerlendirmesi ön kontrolden sonra POST ile kaydedilir", async () => {
  renderDetail();
  await screen.findByRole("heading", { name: "Burak Yılmaz" });
  await userEvent.click(screen.getByRole("button", { name: "Değerlendirme" }));
  await userEvent.type(screen.getByLabelText("Değerlendirme ölçütü"), "TypeScript bilgisi");
  await userEvent.type(screen.getByLabelText("Verilen puan"), "4");
  await userEvent.click(screen.getByRole("button", { name: "Bilgileri kontrol et" }));
  const review = screen.getByRole("region", { name: "Aday değerlendirmesi ön kontrolü" });
  expect(review).toHaveTextContent("Burak Yılmaz");
  expect(review).toHaveTextContent("TypeScript bilgisi");
  expect(review).toHaveTextContent("4/5");
  expect(vi.mocked(fetch).mock.calls.some(([, i]) => i?.method === "POST")).toBe(false);
  await userEvent.click(screen.getByRole("button", { name: "Değerlendirmeyi kaydet" }));
  await waitFor(() => {
    const posted = vi.mocked(fetch).mock.calls.find(
      ([u, i]) => String(u) === "/api/candidates/5/evaluations" && i?.method === "POST",
    );
    expect(posted).toBeTruthy();
    expect(JSON.parse(String(posted![1]?.body))).toEqual({
      criterion: "TypeScript bilgisi", score: 4, maxScore: 5,
    });
  });
});

test("eksik ve aralık dışı değerlendirme puanı gönderilmez", async () => {
  renderDetail();
  await screen.findByRole("heading", { name: "Burak Yılmaz" });
  await userEvent.click(screen.getByRole("button", { name: "Değerlendirme" }));
  await userEvent.type(screen.getByLabelText("Değerlendirme ölçütü"), "TypeScript bilgisi");
  await userEvent.click(screen.getByRole("button", { name: "Bilgileri kontrol et" }));
  expect(screen.getByRole("alert")).toHaveTextContent("Verilen puan 0 ile azami puan");
  await userEvent.type(screen.getByLabelText("Verilen puan"), "6");
  await userEvent.click(screen.getByRole("button", { name: "Bilgileri kontrol et" }));
  expect(screen.getByRole("alert")).toHaveTextContent("Verilen puan 0 ile azami puan");
  expect(vi.mocked(fetch).mock.calls.some(([, i]) => i?.method === "POST")).toBe(false);
});

test("değerlendirme alanı değişince eski ön kontrol geçersiz olur", async () => {
  renderDetail();
  await screen.findByRole("heading", { name: "Burak Yılmaz" });
  await userEvent.click(screen.getByRole("button", { name: "Değerlendirme" }));
  await userEvent.type(screen.getByLabelText("Değerlendirme ölçütü"), "TypeScript bilgisi");
  await userEvent.type(screen.getByLabelText("Verilen puan"), "4");
  await userEvent.click(screen.getByRole("button", { name: "Bilgileri kontrol et" }));
  await userEvent.clear(screen.getByLabelText("Verilen puan"));
  await userEvent.type(screen.getByLabelText("Verilen puan"), "3");
  expect(screen.queryByRole("button", { name: "Değerlendirmeyi kaydet" })).not.toBeInTheDocument();
  expect(screen.getByRole("button", { name: "Bilgileri kontrol et" })).toBeInTheDocument();
  expect(vi.mocked(fetch).mock.calls.some(([, i]) => i?.method === "POST")).toBe(false);
});

test("pipeline aşaması gerekçeli ön kontrol ve teyitten sonra değişir", async () => {
  renderDetail();
  await screen.findByRole("heading", { name: "Burak Yılmaz" });
  await userEvent.selectOptions(screen.getByLabelText("Pipeline durumu"), "Teklif");
  const review = screen.getByRole("region", { name: "Aday aşaması ön kontrolü" });
  expect(review).toHaveTextContent("Mülakat → Teklif");
  expect(review).toHaveTextContent("teklif belgesi hazırlamaz veya adaya göndermez");
  expect(review).toHaveTextContent("teklif belgesinin gönderimini ayrıca doğrulayın");
  expect(review).toHaveTextContent("önceki hareket geçmişte kalır");
  expect(screen.getByRole("button", { name: /İşe Al/ })).toBeDisabled();
  expect(screen.getByRole("button", { name: "Aşamayı kaydet" })).toBeDisabled();
  expect(vi.mocked(fetch).mock.calls.some(([u]) => String(u) === "/api/candidates/5/status")).toBe(false);
  await userEvent.click(screen.getByLabelText("Aday kaydını ve aşama gerekçesini kontrol ettim."));
  await userEvent.click(screen.getByRole("button", { name: "Aşamayı kaydet" }));
  await waitFor(() => {
    const patched = vi.mocked(fetch).mock.calls.find(
      ([u, i]) => String(u) === "/api/candidates/5/status" && i?.method === "PATCH",
    );
    expect(patched).toBeTruthy();
    expect(JSON.parse(String(patched![1]?.body))).toMatchObject({ status: "Teklif" });
  });
});

test("Yeni adayın ilk aşama ön kontrolü karar alıştırmasını gösterir", async () => {
  stubApi({
    "/api/candidates/5": { ...detail, status: "Yeni" },
    "/api/departments": departments,
    "/api/guidance/recruitment/checks/first-review": {
      stepKey: "first-review", contentVersion: 1,
      question: "Yeni adayın aşamasını değiştirmeden önce hangi kontrol gerekir?",
      choices: ["Yalnız genel aday puanına bakarım", "Başvuru, not ve değerlendirme dayanaklarını karşılaştırırım", "Aşama seçimi adaya otomatik bildirim gönderir"],
      lastSelectedChoice: null, lastCorrect: null, attemptCount: 0, explanation: null,
    },
  });
  renderDetail();
  await screen.findByRole("heading", { name: "Burak Yılmaz" });
  await userEvent.selectOptions(screen.getByLabelText("Pipeline durumu"), "Mülakat");
  expect(await screen.findByText(/Yeni adayın aşamasını değiştirmeden önce hangi kontrol gerekir/)).toBeInTheDocument();
  expect(screen.getByRole("region", { name: "Aday aşaması ön kontrolü" })).toHaveTextContent("Kısa karar alıştırması");
});

test("aşama seçimi vazgeçilince kayıt değişmez", async () => {
  renderDetail();
  await screen.findByRole("heading", { name: "Burak Yılmaz" });
  await userEvent.selectOptions(screen.getByLabelText("Pipeline durumu"), "Red");
  expect(screen.getByRole("region", { name: "Aday aşaması ön kontrolü" })).toHaveTextContent("adaya mesaj göndermez");
  await userEvent.click(screen.getByRole("button", { name: "Vazgeç" }));
  expect(screen.getByLabelText("Pipeline durumu")).toHaveValue("Mülakat");
  expect(screen.getByRole("button", { name: /İşe Al/ })).toBeEnabled();
  expect(vi.mocked(fetch).mock.calls.some(([u]) => String(u) === "/api/candidates/5/status")).toBe(false);
});

test("başka işlem aday aşamasını değiştirdiyse eski kararı göndermez", async () => {
  renderDetail();
  await screen.findByRole("heading", { name: "Burak Yılmaz" });
  await userEvent.selectOptions(screen.getByLabelText("Pipeline durumu"), "Teklif");
  await userEvent.click(screen.getByLabelText("Aday kaydını ve aşama gerekçesini kontrol ettim."));
  vi.stubGlobal("fetch", vi.fn(async () => new Response(JSON.stringify({ ...detail, status: "Red" }), {
    status: 200, headers: { "Content-Type": "application/json" },
  })));
  await userEvent.click(screen.getByRole("button", { name: "Aşamayı kaydet" }));
  expect(await screen.findByText(/Adayın durumu bu sırada değişti/)).toBeInTheDocument();
  expect(screen.getByLabelText("Pipeline durumu")).toHaveValue("Red");
  expect(vi.mocked(fetch).mock.calls.some(([u]) => String(u) === "/api/candidates/5/status")).toBe(false);
});

test("işe alma ön kontrolünden sonra personel kaydı oluşturur ve kalan işe giriş adımına bağlar", async () => {
  stubApi({
    "/api/candidates/5": detail,
    "/api/departments": departments,
    "/api/candidates/5/hire": {
      candidateId: 5, employeeId: 9, employeeName: "Burak Yılmaz",
      provisioning: { status: "pending", email: "burak.yilmaz@hrmaster.local" },
    },
  });
  renderDetail();
  await screen.findByRole("heading", { name: "Burak Yılmaz" });
  await userEvent.click(screen.getByRole("button", { name: /İşe Al/ }));
  await userEvent.selectOptions(await screen.findByLabelText("Departman"), "1");
  await userEvent.type(screen.getByLabelText("İş e-postası"), "burak.yilmaz@hrmaster.local");
  await userEvent.type(screen.getByLabelText("İşe giriş tarihi"), "2026-10-01");
  await userEvent.selectOptions(screen.getByLabelText("İmzalı sözleşmedeki tür"), "indefinite");
  await userEvent.click(screen.getByRole("button", { name: "Bilgileri kontrol et" }));
  expect(screen.getByRole("region", { name: "İşe almadan önce son kontrol" })).toHaveTextContent("2026-10-01");
  expect(vi.mocked(fetch).mock.calls.some(
    ([u, i]) => String(u) === "/api/candidates/5/hire" && i?.method === "POST",
  )).toBe(false);

  await userEvent.click(screen.getByRole("button", { name: "İşe almayı onayla" }));
  await waitFor(() => {
    const posted = vi.mocked(fetch).mock.calls.find(
      ([u, i]) => String(u) === "/api/candidates/5/hire" && i?.method === "POST",
    );
    expect(JSON.parse(String(posted?.[1]?.body))).toMatchObject({
      departmentId: 1, email: "burak.yilmaz@hrmaster.local", hireDate: "2026-10-01", contractType: "indefinite",
    });
  });
  expect((await screen.findByRole("heading", { name: "Personel kaydı oluşturuldu" })).closest("section")).toHaveTextContent("Giriş hesabı hazırlanıyor");
  expect(screen.getByRole("link", { name: "Personel kartını ve kalan adımları aç" }))
    .toHaveAttribute("href", "/app/personnel/9?edit=1&guide=1&tab=tab-evrak");
  expect(screen.getByRole("button", { name: /İşe Al/ })).toBeDisabled();
});

test("işe giriş tarihi açıkça seçilmeden işlem gönderilmez", async () => {
  renderDetail();
  await screen.findByRole("heading", { name: "Burak Yılmaz" });
  await userEvent.click(screen.getByRole("button", { name: /İşe Al/ }));
  await userEvent.selectOptions(await screen.findByLabelText("Departman"), "1");
  await userEvent.type(screen.getByLabelText("İş e-postası"), "burak.yilmaz@hrmaster.local");
  await userEvent.selectOptions(screen.getByLabelText("İmzalı sözleşmedeki tür"), "indefinite");
  await userEvent.click(screen.getByRole("button", { name: "Bilgileri kontrol et" }));
  expect(screen.getByRole("alert")).toHaveTextContent("İşe giriş tarihini onaylı iş bilgisiyle doğrulayıp seçin.");
  expect(vi.mocked(fetch).mock.calls.some(
    ([u, i]) => String(u) === "/api/candidates/5/hire" && i?.method === "POST",
  )).toBe(false);
});

test("belirli süreli sözleşmeyi ön kontrolde gösterip aynı türle işe alır", async () => {
  renderDetail();
  await screen.findByRole("heading", { name: "Burak Yılmaz" });
  await userEvent.click(screen.getByRole("button", { name: /İşe Al/ }));
  await userEvent.selectOptions(await screen.findByLabelText("Departman"), "1");
  await userEvent.type(screen.getByLabelText("İş e-postası"), "burak.yilmaz@hrmaster.local");
  await userEvent.type(screen.getByLabelText("İşe giriş tarihi"), "2026-10-01");
  await userEvent.selectOptions(screen.getByLabelText("İmzalı sözleşmedeki tür"), "fixed-term");
  await userEvent.click(screen.getByRole("button", { name: "Bilgileri kontrol et" }));
  expect(screen.getByRole("region", { name: "İşe almadan önce son kontrol" }))
    .toHaveTextContent("Belirli süreli olarak kaydedilecek");
  expect(vi.mocked(fetch).mock.calls.some(
    ([u, i]) => String(u) === "/api/candidates/5/hire" && i?.method === "POST",
  )).toBe(false);
  await userEvent.click(screen.getByRole("button", { name: "İşe almayı onayla" }));
  await waitFor(() => {
    const posted = vi.mocked(fetch).mock.calls.find(
      ([u, i]) => String(u) === "/api/candidates/5/hire" && i?.method === "POST",
    );
    expect(JSON.parse(String(posted?.[1]?.body))).toMatchObject({ contractType: "fixed-term" });
  });
});

test("reddedilmiş adayda işe alma yerine önce pipeline düzeltmesi ister", async () => {
  stubApi({ "/api/candidates/5": { ...detail, status: "Red" }, "/api/departments": departments });
  renderDetail();
  await screen.findByRole("heading", { name: "Burak Yılmaz" });
  expect(screen.getByRole("button", { name: /İşe Al/ })).toBeDisabled();
  expect(screen.getByText(/önce pipeline durumunu güncelleyin/)).toBeInTheDocument();
});

test("işe alınmış adayda durum select ve İşe Al pasiftir", async () => {
  stubApi({ "/api/candidates/5": { ...detail, status: "İşe Alındı" }, "/api/departments": departments });
  renderDetail();
  await screen.findByRole("heading", { name: "Burak Yılmaz" });
  expect(screen.getByLabelText("Pipeline durumu")).toBeDisabled();
  expect(screen.getByRole("button", { name: /İşe Al/ })).toBeDisabled();
});

test("bekleyen hesap kurulumu düzeltilmiş e-postayla yeniden denenir", async () => {
  stubApi({
    "/api/candidates/5": {
      ...detail,
      status: "İşe Alındı",
      provisioning: {
        status: "pending",
        email: "burak.hatali@hrmaster.local",
        attemptCount: 2,
        lastError: "E-posta servisine ulaşılamadı.",
      },
    },
    "/api/candidates/5/provisioning/retry": {
      status: "completed",
      email: "burak.yeni@hrmaster.local",
      attemptCount: 3,
      lastError: null,
    },
    "/api/departments": departments,
  });

  renderDetail();
  expect(await screen.findByText("Giriş hesabı hazırlanıyor")).toBeInTheDocument();
  expect(screen.getByText("E-posta servisine ulaşılamadı.")).toBeInTheDocument();

  const email = screen.getByLabelText("Provizyon e-postası");
  await userEvent.clear(email);
  await userEvent.type(email, "burak.yeni@hrmaster.local");
  await userEvent.click(screen.getByRole("button", { name: "Şimdi yeniden dene" }));

  await waitFor(() => {
    const posted = vi.mocked(fetch).mock.calls.find(
      ([u, i]) => String(u) === "/api/candidates/5/provisioning/retry" && i?.method === "POST",
    );
    expect(posted).toBeTruthy();
    expect(JSON.parse(String(posted![1]?.body))).toEqual({
      email: "burak.yeni@hrmaster.local",
    });
  });
});
