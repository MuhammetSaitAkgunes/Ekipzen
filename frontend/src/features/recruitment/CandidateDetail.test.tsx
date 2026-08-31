import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, expect, test, vi } from "vitest";
import { stubApi } from "../../test/apiStub";
import { renderPage } from "../../test/renderPage";
import { ToastProvider } from "../../layout/ToastProvider";
import { CandidateDetail } from "./CandidateDetail";

const detail = {
  id: 5, name: "Burak Yılmaz", appliedRole: "Senior Frontend Developer",
  positionId: null, positionTitle: null, status: "Mülakat", score: 92, initials: "BY",
  appliedAtUtc: "2026-07-16T10:00:00Z", location: "İstanbul", experienceYears: 5,
  summary: "React ve modern JavaScript konusunda güçlü aday.",
  skills: [{ id: 1, name: "React.js" }, { id: 2, name: "TypeScript" }],
  experiences: [{ id: 1, title: "Senior Frontend Developer", company: "TechSolutions A.Ş.", period: "2021 - Günümüz", description: "Arayüz geliştirme." }],
  notes: [{ id: 1, authorName: "Ayşe Demir", noteType: "İK Görüşmesi", text: "İletişim becerileri kuvvetli.", createdAtUtc: "2026-07-15T14:30:00Z" }],
  evaluations: [{ id: 1, criterion: "Teknik Yeterlilik", score: 4, maxScore: 5 }],
  history: [{ id: 1, event: "Başvuru alındı", occurredAtUtc: "2026-07-14T09:00:00Z" }],
};
const departments = [{ id: 1, name: "Yazılım", employeeCount: 4 }];

beforeEach(() =>
  stubApi({
    "/api/candidates/5": detail,
    "/api/candidates/5/status": { ...detail, status: "Teklif" },
    "/api/candidates/5/notes": detail.notes[0],
    "/api/candidates/5/hire": { candidateId: 5, employeeId: 9, employeeName: "Burak Yılmaz" },
    "/api/departments": departments,
  }),
);
afterEach(() => vi.unstubAllGlobals());

const renderDetail = () =>
  renderPage(
    <ToastProvider>
      <CandidateDetail id={5} />
    </ToastProvider>,
  );

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
  await userEvent.click(screen.getByRole("button", { name: "Not Ekle" }));
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

test("pipeline durumu PATCH status ucuna gider", async () => {
  renderDetail();
  await screen.findByRole("heading", { name: "Burak Yılmaz" });
  await userEvent.selectOptions(screen.getByLabelText("Pipeline durumu"), "Teklif");
  await waitFor(() => {
    const patched = vi.mocked(fetch).mock.calls.find(
      ([u, i]) => String(u) === "/api/candidates/5/status" && i?.method === "PATCH",
    );
    expect(patched).toBeTruthy();
    expect(JSON.parse(String(patched![1]?.body))).toMatchObject({ status: "Teklif" });
  });
});

test("İşe Al modalı departmanla hire ucuna gider", async () => {
  renderDetail();
  await screen.findByRole("heading", { name: "Burak Yılmaz" });
  await userEvent.click(screen.getByRole("button", { name: /İşe Al/ }));
  await userEvent.selectOptions(await screen.findByLabelText("Departman"), "1");
  await userEvent.type(screen.getByLabelText("İş e-postası"), "burak.yilmaz@hrmaster.local");
  await userEvent.click(screen.getByRole("button", { name: /Onayla/ }));
  await waitFor(() => {
    const posted = vi.mocked(fetch).mock.calls.find(
      ([u, i]) => String(u) === "/api/candidates/5/hire" && i?.method === "POST",
    );
    expect(posted).toBeTruthy();
    expect(JSON.parse(String(posted![1]?.body))).toMatchObject({
      departmentId: 1, email: "burak.yilmaz@hrmaster.local",
    });
  });
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
