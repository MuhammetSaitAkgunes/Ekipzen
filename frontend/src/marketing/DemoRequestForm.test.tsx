import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, expect, test, vi } from "vitest";
import { renderPage } from "../test/renderPage";
import { DemoRequestForm } from "./DemoRequestForm";

beforeEach(() => vi.stubGlobal("fetch", vi.fn()));
afterEach(() => vi.unstubAllGlobals());

const fillValid = async () => {
  await userEvent.type(screen.getByLabelText(/Ad soyad/), "Ayşe Demir");
  await userEvent.type(screen.getByLabelText(/İş e-postası/), "ayse@firma.example");
  await userEvent.type(screen.getByLabelText(/Şirket adı/), "Firma A.Ş.");
  await userEvent.selectOptions(screen.getByLabelText(/Çalışan sayısı/), "51-200");
  await userEvent.click(screen.getByRole("checkbox", { name: /KVKK aydınlatma metnini/ }));
};

test("geçerli form gönderilir ve teşekkür mesajı gösterilir", async () => {
  vi.mocked(fetch).mockResolvedValueOnce(new Response(null, { status: 204 }));
  renderPage(<DemoRequestForm />);
  await fillValid();
  await userEvent.click(screen.getByRole("button", { name: "Demo talebini gönder" }));

  expect(await screen.findByRole("heading", { name: "Talebiniz bize ulaştı" })).toBeInTheDocument();
  const [url, init] = vi.mocked(fetch).mock.calls[0];
  expect(url).toBe("/api/public/demo-requests");
  expect(JSON.parse(init!.body as string)).toMatchObject({
    fullName: "Ayşe Demir", email: "ayse@firma.example", company: "Firma A.Ş.", companySize: "51-200", kvkkConsent: true, website: "",
  });
});

test("KVKK onayı ve zorunlu alanlar olmadan istek atılmaz", async () => {
  renderPage(<DemoRequestForm />);
  await userEvent.click(screen.getByRole("button", { name: "Demo talebini gönder" }));
  expect(fetch).not.toHaveBeenCalled();
  expect(screen.getByText("Ad soyad zorunludur.")).toBeInTheDocument();
  expect(screen.getByText("KVKK aydınlatma metnini onaylamanız gerekir.")).toBeInTheDocument();
  expect(screen.getByLabelText(/Ad soyad/)).toHaveFocus();
});

test("geçersiz e-posta istemcide yakalanır", async () => {
  renderPage(<DemoRequestForm />);
  await fillValid();
  await userEvent.clear(screen.getByLabelText(/İş e-postası/));
  await userEvent.type(screen.getByLabelText(/İş e-postası/), "gecersiz");
  await userEvent.click(screen.getByRole("button", { name: "Demo talebini gönder" }));
  expect(fetch).not.toHaveBeenCalled();
  expect(screen.getByText("Geçerli bir e-posta adresi girin.")).toBeInTheDocument();
});

test("sunucu doğrulama hatası ilgili alanda gösterilir", async () => {
  vi.mocked(fetch).mockResolvedValueOnce(new Response(
    JSON.stringify({ title: "Doğrulama hatası.", status: 400, errors: { Company: ["Şirket adı çok uzun."] } }),
    { status: 400, headers: { "Content-Type": "application/json" } },
  ));
  renderPage(<DemoRequestForm />);
  await fillValid();
  await userEvent.click(screen.getByRole("button", { name: "Demo talebini gönder" }));
  expect(await screen.findByText("Şirket adı çok uzun.")).toBeInTheDocument();
});

test("çok fazla istek ve beklenmeyen hata genel mesajla gösterilir", async () => {
  vi.mocked(fetch).mockResolvedValueOnce(new Response(null, { status: 429 }));
  renderPage(<DemoRequestForm />);
  await fillValid();
  await userEvent.click(screen.getByRole("button", { name: "Demo talebini gönder" }));
  expect(await screen.findByText(/Çok fazla talep gönderildi/)).toBeInTheDocument();
});

test("bilinen forma eşlenmeyen sunucu hata anahtarı genel mesajla gösterilir", async () => {
  vi.mocked(fetch).mockResolvedValueOnce(new Response(
    JSON.stringify({ title: "Doğrulama hatası.", status: 400, errors: { "$.fullName": ["Beklenmeyen JSON."], command: ["Geçersiz istek."] } }),
    { status: 400, headers: { "Content-Type": "application/json" } },
  ));
  renderPage(<DemoRequestForm />);
  await fillValid();
  await userEvent.click(screen.getByRole("button", { name: "Demo talebini gönder" }));
  expect(await screen.findByText("Talebiniz şu anda gönderilemedi. Lütfen tekrar deneyin ya da bize e-postayla ulaşın.")).toBeInTheDocument();
});
